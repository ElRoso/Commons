# Changelog

Una sección `## <PackageId> <versión>` por paquete y versión; los workflows de release la buscan por ese par.

## ElRoso.Commons.OpenApi 1.0.0

- Paquete nuevo: documentación OpenAPI con `Microsoft.AspNetCore.OpenApi` 10.0.15. `AddElRosoOpenApi` registra un documento por versión de API (`Asp.Versioning.OpenApi` 10.2.3) con esquemas Bearer (JWT) y/o Basic; `MapElRosoOpenApi` publica `/openapi/{documentName}.json`.
- El documento sirve para generar tipos TypeScript (por ejemplo, `openapi-typescript`).

## ElRoso.Commons.AspNetCore 2.2.0

- `AddApiVersioningCustom` acepta una acción de configuración aditiva para registrar el generador OpenAPI sin duplicar el versionado.
- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons.Swagger 2.0.3

- Sin cambios propios: Swashbuckle resuelve `Microsoft.OpenApi` 2.13.1 (antes 2.7.5) por la suba del pin de seguridad.
- Un consumidor que fija `Microsoft.OpenApi` por debajo de 2.13.1 tiene que subirlo.

## ElRoso.Commons.Core 1.1.1

- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons.AzureAD 2.0.3

- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons.AzureBlob 1.0.4

- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons.EntityFrameworkCore 1.0.3

- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons.Graph 2.0.1

- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons.MongoDB 1.0.3

- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons.S3 1.0.3

- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons.Serilog 1.0.4

- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons.SharePoint 2.0.1

- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons.Telegram 1.0.4

- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons.WindowsServices 1.0.4

- Sin cambios propios: se republica por la suba del pin de seguridad Microsoft.OpenApi 2.7.5 → 2.13.1.

## ElRoso.Commons 2029.1.0

Metapaquete: referencia `ElRoso.Commons.OpenApi` 1.0.0 y las versiones nuevas de los otros 13 paquetes.

## ElRoso.Commons.Core 1.1.0

- `Result`, `Result<T>`, `Error` y `ErrorType` (namespace `ElRoso.Outcomes`): un handler devuelve el error en vez de lanzar. `Error.FromValidation` arma el error de validación desde FluentValidation.

## ElRoso.Commons.AspNetCore 2.1.0

- `AddElRosoProblemDetails` y `UseElRosoProblemDetails`: respuestas `application/problem+json` RFC 9457 (`type`, `title`, `status`, `detail`, `instance`, `code`, `traceId`, `errors`) para `Result` y para las excepciones tipadas.
- `ToHttpResult` (Minimal APIs) y `ToActionResult` (controllers) convierten un `Result` en la respuesta HTTP.
- `ExceptionHandlerMiddleware` (RFC 7807) no cambia. Se monta uno de los dos, no ambos.

## ElRoso.Commons 2029.0.0

Metapaquete: referencia `Graph` 2.0.0, `SharePoint` 2.0.0 y las versiones nuevas de los otros 11 paquetes.

- Cambio que rompe: arrastra `Microsoft.Graph` 6 y `Microsoft.Kiota` 2; ver `ElRoso.Commons.Graph 2.0.0` y `ElRoso.Commons.SharePoint 2.0.0`.

## ElRoso.Commons.Graph 2.0.0

- `Microsoft.Graph` 5.105.0 → 6.11.1 (`Microsoft.Graph.Core` 3.2.5 → 4.0.1, `Microsoft.Kiota` 1.22.2 → 2.2.0). El envío por `SendGraphAsync` no cambia.
- Cambio que rompe: `Microsoft.Graph` 6 arrastra `Microsoft.Graph.Core` 4 y `Microsoft.Kiota` 2. Un consumidor que usa el SDK de Graph directamente tiene que compilar contra la 6. Kiota 2 quitó `KiotaSerializer.Deserialize` síncrono e `IAsyncParseNodeFactory`.
- Cambio que rompe: un fallo al parsear la respuesta de Graph llega como `ApiException` con el status, los headers y el body.

## ElRoso.Commons.SharePoint 2.0.0

- `Microsoft.Graph` 5.105.0 → 6.11.1 (`Microsoft.Graph.Core` 3.2.5 → 4.0.1, `Microsoft.Kiota` 1.22.2 → 2.2.0).
- Cambio que rompe: `ISharePointFileService` devuelve `DriveItem` de `Microsoft.Graph.Models`; un consumidor que usa esos tipos, o el SDK de Graph directamente, tiene que compilar contra la 6. Kiota 2 quitó `KiotaSerializer.Deserialize` síncrono e `IAsyncParseNodeFactory`.
- Cambio que rompe: `ItemWithPath` lanza con un path nulo o vacío. `UploadFileSharePointAsync` con un nombre de archivo vacío o en blanco lanza `ArgumentException` antes de escribir.
- Cambio que rompe: un fallo al parsear la respuesta de Graph llega como `ApiException` con el status, los headers y el body. Un 404 de Graph sigue llegando como `ODataError`: la descarga lo traduce a `FileNotFoundException` y el borrado lo ignora, igual que antes.

## ElRoso.Commons.AspNetCore 2.0.2

- Sin cambios propios: se republica por el cambio de los pins de seguridad de `Directory.Packages.props` (familia `Microsoft.Kiota` 1.22.2 → 2.2.0).

## ElRoso.Commons.AzureAD 2.0.2

- Sin cambios propios: se republica por el cambio de los pins de seguridad de `Directory.Packages.props` (familia `Microsoft.Kiota` 1.22.2 → 2.2.0).

## ElRoso.Commons.AzureBlob 1.0.3

- Sin cambios propios: se republica por el cambio de los pins de seguridad de `Directory.Packages.props` (familia `Microsoft.Kiota` 1.22.2 → 2.2.0).

## ElRoso.Commons.Core 1.0.2

- Sin cambios propios: se republica por el cambio de los pins de seguridad de `Directory.Packages.props` (familia `Microsoft.Kiota` 1.22.2 → 2.2.0).

## ElRoso.Commons.EntityFrameworkCore 1.0.2

- Sin cambios propios: se republica por el cambio de los pins de seguridad de `Directory.Packages.props` (familia `Microsoft.Kiota` 1.22.2 → 2.2.0).

## ElRoso.Commons.MongoDB 1.0.2

- Sin cambios propios: se republica por el cambio de los pins de seguridad de `Directory.Packages.props` (familia `Microsoft.Kiota` 1.22.2 → 2.2.0).

## ElRoso.Commons.S3 1.0.2

- Sin cambios propios: se republica por el cambio de los pins de seguridad de `Directory.Packages.props` (familia `Microsoft.Kiota` 1.22.2 → 2.2.0).

## ElRoso.Commons.Serilog 1.0.3

- Sin cambios propios: se republica por el cambio de los pins de seguridad de `Directory.Packages.props` (familia `Microsoft.Kiota` 1.22.2 → 2.2.0).

## ElRoso.Commons.Swagger 2.0.2

- Sin cambios propios: se republica por el cambio de los pins de seguridad de `Directory.Packages.props` (familia `Microsoft.Kiota` 1.22.2 → 2.2.0).

## ElRoso.Commons.Telegram 1.0.3

- Sin cambios propios: se republica por el cambio de los pins de seguridad de `Directory.Packages.props` (familia `Microsoft.Kiota` 1.22.2 → 2.2.0).

## ElRoso.Commons.WindowsServices 1.0.3

- Sin cambios propios: se republica por el cambio de los pins de seguridad de `Directory.Packages.props` (familia `Microsoft.Kiota` 1.22.2 → 2.2.0).

## ElRoso.Commons 2028.0.1

Metapaquete: referencia las versiones nuevas de los 13 paquetes por área.

## ElRoso.Commons.AspNetCore 2.0.1

- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.7 → 10.0.15.
- Dependencias actualizadas de `Core`.

## ElRoso.Commons.AzureAD 2.0.1

- `Microsoft.Identity.Web` 4.9.0 → 4.18.0.
- Dependencias actualizadas de `Core`.

## ElRoso.Commons.AzureBlob 1.0.2

- Sin cambios propios: dependencias actualizadas de `Core`.

## ElRoso.Commons.Core 1.0.1

- `FluentValidation` 12.1.1 → 12.2.0.

## ElRoso.Commons.EntityFrameworkCore 1.0.1

- `Microsoft.EntityFrameworkCore` 10.0.7 → 10.0.15.
- Dependencias actualizadas de `Core`.

## ElRoso.Commons.Graph 1.0.1

- Sin cambios propios: dependencias actualizadas de `Core`.

## ElRoso.Commons.MongoDB 1.0.1

- `MongoDB.Driver` y `MongoDB.Bson` 3.9.0 → 3.14.1.
- Dependencias actualizadas de `Core`.

## ElRoso.Commons.S3 1.0.1

- `AWSSDK.S3` 4.0.23 → 4.1.2.
- Dependencias actualizadas de `Core`.

## ElRoso.Commons.Serilog 1.0.2

- Sin cambios propios: dependencias actualizadas de `Core`.

## ElRoso.Commons.SharePoint 1.0.1

- Sin cambios propios: dependencias actualizadas de `Core`.

## ElRoso.Commons.Swagger 2.0.1

- `Swashbuckle.AspNetCore.SwaggerGen` y `SwaggerUI` 10.1.7 → 10.3.1.
- Dependencias actualizadas de `Core`.

## ElRoso.Commons.Telegram 1.0.2

- Sin cambios propios: dependencias actualizadas de `Core`.

## ElRoso.Commons.WindowsServices 1.0.2

- Sin cambios propios: dependencias actualizadas de `Core`.

## ElRoso.Commons 2028.0.0

Metapaquete: referencia `AspNetCore` 2.0.0, `Swagger` 2.0.0 y `AzureAD` 2.0.0.

- Cambio que rompe: arrastra `Asp.Versioning` 10; ver `ElRoso.Commons.AspNetCore 2.0.0`.

## ElRoso.Commons.AspNetCore 2.0.0

- `Asp.Versioning.Mvc` y `Asp.Versioning.Mvc.ApiExplorer` 8.1.1 → 10.2.1. `AddApiVersioningCustom` registra el versionado de controllers con `AddMvc()` explícito; el ruteo, el header `api-supported-versions` y los grupos del ApiExplorer no cambian.
- Cambio que rompe: la v10 de `Asp.Versioning` convirtió en propiedades métodos de extensión que un consumidor puede usar, y el código que los llama deja de compilar. Por ejemplo: `HttpContext.GetRequestedApiVersion()` → `HttpContext.RequestedApiVersion`; `ApiDescription.GetApiVersion()` → `ApiDescription.ApiVersion`; `ApiDescription.IsDeprecated()` → `ApiDescription.IsDeprecated`; `ActionDescriptor.GetApiVersionMetadata()` → `ActionDescriptor.ApiVersionMetadata`.

## ElRoso.Commons.Swagger 2.0.0

- Cambio que rompe: depende de `ElRoso.Commons.AspNetCore` 2.0.0 y arrastra `Asp.Versioning` 10; ver `ElRoso.Commons.AspNetCore 2.0.0`.

## ElRoso.Commons.AzureAD 2.0.0

- Cambio que rompe: depende de `ElRoso.Commons.AspNetCore` 2.0.0 y arrastra `Asp.Versioning` 10; ver `ElRoso.Commons.AspNetCore 2.0.0`.

## ElRoso.Commons 2027.0.1

Metapaquete: referencia las versiones nuevas de `Serilog`, `Telegram`, `WindowsServices`, `AzureBlob` y `AzureAD`.

## ElRoso.Commons.AzureAD 1.0.1

- Declara `System.Security.Cryptography.Xml` 10.0.15 como dependencia: un consumidor sin ASP.NET Core resolvía 10.0.7, con advisories HIGH.

## ElRoso.Commons.Serilog 1.0.1

- `Serilog` 4.3.1 → 4.4.0 y `Serilog.Sinks.Seq` 9.0.0 → 9.1.0.

## ElRoso.Commons.Telegram 1.0.1

- `Microsoft.Extensions.Http.Resilience` 10.8.0 → 10.11.1.

## ElRoso.Commons.WindowsServices 1.0.1

- `Microsoft.Extensions.Hosting.WindowsServices` 10.0.7 → 10.0.15.

## ElRoso.Commons.AzureBlob 1.0.1

- `Azure.Storage.Blobs` 12.29.1 → 12.30.2.

## ElRoso.Commons 2027.0.0

Metapaquete: referencia los 13 paquetes por área y trae `ElRosoApplicationBuilder`.

- Breaking: se parte la librería en paquetes por área; los consumidores se recompilan (sin `TypeForwardedTo`).
- Breaking: se quitan `Microsoft.EntityFrameworkCore.SqlServer` y `Microsoft.AspNetCore.Mvc.NewtonsoftJson`; el consumidor elige su proveedor de base de datos.
- `ElRosoApplicationBuilder.CreateBuilder` y `UseElRoso` quedan `[Obsolete]` (warning CS0618) a favor de los `AddX` / `UseX` de cada paquete.

## ElRoso.Commons.Core 1.0.0

Excepciones tipadas, DTOs y respuestas, `EncryptionHelper` (BCrypt), `CronJobService` (Cronos), `CustomRandom`, helpers y contratos de servicios (`IMailService`, `IBlobStorageService`, `ITelegramNotifier`).

## ElRoso.Commons.AspNetCore 1.0.0

Middlewares de excepciones (RFC 7807) y de usuario actual, forwarded headers, health check, CORS, controllers, versionado de API y autenticación JWT.

- `AddElRosoConfiguration` y `MapElRosoInfo` quedan públicos para componer el host sin `ElRosoApplicationBuilder`.
- `ExceptionHandlerMiddleware` loguea una excepción de dominio en Warning y una desconocida en Error con stack trace.

## ElRoso.Commons.Serilog 1.0.0

Logging con Serilog (consola, archivo, Seq, Graylog) con `AddElRosoSerilog` sobre el builder del host; silencia `Microsoft.AspNetCore.Hosting.Diagnostics` bajo Warning.

## ElRoso.Commons.Swagger 1.0.0

Swagger / OpenAPI con Swashbuckle, una documentación por versión de API.

## ElRoso.Commons.EntityFrameworkCore 1.0.0

Repositorio base con Entity Framework Core sin proveedor de base de datos: cache en memoria, paginación y auditoría.

## ElRoso.Commons.MongoDB 1.0.0

Repositorio base para MongoDB con auditoría, paginación y escrituras en lote.

## ElRoso.Commons.AzureAD 1.0.0

Autenticación con Azure AD (Microsoft.Identity.Web).

## ElRoso.Commons.Graph 1.0.0

Envío de mails por Microsoft Graph o SMTP.

## ElRoso.Commons.SharePoint 1.0.0

Archivos en SharePoint Online por Microsoft Graph.

## ElRoso.Commons.S3 1.0.0

Almacenamiento de archivos en S3 o endpoints S3-compatibles.

## ElRoso.Commons.AzureBlob 1.0.0

Almacenamiento de archivos en Azure Blob Storage.

## ElRoso.Commons.Telegram 1.0.0

Bots de Telegram: envío con reintentos y circuit breaker, long polling y notificador a N destinatarios.

## ElRoso.Commons.WindowsServices 1.0.0

Hosting como Windows Service.
