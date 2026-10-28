using System.Threading.Channels;
using api_server.core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace api_server.Services.Feeders
{
	/// <summary>
	/// Tells the feeders which company's map somebody is looking at, so they poll that company's accounts every couple of seconds
	/// instead of every 30. Call <see cref="CompanyWatched"/> whenever a company's live map data is served; it is cheap, never blocks
	/// and never throws, so it can sit on a request path. Safe from any thread.
	/// </summary>
	public interface IFeederHeartbeat
	{
		void CompanyWatched(int companyId);
	}

	public sealed class FeederHeartbeatOptions
	{
		public const string SectionName = "Feeders:Heartbeat";

		/// <summary>The feeder's address, like http://127.0.0.1:5110. Empty: heartbeats are off (the feeders then poll at their baseline interval).</summary>
		public string? BaseUrl { get; set; }

		/// <summary>The key this API is known by in the feeder's <c>Feeders:Api:Clients</c> (the feeder keeps only its SHA-256). A secret: keep it in local_configs, not in appsettings.json.</summary>
		public string? ApiKey { get; set; }

		/// <summary>
		/// At most one heartbeat per company per this many seconds, however often the map is read. The feeder keeps a company live for
		/// 45 s after a heartbeat by default, so this must stay well below that.
		/// </summary>
		public int MinIntervalSeconds { get; set; } = 10;

		public int TimeoutSeconds { get; set; } = 3;

		/// <summary>After a failed call (feeder down, key refused) no heartbeat is attempted for this long, so a feeder outage costs the API nothing.</summary>
		public int FailureBackoffSeconds { get; set; } = 30;

		/// <summary>Heartbeats waiting to be sent; more than this are dropped (and retried by the next map read).</summary>
		public int QueueCapacity { get; set; } = 256;

		public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);

		public IReadOnlyList<string> Validate()
		{
			var errors = new List<string>();
			if (!IsConfigured) return errors;

			if (!Uri.TryCreate(BaseUrl!.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
			{
				errors.Add($"{SectionName}:{nameof(BaseUrl)} must be an absolute http or https address.");
			}
			if (string.IsNullOrWhiteSpace(ApiKey)) errors.Add($"{SectionName}:{nameof(ApiKey)} is required when {nameof(BaseUrl)} is set.");
			if (MinIntervalSeconds is < 1 or > 30) errors.Add($"{SectionName}:{nameof(MinIntervalSeconds)} must be between 1 and 30 (the feeder's live grace is 45 s by default).");
			if (TimeoutSeconds is < 1 or > 30) errors.Add($"{SectionName}:{nameof(TimeoutSeconds)} must be between 1 and 30.");
			if (FailureBackoffSeconds < 1) errors.Add($"{SectionName}:{nameof(FailureBackoffSeconds)} must be at least 1.");
			if (QueueCapacity < 1) errors.Add($"{SectionName}:{nameof(QueueCapacity)} must be at least 1.");
			return errors;
		}
	}

	public enum HeartbeatResult
	{
		Accepted,
		/// <summary>The feeder does not accept this API's key: nothing will work until the configuration is fixed.</summary>
		Rejected,
		/// <summary>The feeder refused this one request (a bad company id); other companies are unaffected.</summary>
		Invalid,
		/// <summary>The feeder cannot be reached or is overloaded.</summary>
		Unavailable,
	}

	public interface IFeederHeartbeatTransport
	{
		Task<HeartbeatResult> SendAsync(int companyId, CancellationToken cancellationToken);
	}

	/// <summary>
	/// The debouncer and sender behind <see cref="IFeederHeartbeat"/>. Best effort by design: the map must keep working with the feeder down,
	/// misconfigured or slow, so nothing here ever reaches the caller, and after a failure it stops trying for a while instead of
	/// timing out on every map read.
	/// </summary>
	public sealed class FeederHeartbeat : BackgroundService, IFeederHeartbeat
	{
		private readonly IFeederHeartbeatTransport mTransport;
		private readonly TimeProvider mTime;
		private readonly ILogger<FeederHeartbeat> mLog;
		private readonly FeederHeartbeatOptions mOptions;
		private readonly IReadOnlyList<string> mConfigurationErrors;
		private readonly bool mEnabled;
		private readonly TimeSpan mMinInterval;
		private readonly TimeSpan mBackoff;
		private readonly Channel<int> mQueue;

		private readonly object mLock = new();
		private readonly Dictionary<int, DateTimeOffset> mLastQueued = new();
		private DateTimeOffset mBlockedUntil = DateTimeOffset.MinValue;
		private bool mFailing;

		public FeederHeartbeat(IOptions<FeederHeartbeatOptions> options, IFeederHeartbeatTransport transport, TimeProvider time, ILogger<FeederHeartbeat> log)
		{
			mOptions = options.Value;
			mTransport = transport;
			mTime = time;
			mLog = log;
			mConfigurationErrors = mOptions.Validate();
			mEnabled = mOptions.IsConfigured && mConfigurationErrors.Count == 0;
			mMinInterval = TimeSpan.FromSeconds(Math.Max(1, mOptions.MinIntervalSeconds));
			mBackoff = TimeSpan.FromSeconds(Math.Max(1, mOptions.FailureBackoffSeconds));
			mQueue = Channel.CreateBounded<int>(new BoundedChannelOptions(Math.Max(1, mOptions.QueueCapacity)) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
		}

		public bool Enabled => mEnabled;

		/// <summary>Companies remembered for debouncing; old ones are forgotten, so this follows the number of companies currently watched.</summary>
		public int TrackedCompanyCount
		{
			get
			{
				lock (mLock) return mLastQueued.Count;
			}
		}

		public void CompanyWatched(int companyId)
		{
			if (!mEnabled || companyId <= 0) return;
			try
			{
				var now = mTime.GetUtcNow();
				lock (mLock)
				{
					if (now < mBlockedUntil)
					{
						Telemetry.FeederHeartbeatDropped.Add(1, new KeyValuePair<string, object?>("reason", "backoff"));
						return;
					}
					if (mLastQueued.TryGetValue(companyId, out var last) && now - last < mMinInterval) return; // sent a moment ago: the feeder is still live

					if (!mQueue.Writer.TryWrite(companyId))
					{
						Telemetry.FeederHeartbeatDropped.Add(1, new KeyValuePair<string, object?>("reason", "queue_full"));
						return; // not remembered, so the next map read tries again
					}
					mLastQueued[companyId] = now;
					if (mLastQueued.Count > 1024) forgetOld(now);
				}
			}
			catch (Exception ex)
			{
				// Nothing on a request path may fail because of a heartbeat.
				mLog.LogDebug(ex, "Could not queue a feeder heartbeat for company {CompanyId}.", companyId);
			}
		}

		/// <summary>Entries older than the debounce interval would not suppress anything any more.</summary>
		private void forgetOld(DateTimeOffset now)
		{
			var stale = mLastQueued.Where(e => now - e.Value >= mMinInterval).Select(e => e.Key).ToList();
			foreach (var id in stale) mLastQueued.Remove(id);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			if (!mEnabled)
			{
				if (mConfigurationErrors.Count > 0)
				{
					mLog.LogError("Feeder heartbeats are OFF because the configuration is invalid (the API keeps working, the feeders poll at their baseline interval): {Errors}", string.Join(" ", mConfigurationErrors));
				}
				else
				{
					mLog.LogInformation("Feeder heartbeats are off ({Section}:BaseUrl is not set); the feeders poll at their baseline interval.", FeederHeartbeatOptions.SectionName);
				}
				return;
			}

			mLog.LogInformation("Feeder heartbeats go to {BaseUrl}, at most one per company per {Seconds} s.", mOptions.BaseUrl, mOptions.MinIntervalSeconds);
			try
			{
				await foreach (var companyId in mQueue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
				{
					await sendAsync(companyId, stoppingToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Stopping.
			}
		}

		private async Task sendAsync(int companyId, CancellationToken cancellationToken)
		{
			bool blocked;
			lock (mLock) blocked = mTime.GetUtcNow() < mBlockedUntil;
			if (blocked)
			{
				Telemetry.FeederHeartbeatDropped.Add(1, new KeyValuePair<string, object?>("reason", "backoff"));
				return;
			}

			HeartbeatResult result;
			try
			{
				result = await mTransport.SendAsync(companyId, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				mLog.LogDebug(ex, "Feeder heartbeat transport failed for company {CompanyId}.", companyId);
				result = HeartbeatResult.Unavailable;
			}

			switch (result)
			{
				case HeartbeatResult.Accepted:
					Telemetry.FeederHeartbeatSent.Add(1);
					if (mFailing)
					{
						mFailing = false;
						mLog.LogInformation("Feeder heartbeats are being accepted again.");
					}
					break;

				case HeartbeatResult.Invalid:
					Telemetry.FeederHeartbeatFailed.Add(1, new KeyValuePair<string, object?>("result", "invalid"));
					mLog.LogWarning("The feeder refused the heartbeat for company {CompanyId}.", companyId);
					break;

				default:
					Telemetry.FeederHeartbeatFailed.Add(1, new KeyValuePair<string, object?>("result", result == HeartbeatResult.Rejected ? "rejected" : "unavailable"));
					lock (mLock) mBlockedUntil = mTime.GetUtcNow() + mBackoff;
					if (!mFailing)
					{
						mFailing = true;
						if (result == HeartbeatResult.Rejected)
						{
							mLog.LogError("The feeder rejected this API's key: check {Section}:ApiKey against the feeder's Feeders:Api:Clients. Heartbeats pause for {Seconds} s and are retried; until fixed the feeders poll at their baseline interval.", FeederHeartbeatOptions.SectionName, mOptions.FailureBackoffSeconds);
						}
						else
						{
							mLog.LogWarning("The feeder at {BaseUrl} cannot be reached; heartbeats pause for {Seconds} s and are retried. Until then the feeders poll at their baseline interval.", mOptions.BaseUrl, mOptions.FailureBackoffSeconds);
						}
					}
					break;
			}
		}
	}

	/// <summary>Posts the heartbeat to the feeder. Never throws for a network or server problem: that is <see cref="HeartbeatResult.Unavailable"/>.</summary>
	public sealed class HttpFeederHeartbeatTransport : IFeederHeartbeatTransport
	{
		public const string HttpClientName = "feeder-heartbeat";

		private readonly IHttpClientFactory mClients;

		public HttpFeederHeartbeatTransport(IHttpClientFactory clients) => mClients = clients;

		public async Task<HeartbeatResult> SendAsync(int companyId, CancellationToken cancellationToken)
		{
			try
			{
				// A client per call: the factory pools the connections and renews them when the feeder's address changes.
				var client = mClients.CreateClient(HttpClientName);
				using var request = new HttpRequestMessage(HttpMethod.Post, $"v1/companies/{companyId}/live");
				using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				return (int)response.StatusCode switch
				{
					>= 200 and < 300 => HeartbeatResult.Accepted,
					401 or 403 => HeartbeatResult.Rejected,
					429 or >= 500 => HeartbeatResult.Unavailable,
					_ => HeartbeatResult.Invalid,
				};
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException)
			{
				return HeartbeatResult.Unavailable; // refused connection, DNS, timeout
			}
		}
	}

	public static class FeederHeartbeatRegistration
	{
		/// <summary>The heartbeat and its HTTP client. With <c>Feeders:Heartbeat:BaseUrl</c> unset everything is registered but does nothing.</summary>
		public static IServiceCollection AddFeederHeartbeat(this IServiceCollection services, IConfiguration configuration)
		{
			services.TryAddSingleton(TimeProvider.System);
			services.Configure<FeederHeartbeatOptions>(configuration.GetSection(FeederHeartbeatOptions.SectionName));
			services.AddHttpClient(HttpFeederHeartbeatTransport.HttpClientName).ConfigureHttpClient((sp, client) =>
			{
				// Invalid settings leave the client unconfigured: the heartbeat is off then, and says why at startup.
				var options = sp.GetRequiredService<IOptions<FeederHeartbeatOptions>>().Value;
				if (!options.IsConfigured || options.Validate().Count > 0) return;
				var baseUrl = options.BaseUrl!.Trim();
				client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
				client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
				client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey!.Trim());
			});
			services.AddSingleton<IFeederHeartbeatTransport, HttpFeederHeartbeatTransport>();
			services.AddSingleton<FeederHeartbeat>();
			services.AddSingleton<IFeederHeartbeat>(sp => sp.GetRequiredService<FeederHeartbeat>());
			services.AddHostedService(sp => sp.GetRequiredService<FeederHeartbeat>());
			return services;
		}
	}
}
