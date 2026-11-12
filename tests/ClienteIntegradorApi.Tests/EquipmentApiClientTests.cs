using System.Net;
using System.Text.Json;
using ClienteIntegradorApi.Services;

namespace ClienteIntegradorApi.Tests;

public class EquipmentApiClientTests
{
    private static readonly ApiSettings Settings =
        new(new Uri("https://example.test/api/v3/"), "equipments", "token-de-prueba", 15);

    // El manejador HTTP simulado permite validar las solicitudes y respuestas
    // sin depender de la disponibilidad de la API externa.
    /// <summary>Manejador falso: captura la solicitud y devuelve una respuesta fija, sin red.</summary>
    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Captured { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Captured = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    [Fact]
    public async Task EnviaCabeceraBearerYAcceptJson()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "[]");
        await new EquipmentApiClient(new HttpClient(handler), Settings).GetEquipmentsAsync();

        Assert.Equal(HttpMethod.Get, handler.Captured!.Method);
        Assert.Equal("https://example.test/api/v3/equipments", handler.Captured.RequestUri!.ToString());
        Assert.Equal("Bearer token-de-prueba", handler.Captured.Headers.Authorization!.ToString());
        Assert.Contains(handler.Captured.Headers.Accept, a => a.MediaType == "application/json");
    }

    [Fact]
    public async Task ProcesaJsonValidoConColeccionEnData()
    {
        var json = """{"data":[{"id":1,"serie":"A"},{"id":2,"serie":"B"}],"pagination":{"total_records":2}}""";
        var result = await new EquipmentApiClient(new HttpClient(new FakeHandler(HttpStatusCode.OK, json)), Settings)
            .GetEquipmentsAsync();

        Assert.True(result.IsSuccess);
        var summary = ResponseSummarizer.Summarize(result.Body!);
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(2, summary.Lines.Count);
        Assert.Contains("serie=A", summary.Lines[0]);
        Assert.Contains("id=2", summary.Lines[1]);
    }

    [Fact]
    public void ColeccionVaciaDevuelveCeroElementos()
    {
        var summary = ResponseSummarizer.Summarize("""{"data":[],"pagination":{}}""");

        Assert.Equal(0, summary.ItemCount);
        Assert.Empty(summary.Lines);
    }

    [Fact]
    public async Task Http401SeReportaComoErrorNoComoExito()
    {
        var result = await new EquipmentApiClient(
            new HttpClient(new FakeHandler(HttpStatusCode.Unauthorized, "{}")), Settings).GetEquipmentsAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Contains("credencial de demostración", result.Error);
        Assert.DoesNotContain("token-de-prueba", result.Error);
    }

    [Fact]
    public void JsonInvalidoOVacioLanzaJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => ResponseSummarizer.Summarize("no es json"));
        Assert.ThrowsAny<JsonException>(() => ResponseSummarizer.Summarize(""));
    }
}
