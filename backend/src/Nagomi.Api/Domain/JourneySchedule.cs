using System.Text.Json.Serialization;

namespace Nagomi.Api.Domain;

public sealed class JourneySchedule
{
    public DateTimeOffset? AppointmentAt { get; private set; }
    public DateTimeOffset ScheduledStartAt { get; private set; }
    public DateTimeOffset? ScheduledPickupAt { get; private set; }
    public bool PickupTimePending { get; private set; }

    private JourneySchedule()
    {
    }

    [JsonConstructor]
    public JourneySchedule(
        DateTimeOffset? appointmentAt,
        DateTimeOffset scheduledStartAt,
        DateTimeOffset? scheduledPickupAt,
        bool pickupTimePending)
    {
        if (pickupTimePending && (!scheduledPickupAt.HasValue ||
            scheduledPickupAt.Value.Hour != 23 || scheduledPickupAt.Value.Minute != 59))
            throw new DomainValidationException("A pending return pickup must use the 23:59 placeholder.");
        if (appointmentAt.HasValue && scheduledStartAt > appointmentAt.Value)
            throw new DomainValidationException("The outbound start cannot be after the appointment.");
        AppointmentAt = appointmentAt;
        ScheduledStartAt = scheduledStartAt;
        ScheduledPickupAt = scheduledPickupAt;
        PickupTimePending = pickupTimePending;
    }

    public static JourneySchedule Outbound(
        DateTimeOffset? appointmentAt,
        bool destinationIsHealthcareFacility,
        DateTimeOffset? scheduledStartAt = null,
        DateTimeOffset? scheduledPickupAt = null)
    {
        if (destinationIsHealthcareFacility && appointmentAt is null)
        {
            throw new DomainValidationException("An appointment time is required for an outbound journey to a healthcare facility.");
        }

        if (appointmentAt is null && scheduledStartAt is null)
        {
            throw new DomainValidationException("An outbound scheduled start time is required.");
        }

        var start = scheduledStartAt ?? appointmentAt!.Value.AddHours(-1);
        if (appointmentAt.HasValue && start > appointmentAt.Value)
        {
            throw new DomainValidationException("The outbound start cannot be after the appointment.");
        }

        return new JourneySchedule(appointmentAt, start, scheduledPickupAt, false);
    }

    public static JourneySchedule Return(DateTimeOffset pickupAt, bool pickupTimePending = false)
    {
        if (pickupTimePending && (pickupAt.Hour != 23 || pickupAt.Minute != 59))
        {
            throw new DomainValidationException("A pending return pickup must use the 23:59 placeholder.");
        }

        return new JourneySchedule(null, pickupAt, pickupAt, pickupTimePending);
    }

    public JourneySchedule Copy() => new(AppointmentAt, ScheduledStartAt, ScheduledPickupAt, PickupTimePending);

    public static void ValidateRoundTrip(JourneySchedule outbound, JourneySchedule returnSchedule)
    {
        var returnPickup = returnSchedule.ScheduledPickupAt
            ?? throw new DomainValidationException("A return pickup time is required.");
        var duration = returnPickup - outbound.ScheduledStartAt;

        if (duration < TimeSpan.Zero)
        {
            throw new DomainValidationException("The return pickup cannot precede the outbound start.");
        }

        if (duration > TimeSpan.FromHours(24))
        {
            throw new DomainValidationException("A round trip cannot exceed 24 hours.");
        }
    }
}
