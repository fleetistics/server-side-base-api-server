using exs.Commons.Utils;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace api_server.Controllers.MapData.Dto
{
	// One location item of a mobile location report, as produced by the device's background-geolocation SDK.
	// Mirrors the SDK's `Location` type and is bound by the standard request parser (snake_case names are explicit,
	// numbers may also arrive as JSON strings). Every member is optional on the wire, so the service validates what it needs.
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public sealed class MobileLocation
	{
		[JsonPropertyName("timestamp")]
		[JsonConverter(typeof(UnixOrIsoDateTimeConverter))]
		public DateTime? Timestamp { get; set; }

		//[JsonPropertyName("recorded_at")]
		//[JsonConverter(typeof(UnixOrIsoDateTimeConverter))]
		//public DateTime? RecordedAt { get; set; }

		//[JsonPropertyName("age")] public double? Age { get; set; }
		[JsonPropertyName("odometer")] public double? Odometer { get; set; }
		[JsonPropertyName("odometer_error")] public double? OdometerError { get; set; }
		//[JsonPropertyName("is_moving")] public bool? IsMoving { get; set; }
		//[JsonPropertyName("uuid")] public string? Uuid { get; set; }
		//[JsonPropertyName("event")] public string? Event { get; set; }
		//[JsonPropertyName("mock")] public bool? Mock { get; set; }
		//[JsonPropertyName("sample")] public bool? Sample { get; set; }
		[JsonPropertyName("coords")] public MobileLocationCoords? Coords { get; set; }
		[JsonPropertyName("battery")] public MobileLocationBattery? Battery { get; set; }
		//[JsonPropertyName("extras")] public JsonElement? Extras { get; set; }
		//[JsonPropertyName("geofence")] public MobileLocationGeofence? Geofence { get; set; }
		[JsonPropertyName("activity")] public MobileLocationActivity? Activity { get; set; }
		[JsonPropertyName("provider")] public MobileLocationProvider? Provider { get; set; }
	}

	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public sealed class MobileLocationCoords
	{
		[JsonPropertyName("floor")] public int? Floor { get; set; }
		[JsonPropertyName("latitude")] public double? Latitude { get; set; }
		[JsonPropertyName("longitude")] public double? Longitude { get; set; }
		[JsonPropertyName("accuracy")] public float? Accuracy { get; set; }
		[JsonPropertyName("altitude")] public float? Altitude { get; set; }
		[JsonPropertyName("ellipsoidal_altitude")] public float? EllipsoidalAltitude { get; set; }
		[JsonPropertyName("altitude_accuracy")] public float? AltitudeAccuracy { get; set; }
		[JsonPropertyName("heading")] public float? Heading { get; set; }
		[JsonPropertyName("heading_accuracy")] public float? HeadingAccuracy { get; set; }
		[JsonPropertyName("speed")] public float? Speed { get; set; }
		[JsonPropertyName("speed_accuracy")] public float? SpeedAccuracy { get; set; }
	}

	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public sealed class MobileLocationBattery
	{
		[JsonPropertyName("is_charging")] public bool? IsCharging { get; set; }

		// The previous parser read "isCharging"; accept it too until every client is known to send "is_charging".
		[JsonPropertyName("isCharging")] public bool? IsChargingCamel { get; set; }

		[JsonPropertyName("level")] public float? Level { get; set; }
	}

	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public sealed class MobileLocationActivity
	{
		// "still" | "walking" | "on_foot" | "running" | "on_bicycle" | "in_vehicle" | "unknown"
		[JsonPropertyName("type")] public string? Type { get; set; }
		[JsonPropertyName("confidence")] public int? Confidence { get; set; }
	}

	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public sealed class MobileLocationGeofence
	{
		[JsonPropertyName("identifier")] public string? Identifier { get; set; }

		// "ENTER" | "EXIT" | "DWELL"
		[JsonPropertyName("action")] public string? Action { get; set; }

		[JsonPropertyName("timestamp")]
		[JsonConverter(typeof(UnixOrIsoDateTimeConverter))]
		public DateTime? Timestamp { get; set; }

		[JsonPropertyName("extras")] public JsonElement? Extras { get; set; }
	}

	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
	public sealed class MobileLocationProvider
	{
		[JsonPropertyName("enabled")] public bool? Enabled { get; set; }
		[JsonPropertyName("status")] public int? Status { get; set; }
		[JsonPropertyName("network")] public bool? Network { get; set; }
		[JsonPropertyName("gps")] public bool? Gps { get; set; }
		[JsonPropertyName("accuracyAuthorization")] public int? AccuracyAuthorization { get; set; }
	}

	// The SDK sends timestamps as an ISO 8601 string or as a unix time number; always yields a UTC DateTime.
	public sealed class UnixOrIsoDateTimeConverter : JsonConverter<DateTime>
	{
		public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Number:
					if (reader.TryGetInt64(out var unix)) return DateTimeHelper.UnixTimeToUtcDateTime(unix);
					return DateTimeHelper.UnixTimeToUtcDateTime((long)reader.GetDouble());

				case JsonTokenType.String:
					if (reader.TryGetDateTime(out var dt))
					{
						return dt.Kind switch
						{
							DateTimeKind.Utc => dt,
							DateTimeKind.Local => dt.ToUniversalTime(),
							_ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
						};
					}
					var text = reader.GetString();
					if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixText))
						return DateTimeHelper.UnixTimeToUtcDateTime(unixText);
					if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
						return parsed;
					break;
			}
			throw new JsonException($"Unsupported timestamp value: {reader.TokenType}");
		}

		public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value);
	}
}
