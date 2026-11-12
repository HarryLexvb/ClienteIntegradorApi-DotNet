using System.Text.Json;
using ClienteIntegradorApi.Services;

// Flujo: 1) cargar configuración, 2) llamar a la API, 3) resumir el JSON.
// Códigos de salida: 0 = éxito, 1 = fallo de red/HTTP/JSON, 2 = configuración inválida.

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== Cliente Integrador .NET - API AviHub v3 ===");

// appsettings.json se copia junto al ejecutable, por eso se busca en AppContext.BaseDirectory.
ApiSettings settings;
try
{
    settings = ApiSettings.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
}
catch (Exception ex) when (ex is InvalidOperationException or JsonException or IOException)
{
    Console.Error.WriteLine($"Configuración inválida: {ex.Message}");
    return 2;
}

// Un único HttpClient reutilizable (evita agotar sockets); el timeout viene de la configuración.
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds) };
var client = new EquipmentApiClient(http, settings);

// Ctrl+C cancela la solicitud en curso de forma ordenada.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine($"Endpoint solicitado : GET {new Uri(settings.BaseUrl, settings.Endpoint)}");
var result = await client.GetEquipmentsAsync(cts.Token);
Console.WriteLine($"Código HTTP         : {(result.StatusCode is null ? "sin respuesta" : $"{(int)result.StatusCode} {result.StatusCode}")}");

// Un fallo real (401, 5xx, red, timeout...) nunca se reemplaza por datos simulados.
if (!result.IsSuccess)
{
    Console.Error.WriteLine($"Resultado           : ERROR - {result.Error}");
    return 1;
}

try
{
    var summary = ResponseSummarizer.Summarize(result.Body!);
    Console.WriteLine("Resultado           : OK");
    Console.WriteLine(summary.ItemCount is { } n
        ? $"Equipos recibidos   : {n}"
        : "Equipos recibidos   : la respuesta no contiene una colección (se muestra el contenido)");
    foreach (var line in summary.Lines) Console.WriteLine(line);
    return 0;
}
catch (JsonException ex)
{
    // Respuesta 2xx pero cuerpo vacío o JSON inválido.
    Console.Error.WriteLine($"Resultado           : ERROR - respuesta vacía o JSON inválido ({ex.Message})");
    return 1;
}
