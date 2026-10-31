using db_model.Map;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api_server.Controllers.Fleet.Services
{
	public sealed class FuelEconomyOptions
	{
		/// <summary>How far before the start of a period the last reading is looked for (the value in force at the start). A device silent for longer has no baseline.</summary>
		public int BaselineLookbackDays { get; set; } = 7;

		/// <summary>The longest period one call may ask for.</summary>
		public int MaxPeriodDays { get; set; } = 366;
	}

	public enum FuelEconomyCoverage
	{
		/// <summary>No reading of any counter in or before the period: nothing is known.</summary>
		None,
		/// <summary>The first reading of a counter came inside the period (a new device, or one silent before it): what is counted starts there.</summary>
		Partial,
		/// <summary>Every counter that reported had a reading before the period to start from.</summary>
		Full,
	}

	/// <summary>
	/// What a device used between two hour-aligned times. Distance is metres, engine time seconds, fuel litres. <see cref="LitresPer100Km"/> is null
	/// below one kilometre (a quotient of tiny numbers means nothing) and without fuel or distance. <see cref="Resets"/> counts the steps where a
	/// counter went down (a replaced device or a reset): those steps are skipped, not counted as negative use.
	/// </summary>
	public sealed record FuelEconomy(
		int GpsDeviceId, DateTime FromUtc, DateTime ToUtc, FuelEconomyCoverage Coverage,
		double? DistanceMetres, double? EngineSeconds, double? FuelLitres, double? IdleFuelLitres,
		double? LitresPer100Km, double? IdleShare, int Resets);

	/// <summary>The arithmetic, on rows already loaded, so it can be tested without a database.</summary>
	public static class FuelEconomyCalculator
	{
		public const double MinDistanceMetres = 1000;

		/// <param name="rows">Rows of one device, any order: those before <paramref name="fromUtc"/> give the starting value, those in [from, to) are the period.</param>
		public static FuelEconomy Calculate(int gpsDeviceId, DateTime fromUtc, DateTime toUtc, IEnumerable<GpsDeviceHourlyTotal> rows)
		{
			var ordered = rows.OrderBy(r => r.HourUtc).ToList();
			var sums = new double?[4];
			var resets = 0;
			var partial = false;
			var any = false;

			// The four counters work the same way; each has its own baseline and its own gaps.
			for (var column = 0; column < 4; column++)
			{
				double? previous = null;
				foreach (var row in ordered.Where(r => r.HourUtc < fromUtc))
				{
					if (valueOf(row, column) is { } v) previous = v; // the last reading before the period is the value in force at its start
				}
				var hadBaseline = previous != null;

				double sum = 0;
				var seen = false;
				foreach (var row in ordered.Where(r => r.HourUtc >= fromUtc && r.HourUtc < toUtc))
				{
					if (valueOf(row, column) is not { } value) continue;
					if (previous == null)
					{
						previous = value; // the first reading of this counter: it starts the count, nothing before it is known
						partial = true;
					}
					else if (value >= previous.Value)
					{
						sum += value - previous.Value;
						previous = value;
					}
					else
					{
						resets++;
						previous = value;
					}
					seen = true;
				}

				if (hadBaseline || seen)
				{
					sums[column] = sum;
					any = true;
				}
			}

			var distance = sums[0];
			var fuel = sums[2];
			var idle = sums[3];
			return new FuelEconomy(gpsDeviceId, fromUtc, toUtc,
				!any ? FuelEconomyCoverage.None : partial ? FuelEconomyCoverage.Partial : FuelEconomyCoverage.Full,
				distance, sums[1], fuel, idle,
				fuel is { } f && distance is { } d && d >= MinDistanceMetres ? f / (d / 1000) * 100 : null,
				fuel is > 0 && idle is { } i ? Math.Min(1, i / fuel.Value) : null,
				resets);
		}

		private static double? valueOf(GpsDeviceHourlyTotal row, int column) => column switch
		{
			0 => row.Odometer,
			1 => row.EngineHours,
			2 => row.FuelTotal,
			_ => row.FuelIdleTotal,
		};
	}

	public interface IFuelEconomyService
	{
		/// <summary>
		/// Distance, fuel and idle fuel of each device between two times, from the hourly readings. Both times are rounded down to the hour: the
		/// period is whole hours [from, to). One result per requested device, in the order asked, also for a device without any reading.
		/// </summary>
		Task<IReadOnlyList<FuelEconomy>> CalculateAsync(IReadOnlyCollection<int> gpsDeviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
	}

	public sealed class FuelEconomyService : IFuelEconomyService
	{
		private readonly IRepository mRepository;
		private readonly FuelEconomyOptions mOptions;

		public FuelEconomyService(IRepository repository, IOptions<FuelEconomyOptions> options)
		{
			mRepository = repository;
			mOptions = options.Value;
		}

		public async Task<IReadOnlyList<FuelEconomy>> CalculateAsync(IReadOnlyCollection<int> gpsDeviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
		{
			var from = hourOf(fromUtc);
			var to = hourOf(toUtc);
			if (to <= from) throw new ArgumentException("The period must contain at least one whole hour.", nameof(toUtc));
			if (to - from > TimeSpan.FromDays(mOptions.MaxPeriodDays)) throw new ArgumentException($"The period must not be longer than {mOptions.MaxPeriodDays} days.", nameof(toUtc));

			var ids = gpsDeviceIds.Distinct().ToList();
			if (ids.Count == 0) return Array.Empty<FuelEconomy>();

			var since = from - TimeSpan.FromDays(mOptions.BaselineLookbackDays);
			var rows = await mRepository.GetQueryable<GpsDeviceHourlyTotal>(h => ids.Contains(h.GpsDeviceId) && h.HourUtc >= since && h.HourUtc < to)
				.AsNoTracking().ToListAsync(cancellationToken);
			var byDevice = rows.ToLookup(r => r.GpsDeviceId);
			return ids.Select(id => FuelEconomyCalculator.Calculate(id, from, to, byDevice[id])).ToList();
		}

		private static DateTime hourOf(DateTime at)
		{
			var utc = at.Kind == DateTimeKind.Local ? at.ToUniversalTime() : DateTime.SpecifyKind(at, DateTimeKind.Utc);
			return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerHour, DateTimeKind.Utc);
		}
	}
}
