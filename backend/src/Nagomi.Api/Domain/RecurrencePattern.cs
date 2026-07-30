namespace Nagomi.Api.Domain;

public sealed class WeekdaySchedule
{
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly OutboundAppointmentTime { get; private set; }
    public TimeOnly? OutboundStartTime { get; private set; }
    public TimeOnly? OutboundPickupTime { get; private set; }
    public TimeOnly? ReturnPickupTime { get; private set; }
    public bool ReturnPickupNextDay { get; private set; }
    public bool ReturnPickupTimePending { get; private set; }

    private WeekdaySchedule()
    {
    }

    public WeekdaySchedule(
        DayOfWeek dayOfWeek,
        TimeOnly outboundAppointmentTime,
        TimeOnly? returnPickupTime = null,
        TimeOnly? outboundStartTime = null,
        TimeOnly? outboundPickupTime = null,
        bool returnPickupNextDay = false,
        bool returnPickupTimePending = false)
    {
        if (returnPickupTimePending && returnPickupTime != new TimeOnly(23, 59))
        {
            throw new DomainValidationException("A pending return pickup must use the 23:59 placeholder.");
        }

        if (returnPickupTime is null && (returnPickupNextDay || returnPickupTimePending))
        {
            throw new DomainValidationException("Return options require a return pickup time.");
        }

        DayOfWeek = dayOfWeek;
        OutboundAppointmentTime = outboundAppointmentTime;
        ReturnPickupTime = returnPickupTime;
        OutboundStartTime = outboundStartTime;
        OutboundPickupTime = outboundPickupTime;
        ReturnPickupNextDay = returnPickupNextDay;
        ReturnPickupTimePending = returnPickupTimePending;
    }
}

public sealed class RecurrencePattern
{
    private readonly List<WeekdaySchedule> _weekdaySchedules = [];

    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public TimeSpan UtcOffset { get; private set; }
    public IReadOnlyCollection<WeekdaySchedule> WeekdaySchedules => _weekdaySchedules.AsReadOnly();

    private RecurrencePattern()
    {
    }

    public RecurrencePattern(
        DateOnly startDate,
        DateOnly endDate,
        IEnumerable<WeekdaySchedule> weekdaySchedules,
        TimeSpan? utcOffset = null)
    {
        if (endDate < startDate)
        {
            throw new DomainValidationException("The recurrence end date cannot precede its start date.");
        }

        if (endDate > startDate.AddMonths(6))
        {
            throw new DomainValidationException("A recurrence interval cannot exceed six months.");
        }

        var schedules = weekdaySchedules?.ToList() ?? [];
        if (schedules.Count == 0)
        {
            throw new DomainValidationException("At least one weekday must be selected.");
        }

        if (schedules.Select(x => x.DayOfWeek).Distinct().Count() != schedules.Count)
        {
            throw new DomainValidationException("Each selected weekday must have one schedule.");
        }

        var offset = utcOffset ?? TimeSpan.Zero;
        if (offset < TimeSpan.FromHours(-14) || offset > TimeSpan.FromHours(14))
        {
            throw new DomainValidationException("The recurrence UTC offset is invalid.");
        }

        StartDate = startDate;
        EndDate = endDate;
        UtcOffset = offset;
        _weekdaySchedules.AddRange(schedules);
    }

    internal IEnumerable<(DateOnly Date, WeekdaySchedule Schedule)> Occurrences()
    {
        var schedules = _weekdaySchedules.ToDictionary(x => x.DayOfWeek);
        for (var date = StartDate; date <= EndDate; date = date.AddDays(1))
        {
            if (schedules.TryGetValue(date.DayOfWeek, out var schedule))
            {
                yield return (date, schedule);
            }
        }
    }

    internal DateTimeOffset At(DateOnly date, TimeOnly time) =>
        new(date.ToDateTime(time), UtcOffset);
}
