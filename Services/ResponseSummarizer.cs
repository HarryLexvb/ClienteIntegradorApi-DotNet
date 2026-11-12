using System.Text.Json;

namespace ClienteIntegradorApi.Services;

/// <summary>
/// Resumen de la respuesta: cantidad de elementos (null si la respuesta no es una colección)
/// y líneas de texto listas para imprimir.
/// </summary>
public sealed record JsonSummary(int? ItemCount, IReadOnlyList<string> Lines);

/// <summary>
/// Resume el JSON con JsonElement (sin DTOs) porque el esquema completo no está garantizado:
/// solo se muestran los campos que realmente existan en cada elemento.
/// </summary>
public static class ResponseSummarizer
{
    private const int MaxItemsShown = 5;
    private const int MaxFallbackChars = 1000;

    // Campos de equipo que se muestran si están presentes (no se inventa ninguno).
    private static readonly string[] FieldsShown = ["id", "serie", "description", "model", "status"];

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        // Evita que "Balança" se imprima como "Balança" en la consola.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Lanza <see cref="JsonException"/> si el cuerpo está vacío o no es JSON válido.</summary>
    public static JsonSummary Summarize(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new JsonException("El cuerpo de la respuesta está vacío.");

        // JsonDocument.Parse lanza JsonException si el JSON es inválido; Program la traduce a un error legible.
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var collection = FindCollection(root);
        if (collection is null)
        {
            // Objeto o valor sin colección: se muestra recortado para no imprimir payloads enormes.
            var text = JsonSerializer.Serialize(root, Indented);
            if (text.Length > MaxFallbackChars) text = text[..MaxFallbackChars] + "... (recortado)";
            return new JsonSummary(null, [text]);
        }

        var items = collection.Value.EnumerateArray().ToList();
        var lines = items.Take(MaxItemsShown).Select((item, i) => $"  {i + 1}. {Describe(item)}").ToList();
        if (items.Count > MaxItemsShown) lines.Add($"  ... y {items.Count - MaxItemsShown} más");
        return new JsonSummary(items.Count, lines);
    }

    // Busca la colección: raíz array, o un array dentro de la raíz ("data", "items" y, si no, el primero).
    private static JsonElement? FindCollection(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind != JsonValueKind.Object) return null;

        foreach (var name in new[] { "data", "items" })
            if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array) return p;

        foreach (var prop in root.EnumerateObject())
            if (prop.Value.ValueKind == JsonValueKind.Array) return prop.Value;
        return null;
    }

    // Una línea por elemento con los campos conocidos; ignora ausentes y nulos.
    private static string Describe(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return item.ToString();

        var parts = FieldsShown
            .Where(f => item.TryGetProperty(f, out var v) && v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            .Select(f => $"{f}={item.GetProperty(f)}"); // JsonElement.ToString() devuelve el texto sin comillas
        var text = string.Join(" | ", parts);
        return text.Length > 0 ? text : "(sin campos conocidos)";
    }
}
