namespace Nagomi.Api.Domain;

public enum TransportRequestStatus
{
    Draft = 0,
    Active = 1,
    Completed = 2,
    Cancelled = 3
}

public enum JourneyDirection
{
    Outbound = 0,
    Return = 1
}

public enum LocationType
{
    PrivateAddress = 0,
    HealthcareFacility = 1
}

public enum MobilityType
{
    Autonomous = 0,
    Wheelchair = 1,
    Stretcher = 2
}

public enum JourneyStatus
{
    Scheduled = 0,
    Activated = 1,
    EnRouteToOrigin = 2,
    ArrivedAtOrigin = 3,
    PatientOnBoard = 4,
    EnRouteToDestination = 5,
    ArrivedAtDestination = 6,
    Completed = 7,
    Cancelled = 8
}

public enum ChangeSource
{
    Nagomi = 0,
    TransportProvider = 1
}

public enum CancellationReason
{
    NoLongerRequired = 0,
    PatientUnavailable = 1,
    MedicalReason = 2,
    SchedulingConflict = 3,
    ProviderUnavailable = 4,
    Other = 5
}

public enum CancellingParty
{
    Requester = 0,
    TransportProvider = 1
}
