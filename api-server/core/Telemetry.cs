using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace api_server.core
{
	/// <summary>
	/// Custom telemetry instruments for hand-placed spans and metrics. The name is
	/// registered in Program.cs via AddSource()/AddMeter() — a span or metric from an
	/// unregistered source is silently dropped, so new sources must be added there too.
	/// </summary>
	public static class Telemetry
	{
		public const string SourceName = "mf.aiConsole";

		/// <summary>Custom spans: using var activity = Telemetry.Source.StartActivity("area.action");</summary>
		public static readonly ActivitySource Source = new(SourceName);

		public static readonly Meter Meter = new(SourceName);

		/// <summary>Counts login attempts rejected for bad credentials or inactive accounts.</summary>
		public static readonly Counter<long> LoginFailures =
			Meter.CreateCounter<long>("app.auth.login_failures", description: "Failed login attempts");

		/// <summary>Decompressed size of client-log uploads (the endpoint itself is untraced).</summary>
		public static readonly Histogram<long> ClientLogPayloadBytes =
			Meter.CreateHistogram<long>("app.client_log.payload_size", unit: "By",
				description: "Decompressed size of flight-recorder uploads");

		/// <summary>Distribution of uploaded media file sizes.</summary>
		public static readonly Histogram<long> MediaUploadBytes =
			Meter.CreateHistogram<long>("app.media.upload_size", unit: "By",
				description: "Size of files uploaded through /api/media/upload");
	}
}
