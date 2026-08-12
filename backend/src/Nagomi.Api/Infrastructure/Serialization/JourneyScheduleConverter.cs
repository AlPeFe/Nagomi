using System.Text.Json;
using System.Text.Json.Serialization;
using Nagomi.Api.Domain;

namespace Nagomi.Api.Infrastructure.Serialization;

/// <summary>
/// Deserializes a JourneySchedule tolerantly: if the outbound scheduledStartAt is missing or an
/// empty string (the client form leaves "Inicio previsto" blank), derive it from the appointment
/// (one hour earlier) — mirroring JourneySchedule.Outbound(). A non-nullable DateTimeOffset property
/// cannot bind an empty string, which previously surfaced as a 400/500 on submit.
/// </summary>
public sealed class JourneyScheduleConverter : JsonConverter<JourneySchedule>
{
    public override JourneySchedule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var appointment = GetNullableDateTimeOffset(root, "appointmentAt");
        var start = GetNullableDateTimeOffset(root, "scheduledStartAt");
        var pickup = GetNullableDateTimeOffset(root, "scheduledPickupAt");
        var pending = root.TryGetProperty("pickupTimePending", out var p) && p.ValueKind == JsonValueKind.True;

        if (pending && !pickup.HasValue)
        {
            // A pending return pickup uses the 23:59 placeholder of the same day as the appointment.
            var baseDate = appointment?.Date ?? start?.Date;
            if (baseDate is null) throw new JsonException("A pending return pickup needs a date.");
            pickup = new DateTimeOffset(baseDate.Value.Year, baseDate.Value.Month, baseDate.Value.Day, 23, 59, 0, TimeSpan.Zero);
        }

        if (!start.HasValue)
        {
            start = appointment?.AddHours(-1);
            if (start is null) throw new JsonException("An outbound scheduled start time is required.");
        }

        return new JourneySchedule(appointment, start.Value, pickup, pending);
    }

    public override void Write(Utf8JsonWriter writer, JourneySchedule value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("appointmentAt");
        if (value.AppointmentAt.HasValue) writer.WriteStringValue(value.AppointmentAt.Value);
        else writer.WriteNullValue();
        writer.WritePropertyName("scheduledStartAt");
        writer.WriteStringValue(value.ScheduledStartAt);
        writer.WritePropertyName("scheduledPickupAt");
        if (value.ScheduledPickupAt.HasValue) writer.WriteStringValue(value.ScheduledPickupAt.Value);
        else writer.WriteNullValue();
        writer.WriteBoolean("pickupTimePending", value.PickupTimePending);
        writer.WriteEndObject();
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTimeOffset.TryParse(s, out var parsed)) return parsed;
        }
        if (el.TryGetDateTimeOffset(out var dto)) return dto;
        throw new JsonException($"'{name}' is not a valid date/time.");
    }
}
