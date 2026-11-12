using System.Net;
using System.Net.Http.Headers;

namespace ClienteIntegradorApi.Services;

/// <summary>Resultado de la llamada HTTP: nunca se sustituye por datos simulados.</summary>
public sealed record ApiResponse(HttpStatusCode? StatusCode, string? Body, string? Error)
{
    public bool IsSuccess => Error is null;
}

/// <summary>Cliente HTTP del recurso "equipments". Solo se ocupa de la comunicación, no de la presentación.</summary>
public sealed class EquipmentApiClient(HttpClient http, ApiSettings settings)
{
    public async Task<ApiResponse> GetEquipmentsAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(settings.BaseUrl, settings.Endpoint));
        // Simulación de OAuth 2.0 Client Credentials: token fijo de configuración, sin flujo de obtención.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.BearerToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            // Una sola solicitud GET. Los códigos no 2xx no lanzan excepción: se convierten en un
            // ApiResponse con mensaje en español para que Program decida el código de salida.
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return response.IsSuccessStatusCode
                ? new ApiResponse(response.StatusCode, body, null)
                : new ApiResponse(response.StatusCode, body, DescribeStatus(response.StatusCode));
        }
        // Si el token de cancelación no fue activado, la cancelación proviene del timeout de HttpClient.
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ApiResponse(null, null, $"Tiempo de espera agotado ({settings.TimeoutSeconds} s).");
        }
        catch (OperationCanceledException)
        {
            return new ApiResponse(null, null, "La solicitud fue cancelada.");
        }
        catch (HttpRequestException ex)
        {
            // Solo se informa la causa; el token nunca forma parte del mensaje.
            var causa = ex.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => "no se pudo resolver el nombre DNS del servidor",
                HttpRequestError.ConnectionError => "no se pudo establecer la conexión",
                HttpRequestError.SecureConnectionError => "falló la validación TLS/SSL (no se desactiva por seguridad)",
                _ => ex.Message
            };
            return new ApiResponse(null, null, $"Error de red: {causa}.");
        }
    }

    public static string DescribeStatus(HttpStatusCode code) => (int)code switch
    {
        400 => "HTTP 400: solicitud incorrecta.",
        401 => "HTTP 401: la API no acepta la credencial de demostración (token Bearer rechazado).",
        403 => "HTTP 403: acceso prohibido; la credencial de demostración no tiene permisos.",
        404 => "HTTP 404: recurso no encontrado; revise BaseUrl y Endpoint.",
        429 => "HTTP 429: demasiadas solicitudes; reintente más tarde.",
        >= 500 => $"HTTP {(int)code}: error interno del servidor de la API.",
        _ => $"HTTP {(int)code}: respuesta no exitosa."
    };
}
