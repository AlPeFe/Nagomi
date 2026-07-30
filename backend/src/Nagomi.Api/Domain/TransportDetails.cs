namespace Nagomi.Api.Domain;

public sealed class PatientDetails
{
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? DocumentNumber { get; private set; }
    public string? HealthCardNumber { get; private set; }
    public string? Phone { get; private set; }

    private PatientDetails()
    {
    }

    public PatientDetails(
        string? firstName = null,
        string? lastName = null,
        string? documentNumber = null,
        string? healthCardNumber = null,
        string? phone = null)
    {
        FirstName = Normalize(firstName);
        LastName = Normalize(lastName);
        DocumentNumber = Normalize(documentNumber);
        HealthCardNumber = Normalize(healthCardNumber);
        Phone = Normalize(phone);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class TransportReasonSnapshot
{
    public string Code { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    private TransportReasonSnapshot()
    {
    }

    public TransportReasonSnapshot(string code, string description)
    {
        Code = Required(code, nameof(code));
        Description = Required(description, nameof(description));
    }

    private static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{name} is required.");
        }

        return value.Trim();
    }
}

public sealed class LocationSnapshot
{
    public LocationType Type { get; private set; }
    public string? FacilityPublicId { get; private set; }
    public string? OfficialCode { get; private set; }
    public string? Name { get; private set; }
    public string? Street { get; private set; }
    public string? Number { get; private set; }
    public string? Block { get; private set; }
    public string? Staircase { get; private set; }
    public string? Floor { get; private set; }
    public string? Door { get; private set; }
    public string? AdditionalDetails { get; private set; }
    public string? PostalCode { get; private set; }
    public string? MunicipalityCode { get; private set; }
    public string? Municipality { get; private set; }
    public string? ProvinceCode { get; private set; }
    public string? Province { get; private set; }
    public string? Phone { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string? Observations { get; private set; }

    private LocationSnapshot()
    {
    }

    public LocationSnapshot(
        LocationType type,
        string? name = null,
        string? street = null,
        string? number = null,
        string? municipality = null,
        string? province = null,
        string? phone = null,
        decimal? latitude = null,
        decimal? longitude = null,
        string? observations = null,
        string? facilityPublicId = null,
        string? officialCode = null,
        string? block = null,
        string? staircase = null,
        string? floor = null,
        string? door = null,
        string? additionalDetails = null,
        string? postalCode = null,
        string? municipalityCode = null,
        string? provinceCode = null)
    {
        if (type == LocationType.HealthcareFacility && string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("A healthcare facility name is required.");
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new DomainValidationException("Coordinates are outside their valid ranges.");
        }

        Type = type;
        Name = Clean(name);
        Street = Clean(street);
        Number = Clean(number);
        Municipality = Clean(municipality);
        Province = Clean(province);
        Phone = Clean(phone);
        Latitude = latitude;
        Longitude = longitude;
        Observations = Clean(observations);
        FacilityPublicId = Clean(facilityPublicId);
        OfficialCode = Clean(officialCode);
        Block = Clean(block);
        Staircase = Clean(staircase);
        Floor = Clean(floor);
        Door = Clean(door);
        AdditionalDetails = Clean(additionalDetails);
        PostalCode = Clean(postalCode);
        MunicipalityCode = Clean(municipalityCode);
        ProvinceCode = Clean(provinceCode);
    }

    public LocationSnapshot Copy() => new(
        Type, Name, Street, Number, Municipality, Province, Phone, Latitude, Longitude,
        Observations, FacilityPublicId, OfficialCode, Block, Staircase, Floor, Door,
        AdditionalDetails, PostalCode, MunicipalityCode, ProvinceCode);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class TransportRequirements
{
    public MobilityType Mobility { get; private set; } = MobilityType.Autonomous;
    public bool RequiresOxygen { get; private set; }
    public decimal? OxygenConcentrationPercent { get; private set; }
    public decimal? OxygenFlowLitresPerMinute { get; private set; }
    public bool CompanionRequired { get; private set; }
    public bool MedicalStaffRequired { get; private set; }
    public bool IsolationRequired { get; private set; }
    public bool BariatricRequired { get; private set; }
    public bool StairsAssistanceRequired { get; private set; }

    public TransportRequirements()
    {
    }

    public TransportRequirements(
        MobilityType mobility = MobilityType.Autonomous,
        bool requiresOxygen = false,
        decimal? oxygenConcentrationPercent = null,
        decimal? oxygenFlowLitresPerMinute = null,
        bool companionRequired = false,
        bool medicalStaffRequired = false,
        bool isolationRequired = false,
        bool bariatricRequired = false,
        bool stairsAssistanceRequired = false)
    {
        if (!Enum.IsDefined(mobility))
        {
            throw new DomainValidationException("A valid mobility type is required.");
        }

        if (requiresOxygen && (oxygenConcentrationPercent is null or <= 0 || oxygenFlowLitresPerMinute is null or <= 0))
        {
            throw new DomainValidationException("Oxygen concentration and flow must be positive when oxygen is required.");
        }

        if (!requiresOxygen && (oxygenConcentrationPercent.HasValue || oxygenFlowLitresPerMinute.HasValue))
        {
            throw new DomainValidationException("Oxygen values require oxygen to be selected.");
        }

        Mobility = mobility;
        RequiresOxygen = requiresOxygen;
        OxygenConcentrationPercent = oxygenConcentrationPercent;
        OxygenFlowLitresPerMinute = oxygenFlowLitresPerMinute;
        CompanionRequired = companionRequired;
        MedicalStaffRequired = medicalStaffRequired;
        IsolationRequired = isolationRequired;
        BariatricRequired = bariatricRequired;
        StairsAssistanceRequired = stairsAssistanceRequired;
    }

    public TransportRequirements Copy() => new(
        Mobility, RequiresOxygen, OxygenConcentrationPercent, OxygenFlowLitresPerMinute,
        CompanionRequired, MedicalStaffRequired, IsolationRequired, BariatricRequired,
        StairsAssistanceRequired);
}
