using System.Globalization;
using System.Text.Json;

namespace Nagomi.Api.Features.Audit;

public interface IAuditDiffService
{
    IReadOnlyList<AuditChange> Compare(
        IReadOnlyDictionary<string, object?> previousSnapshot,
        IReadOnlyDictionary<string, object?> currentSnapshot);
}

public sealed class AuditDiffService : IAuditDiffService
{
    private static readonly HashSet<string> SensitiveIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "DocumentNumber",
        "NationalIdentifier",
        "NationalId",
        "IdentityDocumentNumber",
        "Dni",
        "HealthCardNumber",
        "HealthCardIdentifier",
        "SocialSecurityNumber"
    };

    public IReadOnlyList<AuditChange> Compare(
        IReadOnlyDictionary<string, object?> previousSnapshot,
        IReadOnlyDictionary<string, object?> currentSnapshot)
    {
        ArgumentNullException.ThrowIfNull(previousSnapshot);
        ArgumentNullException.ThrowIfNull(currentSnapshot);

        var previousFields = new Dictionary<string, object?>(previousSnapshot, StringComparer.OrdinalIgnoreCase);
        var currentFields = new Dictionary<string, object?>(currentSnapshot, StringComparer.OrdinalIgnoreCase);
        var fieldNames = previousFields.Keys
            .Concat(currentFields.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var changes = new List<AuditChange>();

        foreach (var fieldName in fieldNames)
        {
            previousFields.TryGetValue(fieldName, out var previousValue);
            currentFields.TryGetValue(fieldName, out var currentValue);

            if (ValuesEqual(previousValue, currentValue))
            {
                continue;
            }

            var leafName = fieldName.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? fieldName;
            if (SensitiveIdentifiers.Contains(leafName))
            {
                changes.Add(new AuditChange(
                    Guid.NewGuid(), fieldName, null, null, AuditValueProtection.SensitiveIdentifier));
                continue;
            }

            if (IsPhoneField(leafName))
            {
                changes.Add(new AuditChange(
                    Guid.NewGuid(),
                    fieldName,
                    MaskPhone(FormatValue(previousValue)),
                    MaskPhone(FormatValue(currentValue)),
                    AuditValueProtection.MaskedPhone));
                continue;
            }

            changes.Add(new AuditChange(
                Guid.NewGuid(),
                fieldName,
                FormatValue(previousValue),
                FormatValue(currentValue),
                AuditValueProtection.None));
        }

        return changes;
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return JsonSerializer.Serialize(left, left.GetType()) ==
               JsonSerializer.Serialize(right, right.GetType());
    }

    private static bool IsPhoneField(string fieldName) =>
        fieldName.Contains("phone", StringComparison.OrdinalIgnoreCase) ||
        fieldName.Contains("telephone", StringComparison.OrdinalIgnoreCase) ||
        fieldName.Contains("telefono", StringComparison.OrdinalIgnoreCase) ||
        fieldName.Contains("movil", StringComparison.OrdinalIgnoreCase);

    private static string? FormatValue(object? value) => value switch
    {
        null => null,
        string text => text,
        bool boolean => boolean ? "True" : "False",
        DateTimeOffset timestamp => timestamp.ToString("O", CultureInfo.InvariantCulture),
        DateTime timestamp => timestamp.ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => JsonSerializer.Serialize(value, value.GetType())
    };

    private static string? MaskPhone(string? phone)
    {
        if (phone is null)
        {
            return null;
        }

        var digitCount = phone.Count(char.IsDigit);
        var digitsToKeep = digitCount > 3 ? 3 : 0;
        var digitsSeen = 0;
        var result = phone.ToCharArray();

        for (var index = result.Length - 1; index >= 0; index--)
        {
            if (!char.IsDigit(result[index]))
            {
                continue;
            }

            digitsSeen++;
            if (digitsSeen > digitsToKeep)
            {
                result[index] = '*';
            }
        }

        return new string(result);
    }
}
