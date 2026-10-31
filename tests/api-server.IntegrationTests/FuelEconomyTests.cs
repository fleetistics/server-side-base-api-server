using api_server.Controllers.Fleet.Services;
using db_model.Gps;
using db_model.Map;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>Fuel used, distance and idle share are differences of the hourly counter readings; these are the rules and the edges.</summary>
public sealed class FuelEconomyCalculatorTests
{
    private static readonly DateTime Day = new(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);

    private static GpsDeviceHourlyTotal row(int hourOffset, double? odometer = null, double? fuel = null, double? idle = null, double? engine = null) => new()
    {
        GpsDeviceId = 1, HourUtc = Day.AddHours(hourOffset), Odometer = odometer, EngineHours = engine, FuelTotal = fuel, FuelIdleTotal = idle,
    };

    private static FuelEconomy calc(params GpsDeviceHourlyTotal[] rows) => FuelEconomyCalculator.Calculate(1, Day, Day.AddHours(24), rows);

    [Fact]
    public void Distance_fuel_idle_and_economy_are_the_sums_of_the_steps_from_the_reading_before_the_period()
    {
        var result = calc(
            row(-2, odometer: 100_000, fuel: 500, idle: 50),             // the value in force at the start
            row(3, odometer: 130_000, fuel: 510, idle: 52),
            row(9, odometer: 160_000, fuel: 522, idle: 52.5),
            row(20, odometer: 200_000, fuel: 530, idle: 55));

        result.Coverage.ShouldBe(FuelEconomyCoverage.Full);
        result.DistanceMetres.ShouldBe(100_000);
        result.FuelLitres.ShouldBe(30);
        result.IdleFuelLitres.ShouldBe(5);
        result.LitresPer100Km!.Value.ShouldBe(30.0, 1e-9);     // 30 L over 100 km
        result.IdleShare!.Value.ShouldBe(5.0 / 30.0, 1e-9);
        result.Resets.ShouldBe(0);
    }

    [Fact]
    public void A_period_with_no_new_reading_is_zero_use_when_a_starting_value_is_known()
    {
        var result = calc(row(-30, odometer: 100_000, fuel: 500, idle: 50));

        result.Coverage.ShouldBe(FuelEconomyCoverage.Full);
        result.DistanceMetres.ShouldBe(0);
        result.FuelLitres.ShouldBe(0);
        result.LitresPer100Km.ShouldBeNull(); // no distance: no economy
        result.IdleShare.ShouldBeNull();      // no fuel: no share
    }

    [Fact]
    public void A_device_that_starts_inside_the_period_counts_from_its_first_reading_and_is_marked_partial()
    {
        var result = calc(
            row(6, odometer: 1_000, fuel: 10, idle: 1),
            row(12, odometer: 51_000, fuel: 14, idle: 1.5),
            row(18, odometer: 81_000, fuel: 16, idle: 1.5));

        result.Coverage.ShouldBe(FuelEconomyCoverage.Partial);
        result.DistanceMetres.ShouldBe(80_000); // from the first reading, not from zero
        result.FuelLitres.ShouldBe(6);
    }

    [Fact]
    public void A_counter_that_goes_down_is_a_reset_the_step_is_skipped_and_counting_goes_on_from_the_new_value()
    {
        var result = calc(
            row(-1, fuel: 1000),
            row(4, fuel: 1010),     // +10
            row(8, fuel: 3),        // replaced device: not a negative use
            row(12, fuel: 8));      // +5 from the new start

        result.FuelLitres.ShouldBe(15);
        result.Resets.ShouldBe(1);
    }

    [Fact]
    public void Counters_that_are_missing_stay_unknown_and_do_not_hide_the_others()
    {
        var result = calc(row(-1, odometer: 100_000), row(10, odometer: 150_000));

        result.DistanceMetres.ShouldBe(50_000);
        result.FuelLitres.ShouldBeNull();
        result.IdleFuelLitres.ShouldBeNull();
        result.EngineSeconds.ShouldBeNull();
        result.LitresPer100Km.ShouldBeNull();
        result.Coverage.ShouldBe(FuelEconomyCoverage.Full);
    }

    [Fact]
    public void Each_counter_has_its_own_gaps_a_reading_hour_may_carry_only_some_of_them()
    {
        var result = calc(
            row(-5, odometer: 0, fuel: 100),
            row(5, odometer: 40_000),               // fuel did not report this hour
            row(10, fuel: 104),                     // odometer did not
            row(15, odometer: 80_000, fuel: 108));

        result.DistanceMetres.ShouldBe(80_000);
        result.FuelLitres.ShouldBe(8);
        result.LitresPer100Km!.Value.ShouldBe(10.0, 1e-9);
    }

    [Fact]
    public void Without_any_reading_nothing_is_known()
    {
        var result = calc();

        result.Coverage.ShouldBe(FuelEconomyCoverage.None);
        result.DistanceMetres.ShouldBeNull();
        result.FuelLitres.ShouldBeNull();
        result.Resets.ShouldBe(0);
    }

    [Fact]
    public void Under_a_kilometre_there_is_no_economy_because_a_quotient_of_tiny_numbers_means_nothing()
    {
        var result = calc(row(-1, odometer: 100_000, fuel: 500), row(5, odometer: 100_500, fuel: 500.4));

        result.DistanceMetres.ShouldBe(500);
        result.FuelLitres!.Value.ShouldBe(0.4, 1e-9);
        result.LitresPer100Km.ShouldBeNull();
    }

    [Fact]
    public void Readings_at_or_after_the_end_of_the_period_do_not_count_and_the_order_of_the_rows_does_not_matter()
    {
        var result = calc(
            row(25, fuel: 999),             // after the period
            row(24, fuel: 900),             // the hour that starts at the end: not in [from, to)
            row(10, fuel: 520),
            row(-3, fuel: 500));

        result.FuelLitres.ShouldBe(20);
    }

    [Fact]
    public void The_idle_share_never_exceeds_one()
    {
        var result = calc(row(-1, fuel: 100, idle: 10), row(5, fuel: 101, idle: 12));

        result.IdleShare.ShouldBe(1);
    }

    [Fact]
    public void Engine_time_is_summed_like_the_others()
    {
        var result = calc(row(-1, engine: 1000), row(5, engine: 4600), row(20, engine: 8200));

        result.EngineSeconds.ShouldBe(7200);
    }
}

/// <summary>The service: the rows it asks for, the rounding of the period, and what it refuses.</summary>
[Collection(ApiCollection.Name)]
public sealed class FuelEconomyServiceTests : IAsyncLifetime
{
    private static readonly DateTime Day = new(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);
    private readonly ApiFixture _fixture;

    public FuelEconomyServiceTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<int> DeviceAsync() => await _fixture.QueryDbAsync(async db =>
    {
        var device = new GpsDevice { ProviderId = GpsDeviceProvider.Geotab, StatusId = GpsDeviceStatus.Active, SerialNumber = "SN-" + Guid.NewGuid().ToString("N") };
        db.Add(device);
        await db.SaveChangesAsync();
        return device.Id;
    });

    private Task AddRowsAsync(int device, params (int Hour, double? Odometer, double? Fuel, double? Idle)[] rows) => _fixture.QueryDbAsync(async db =>
    {
        foreach (var r in rows)
        {
            db.Add(new GpsDeviceHourlyTotal { GpsDeviceId = device, HourUtc = Day.AddHours(r.Hour), Odometer = r.Odometer, FuelTotal = r.Fuel, FuelIdleTotal = r.Idle });
        }
        await db.SaveChangesAsync();
        return 0;
    });

    private async Task<IReadOnlyList<FuelEconomy>> CalculateAsync(IReadOnlyCollection<int> ids, DateTime from, DateTime to)
    {
        using var scope = _fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IFuelEconomyService>().CalculateAsync(ids, from, to, CancellationToken.None);
    }

    [Fact]
    public async Task It_calculates_per_device_in_the_order_asked_and_gives_a_result_even_for_a_device_with_no_reading()
    {
        var a = await DeviceAsync();
        var b = await DeviceAsync();
        var silent = await DeviceAsync();
        await AddRowsAsync(a, (-2, 100_000, 500, 50), (10, 160_000, 512, 51), (20, 200_000, 520, 52));
        await AddRowsAsync(b, (-2, 5_000, 80, 8), (15, 25_000, 90, 8));

        var results = await CalculateAsync([b, silent, a], Day, Day.AddDays(1));

        results.Select(r => r.GpsDeviceId).ShouldBe(new[] { b, silent, a });
        results[0].FuelLitres.ShouldBe(10);
        results[0].DistanceMetres.ShouldBe(20_000);
        results[1].Coverage.ShouldBe(FuelEconomyCoverage.None);
        results[2].FuelLitres.ShouldBe(20);
        results[2].LitresPer100Km!.Value.ShouldBe(20.0, 1e-9);
    }

    [Fact]
    public async Task The_period_is_rounded_down_to_whole_hours()
    {
        var device = await DeviceAsync();
        await AddRowsAsync(device, (-1, null, 100, null), (5, null, 110, null), (23, null, 120, null));

        var results = await CalculateAsync([device], Day.AddMinutes(20), Day.AddDays(1).AddMinutes(59));

        results.Single().FromUtc.ShouldBe(Day);
        results.Single().ToUtc.ShouldBe(Day.AddDays(1));
        results.Single().FuelLitres.ShouldBe(20); // the hour that starts at 23:00 is inside, the one at 24:00 is not
    }

    [Fact]
    public async Task A_starting_reading_older_than_the_lookback_is_not_used_so_the_device_counts_as_starting_inside_the_period()
    {
        var device = await DeviceAsync();
        await AddRowsAsync(device, (-24 * 30, null, 100, null), (6, null, 130, null), (12, null, 140, null));

        var result = (await CalculateAsync([device], Day, Day.AddDays(1))).Single();

        result.Coverage.ShouldBe(FuelEconomyCoverage.Partial);
        result.FuelLitres.ShouldBe(10);
    }

    [Fact]
    public async Task A_device_named_twice_is_calculated_once_and_no_devices_give_no_results()
    {
        var device = await DeviceAsync();

        (await CalculateAsync([device, device], Day, Day.AddDays(1))).Count.ShouldBe(1);
        (await CalculateAsync([], Day, Day.AddDays(1))).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_period_without_a_whole_hour_or_longer_than_the_limit_is_refused()
    {
        var device = await DeviceAsync();

        await Should.ThrowAsync<ArgumentException>(() => CalculateAsync([device], Day.AddMinutes(10), Day.AddMinutes(50)));
        await Should.ThrowAsync<ArgumentException>(() => CalculateAsync([device], Day.AddHours(5), Day));
        await Should.ThrowAsync<ArgumentException>(() => CalculateAsync([device], Day, Day.AddDays(400)));
    }

    [Fact]
    public async Task The_limits_come_from_the_options()
    {
        var device = await DeviceAsync();
        using var scope = _fixture.Services.CreateScope();
        var service = new FuelEconomyService(scope.ServiceProvider.GetRequiredService<exs.Database.Commons.Interfaces.IRepository>(), Options.Create(new FuelEconomyOptions { MaxPeriodDays = 2 }));

        await Should.ThrowAsync<ArgumentException>(() => service.CalculateAsync([device], Day, Day.AddDays(3), CancellationToken.None));
        (await service.CalculateAsync([device], Day, Day.AddDays(2), CancellationToken.None)).Count.ShouldBe(1);
    }
}
