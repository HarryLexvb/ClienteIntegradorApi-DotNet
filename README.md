# Cliente Integrador .NET para Consumo de API

## A. Descripción general

Aplicación de consola en C# (.NET 10) que consume el recurso `equipments` de la API AviHub v3
(`https://avihub.pesoexato.com/api/v3/`), procesa la respuesta JSON y muestra en consola un resumen
estructurado: endpoint, código HTTP, resultado, número de equipos y una línea por equipo
(hasta 5) con los campos `id`, `serie`, `description`, `model` y `status` cuando existen.

## B. Arquitectura

Solución deliberadamente simple, sin dependencias externas (solo la biblioteca estándar de .NET):

| Componente | Responsabilidad |
|---|---|
| `Program.cs` | Presentación en consola, código de salida (0 = OK, 1 = fallo de la API/red, 2 = configuración inválida). |
| `Services/ApiSettings.cs` | Lee y valida `appsettings.json` con `System.Text.Json` (URL HTTPS, endpoint, token, timeout). |
| `Services/EquipmentApiClient.cs` | `GET /equipments` con `HttpClient`, cabeceras, traducción de errores HTTP/red a mensajes en español. |
| `Services/ResponseSummarizer.cs` | Procesa el JSON con `JsonDocument`/`JsonElement`: cuenta elementos y genera la muestra. |
| `tests/ClienteIntegradorApi.Tests` | Pruebas xUnit (5) con un `HttpMessageHandler` falso (no usan la red). |

- Se usa un único `HttpClient` reutilizado (aplicación de vida corta); **no** se usa `IHttpClientFactory`
  ni inyección de dependencias: el cliente recibe `HttpClient` y la configuración por constructor.
- **No hay DTOs**: el esquema de `equipments` no está documentado de forma accesible, por lo que se trabaja con
  `JsonElement` y no se inventan campos. Soporta raíz array, objeto con colección (`data`, `items` u otro array),
  objeto sin colección, colección vacía, cuerpo vacío y JSON inválido.
- Soporta `CancellationToken` (Ctrl+C) y timeout configurable.

## C. Simulación de seguridad

- Se eligió **Bearer** porque el reclutador entrega un ejemplo `Authorization: Bearer v3-demo-token`.
- El token se configura en `appsettings.json` (`Api:BearerToken`).
- `EquipmentApiClient` lo inyecta por solicitud con `AuthenticationHeaderValue("Bearer", token)` y añade
  `Accept: application/json`.
- Es una **simulación** de OAuth 2.0 Client Credentials: el token es fijo y de demostración; no hay servidor de
  autorización, obtención de token, refresco ni almacenamiento.
- El token nunca se imprime en consola ni en mensajes de error.
- No se desactiva la validación TLS. Un fallo de certificado se informa como error (la opción `--ssl-no-revoke`
  de curl no tiene equivalente aplicado aquí).
- No suba credenciales reales a repositorios públicos.

## D. Compilación y ejecución (Windows PowerShell)

```powershell
cd "ruta\a\ClienteIntegradorApi"      # carpeta que contiene ClienteIntegradorApi.csproj
dotnet restore
dotnet build
dotnet run
dotnet test tests/ClienteIntegradorApi.Tests
```

## E. Limitaciones

- La página `https://avihub.pesoexato.com/api/v3/docs` responde HTML (renderizado con JavaScript) y no se analizó
  en detalle; el formato se dedujo de la respuesta real de `/equipments`.
- La credencial es de demostración (simulada). En la ejecución realizada la API respondió **HTTP 200** con
  2 equipos; si en el futuro el servidor la rechaza (401/403), la aplicación lo informa como error.
- Solo se consume `GET /equipments`; no se gestiona paginación (la respuesta observada traía 1 página).
- Las pruebas usan respuestas simuladas únicamente para validar el cliente; nunca se presentan como datos reales.
