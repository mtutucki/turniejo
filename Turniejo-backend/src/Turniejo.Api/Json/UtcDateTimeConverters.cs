using System.Text.Json;
using System.Text.Json.Serialization;

namespace Turniejo.Api.Json;

// Kolumny dat to timestamptz, a Npgsql zapisuje do nich wyłącznie wartości UTC — DateTime z Kind=Unspecified
// ("2014-05-01") albo DateTimeOffset z niezerowym offsetem kończyły się 500 dopiero przy SaveChanges.
// Normalizujemy więc każdą przychodzącą datę do UTC już na wejściu: data bez strefy jest traktowana jako UTC,
// data ze strefą jest przeliczana na ten sam moment w UTC.
internal static class UtcDateTimeReader
{
    public static DateTime Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTime(out var value))
            throw new JsonException("The value is not a valid ISO 8601 date.");

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}

public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        UtcDateTimeReader.Read(ref reader);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(UtcDateTimeReader.Read(ref reader));

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
