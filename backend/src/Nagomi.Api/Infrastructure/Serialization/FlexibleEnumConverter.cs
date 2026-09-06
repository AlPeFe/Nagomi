using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nagomi.Api.Infrastructure.Serialization;

/// <summary>
/// Accepts enums on INPUT as either their numeric value or their case-insensitive name
/// (the web frontend sends names like "HealthcareFacility"; the API/tests send numbers).
/// Writes stay numeric so existing consumers and tests reading GetInt32 keep working.
/// </summary>
public sealed class FlexibleEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(FlexibleEnumConverter<>).MakeGenericType(typeToConvert),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: null,
            culture: null)!;

    private sealed class FlexibleEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return (TEnum)Enum.ToObject(typeToConvert, reader.GetInt32());
                case JsonTokenType.String:
                    var text = reader.GetString();
                    if (string.IsNullOrWhiteSpace(text))
                        return default;
                    if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
                        return parsed;
                    throw new JsonException($"Value '{text}' is not a valid {typeof(TEnum).Name}.");
                case JsonTokenType.Null:
                    return default;
                default:
                    throw new JsonException($"Unexpected token {reader.TokenType} for enum {typeof(TEnum).Name}.");
            }
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(Convert.ToInt32(value));
    }
}
