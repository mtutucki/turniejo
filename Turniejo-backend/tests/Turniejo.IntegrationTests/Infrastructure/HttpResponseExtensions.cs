using System.Text.Json;

namespace Turniejo.IntegrationTests.Infrastructure;

// ASP.NET Core domyślnie serializuje na camelCase — helper czyta odpowiedzi jako JsonElement
// zamiast wiązać testy do konkretnych record-typów z Application warstw poszczególnych modułów.
public static class HttpResponseExtensions
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    public static Guid GetGuid(this JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetGuid();

    public static Guid? GetGuidOrNull(this JsonElement element, string propertyName)
    {
        var property = element.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : property.GetGuid();
    }

    public static int GetInt(this JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetInt32();

    public static string GetString(this JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetString()!;
}
