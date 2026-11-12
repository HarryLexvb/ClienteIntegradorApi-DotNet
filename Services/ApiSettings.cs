using System.Text.Json;

namespace ClienteIntegradorApi.Services;

/// <summary>Configuración de la API leída desde appsettings.json (sección "Api").</summary>
public sealed record ApiSettings(Uri BaseUrl, string Endpoint, string BearerToken, int TimeoutSeconds)
{
    /// <summary>
    /// Lee y valida la configuración. Se usa System.Text.Json directamente para no añadir
    /// paquetes de configuración. Lanza <see cref="InvalidOperationException"/> si falta o es inválido algo.
    /// </summary>
    public static ApiSettings Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"No se encontró el archivo de configuración '{path}'.");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("Api", out var api))
            throw new InvalidOperationException("Falta la sección 'Api' en appsettings.json.");

        var baseUrl = Text(api, "BaseUrl");
        // La barra final garantiza que Uri combine BaseUrl + Endpoint sin perder "/api/v3/".
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("'Api:BaseUrl' debe ser una URL absoluta HTTPS.");

        // Se exige HTTPS arriba para que el token Bearer nunca viaje en claro.
        var timeout = api.TryGetProperty("TimeoutSeconds", out var t) && t.TryGetInt32(out var s) ? s : 0;
        if (timeout is < 1 or > 300)
            throw new InvalidOperationException("'Api:TimeoutSeconds' debe estar entre 1 y 300.");

        return new ApiSettings(uri, Text(api, "Endpoint").TrimStart('/'), Text(api, "BearerToken"), timeout);
    }

    private static string Text(JsonElement section, string name)
    {
        var value = section.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Falta o está vacío el valor 'Api:{name}'.")
            : value;
    }
}
