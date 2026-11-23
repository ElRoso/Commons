# Changelog

Una sección `## <PackageId> <versión>` por paquete y versión; los workflows de release la buscan por ese par.

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
