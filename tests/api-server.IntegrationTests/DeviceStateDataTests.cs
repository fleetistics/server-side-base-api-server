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
        back.Extra.ShouldBeNull();
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
