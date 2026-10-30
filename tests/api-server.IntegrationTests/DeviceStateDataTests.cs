using System.Text.Json.Nodes;
using api_server.Controllers.MapData.Services;
using db_model.Map;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>The typed contract of gps_device_state_data.Data: names, dates, and what an old or odd row does to it.</summary>
public sealed class DeviceStateDataTests
{
    private static readonly DateTime T1 = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T0 = T1.AddHours(-2);

    [Fact]
    public void ToJson_WritesShortNames_AndOmitsWhatIsNotThere()
    {
        var data = new DeviceStateData
        {
            Ignition = new StateValue<double>(1, T1, T0),
            Charging = new StateValue<bool>(true, T1),
        };

        var json = JsonNode.Parse(data.ToJson())!.AsObject();

        json.Select(p => p.Key).ShouldBe(new[] { "ignition", "charging" }, ignoreOrder: true);
        json["ignition"]!["v"]!.GetValue<double>().ShouldBe(1);
        json["ignition"]!["t"]!.GetValue<DateTime>().ShouldBe(T1);
        json["ignition"]!["c"]!.GetValue<DateTime>().ShouldBe(T0);
        json["charging"]!.AsObject().ContainsKey("c").ShouldBeFalse();
    }

    [Fact]
    public void RoundTrip_KeepsEveryParameterWithItsOwnDates()
    {
        var data = new DeviceStateData
        {
            Ignition = new StateValue<double>(0, T1, T0),
            BatteryVoltage = new StateValue<double>(12.6, T1),
            Battery = new StateValue<float>(0.82f, T0),
            Charging = new StateValue<bool>(false, T0, T0),
            Activity = new StateValue<short>(3, T1),
            ActivityStatus = new StateValue<short>(GpsDeviceActivityStatus.Parked, T1, T0),
            Communication = new StateValue<bool>(true, T1, T0),
            PrivateMode = new StateValue<bool>(true, T1, T0),
            EngineFault = new StateValue<bool>(true, T1, T0),
            BatteryLow = new StateValue<bool>(true, T1, T0),
            BatteryFault = new StateValue<bool>(false, T1, T0),
            Odometer = new StateValue<double>(123456789, T1, T0),
            EngineHours = new StateValue<double>(7200, T1),
            FuelTotal = new StateValue<double>(5500.5, T1),
            FuelIdleTotal = new StateValue<double>(310.25, T1),
            SeatBelt = new StateValue<double>(1, T1, T0),
            OdometerAdjustment = new StateValue<double>(-1500, T1),
            EngineHoursAdjustment = new StateValue<double>(3600, T1),
            FuelLevel = new StateValue<double>(24.31, T1, T0),
        };

        var back = DeviceStateData.Parse(data.ToJson());

        back.Ignition!.Value.ShouldBe(0);
        back.Ignition.At.ShouldBe(T1);
        back.Ignition.ChangedAt.ShouldBe(T0);
        back.IsIgnitionOn.ShouldBe(false);
        back.BatteryVoltage!.Value.ShouldBe(12.6);
        back.Battery!.Value.ShouldBe(0.82f);
        back.Battery.At.ShouldBe(T0);
        back.Charging!.Value.ShouldBeFalse();
        back.Activity!.Value.ShouldBe((short)3);
        back.ActivityStatus!.Value.ShouldBe((short)GpsDeviceActivityStatus.Parked);
        back.ActivityStatus.Since.ShouldBe(T0);
        back.Communication!.Value.ShouldBeTrue();
        back.PrivateMode!.Value.ShouldBeTrue();
        back.PrivateMode.Since.ShouldBe(T0);
        back.EngineFault!.Value.ShouldBeTrue();
        back.EngineFault.Since.ShouldBe(T0);
        back.BatteryLow!.Value.ShouldBeTrue();
        back.BatteryFault!.Value.ShouldBeFalse();
        back.BatteryFault.Since.ShouldBe(T0);
        back.Odometer!.Value.ShouldBe(123456789);
        back.Odometer.Since.ShouldBe(T0);
        back.EngineHours!.Value.ShouldBe(7200);
        back.FuelTotal!.Value.ShouldBe(5500.5);
        back.FuelIdleTotal!.Value.ShouldBe(310.25);
        back.SeatBelt!.Value.ShouldBe(1);
        back.OdometerAdjustment!.Value.ShouldBe(-1500);
        back.EngineHoursAdjustment!.Value.ShouldBe(3600);
        back.FuelLevel!.Value.ShouldBe(24.31);
        back.FuelLevel.Since.ShouldBe(T0);
        back.Extra.ShouldBeNull();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(-1.0, null)]   // the provider says unavailable
    [InlineData(0.0, false)]   // buckled
    [InlineData(1.0, true)]    // unbuckled
    [InlineData(2.0, true)]
    public void SeatBelt_IsUnbuckledOnlyAboveZero_AndUnknownWhenMissingOrUnavailable(double? value, bool? expected)
    {
        var data = new DeviceStateData { SeatBelt = value == null ? null : new StateValue<double>(value.Value, T1) };

        data.IsSeatBeltUnbuckled.ShouldBe(expected);
    }

    [Fact]
    public void AnEntryWithoutChangedAt_FallsBackToItsReportedTime()
    {
        var data = DeviceStateData.Parse("""{"ignition":{"v":1,"t":"2026-10-29T12:00:00Z"}}""");

        data.Ignition!.ChangedAt.ShouldBeNull();
        data.Ignition.Since.ShouldBe(T1);
        data.IsIgnitionOn.ShouldBe(true);
    }

    [Fact]
    public void WhatTheFeederWritesToday_ReadsBack()
    {
        // GeotabFeedStore.toJson: microsecond UTC time, numeric value.
        var data = DeviceStateData.Parse("""{"ignition":{"v":1,"t":"2026-10-29T12:00:00.123456Z"}}""");

        data.Ignition!.Value.ShouldBe(1);
        data.Ignition.At.ShouldBe(T1.AddTicks(1234560));
        data.Ignition.At.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void WhatThePhonePathWritesToday_ReadsBackThroughTheSameKeys()
    {
        PhoneMapState.BatteryKey.ShouldBe(StateKeys.Battery);
        PhoneMapState.ChargingKey.ShouldBe(StateKeys.Charging);
        PhoneMapState.ActivityKey.ShouldBe(StateKeys.Activity);

        var stored = new JsonObject
        {
            [PhoneMapState.BatteryKey] = new JsonObject { ["v"] = 0.5f, ["t"] = T1.ToString("O") },
            [PhoneMapState.ChargingKey] = new JsonObject { ["v"] = true, ["t"] = T1.ToString("O") },
            [PhoneMapState.ActivityKey] = new JsonObject { ["v"] = (short)4, ["t"] = T1.ToString("O") },
        }.ToJsonString();

        var data = DeviceStateData.Parse(stored);

        data.Battery!.Value.ShouldBe(0.5f);
        data.Charging!.Value.ShouldBeTrue();
        data.Activity!.Value.ShouldBe((short)4);
        var (activity, battery) = PhoneMapState.ReadValues(stored);
        activity.ShouldBe(data.Activity.Value);
        battery.ShouldBe(data.Battery.Value);
    }

    [Fact]
    public void AKeyThisVersionDoesNotKnow_IsKeptAndWrittenBack()
    {
        var data = DeviceStateData.Parse("""{"ignition":{"v":1,"t":"2026-10-29T12:00:00Z"},"fuel":{"v":40,"t":"2026-10-29T12:00:00Z"}}""");

        data.Extra!.Keys.ShouldBe(new[] { "fuel" });
        var json = JsonNode.Parse(data.ToJson())!.AsObject();
        json["fuel"]!["v"]!.GetValue<int>().ShouldBe(40);
        json["ignition"]!["v"]!.GetValue<double>().ShouldBe(1);
    }

    [Fact]
    public void AnEntryOfTheWrongShape_DoesNotLoseTheOthers_AndIsKept()
    {
        // charging is a boolean; a number there (or a bare value) must not throw away the rest.
        var data = DeviceStateData.Parse("""{"charging":{"v":1,"t":"2026-10-29T12:00:00Z"},"ignition":5,"battery":{"v":0.9,"t":"2026-10-29T12:00:00Z"}}""");

        data.Battery!.Value.ShouldBe(0.9f);
        data.Charging.ShouldBeNull();
        data.Ignition.ShouldBeNull();
        data.Extra!.Keys.ShouldBe(new[] { "charging", "ignition" }, ignoreOrder: true);
        JsonNode.Parse(data.ToJson())!.AsObject().Select(p => p.Key).ShouldBe(new[] { "charging", "ignition", "battery" }, ignoreOrder: true);
    }

    [Fact]
    public void ANewValueReplacesAKeptOddEntryOfTheSameKey()
    {
        var data = DeviceStateData.Parse("""{"charging":{"v":1,"t":"2026-10-29T12:00:00Z"}}""");
        data.Charging = new StateValue<bool>(true, T1);

        var json = JsonNode.Parse(data.ToJson())!.AsObject();

        json["charging"]!["v"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void Merge_FirstReport_StartsTheEntry()
    {
        var merged = StateValue<short>.Merge(null, 3, T1);

        merged.Value.ShouldBe((short)3);
        merged.At.ShouldBe(T1);
        merged.ChangedAt.ShouldBe(T1);
    }

    [Fact]
    public void Merge_SameValueLater_MovesReportedTimeButNotChangedTime()
    {
        var stored = new StateValue<short>(3, T0, T0);

        var merged = StateValue<short>.Merge(stored, 3, T1);

        merged.At.ShouldBe(T1);
        merged.ChangedAt.ShouldBe(T0);
    }

    [Fact]
    public void Merge_DifferentValueLater_MovesBothTimes()
    {
        var merged = StateValue<short>.Merge(new StateValue<short>(3, T0, T0), 1, T1);

        merged.Value.ShouldBe((short)1);
        merged.ChangedAt.ShouldBe(T1);
    }

    [Fact]
    public void Merge_AnEntryWithoutChangedAt_KeepsItsReportedTimeAsTheSince()
    {
        var merged = StateValue<short>.Merge(new StateValue<short>(3, T0), 3, T1);

        merged.ChangedAt.ShouldBe(T0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merge_ALateOrRepeatedReport_ChangesNothing(bool sameTime)
    {
        var stored = new StateValue<short>(3, T1, T1);

        var merged = StateValue<short>.Merge(stored, 1, sameTime ? T1 : T0);

        merged.ShouldBeSameAs(stored);
    }

    [Fact]
    public void Set_ReplacesTheStoredEntryEvenWithAnOlderTime_AndDatesTheChange()
    {
        var stored = new StateValue<short>(4, T1, T1);

        var set = StateValue<short>.Set(stored, 1, T0);

        set.Value.ShouldBe((short)1);
        set.At.ShouldBe(T0);
        set.ChangedAt.ShouldBe(T0);
    }

    [Fact]
    public void Set_WithTheSameValue_KeepsTheSinceDate()
    {
        var set = StateValue<short>.Set(new StateValue<short>(3, T0, T0), 3, T1);

        set.At.ShouldBe(T1);
        set.ChangedAt.ShouldBe(T0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    public void BlankOrBrokenJson_GivesAnEmptyObject(string? json)
    {
        var data = DeviceStateData.Parse(json);

        data.Ignition.ShouldBeNull();
        data.Extra.ShouldBeNull();
        data.ToJson().ShouldBe("{}");
    }
}
