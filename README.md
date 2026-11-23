# ElRoso.Commons

Librería base compartida para backends **ASP.NET Core Web API** del ecosistema ElRoso. Provee repositorios, servicios, middlewares, autenticación JWT, hashing de contraseñas, cron jobs y utilidades listas para producción.

## Paquetes

Publicados en nuget.org. Cada área es un paquete aparte: se instala solo lo que se usa.

| Paquete | Contenido |
| --- | --- |
| `ElRoso.Commons.Core` | Excepciones tipadas, DTOs y respuestas, hashing BCrypt, cron jobs, helpers y contratos de servicios |
| `ElRoso.Commons.AspNetCore` | Middlewares (RFC 7807, usuario actual), forwarded headers, health check, CORS, controllers, versionado de API, JWT |
| `ElRoso.Commons.Serilog` | Logging con Serilog: consola, archivo, Seq y Graylog |
| `ElRoso.Commons.Swagger` | Swagger / OpenAPI con Swashbuckle |
| `ElRoso.Commons.EntityFrameworkCore` | Repositorio base con EF Core, sin proveedor de base de datos |
| `ElRoso.Commons.MongoDB` | Repositorio base para MongoDB |
| `ElRoso.Commons.AzureAD` | Autenticación con Azure AD |
| `ElRoso.Commons.Graph` | Mails por Microsoft Graph o SMTP |
| `ElRoso.Commons.SharePoint` | Archivos en SharePoint Online |
| `ElRoso.Commons.S3` | Archivos en S3 o endpoints S3-compatibles |
| `ElRoso.Commons.AzureBlob` | Archivos en Azure Blob Storage |
| `ElRoso.Commons.Telegram` | Bots de Telegram con reintentos y circuit breaker |
| `ElRoso.Commons.WindowsServices` | Hosting como Windows Service |
| `ElRoso.Commons` | Metapaquete: todos los anteriores más `ElRosoApplicationBuilder` |

```bash
dotnet add package ElRoso.Commons.AspNetCore
```

**Versionado por paquete:** cada paquete lleva su propia versión SemVer. El metapaquete `ElRoso.Commons`
está en `2027.0.0` y los paquetes por área en `1.0.0`. Los tags de release son `<PackageId>/vX.Y.Z` y los
cambios por versión están en [`CHANGELOG.md`](CHANGELOG.md).

Dependencias entre paquetes: todos dependen de `ElRoso.Commons.Core`, salvo `ElRoso.Commons.AzureAD` y
`ElRoso.Commons.Swagger`, que dependen de `ElRoso.Commons.AspNetCore`.

---

## Stack Tecnológico

| Capa                | Tecnología                                               |
| ------------------- | -------------------------------------------------------- |
| Framework           | .NET 10.0 (LTS) — ASP.NET Core Web API                   |
| ORM                 | Entity Framework Core 10                                 |
| Base de datos SQL   | EF Core 10, sin proveedor: lo elige el consumidor        |
| Base de datos NoSQL | MongoDB (driver 3.9)                                     |
| Autenticación       | JWT Bearer / Azure AD                                    |
| Serialización       | System.Text.Json                                         |
| Validación          | FluentValidation 12                                      |
| Logging             | Serilog (Seq, Graylog, File)                             |
| API Docs            | Swagger / OpenAPI + API Versioning                       |
| Password Hashing    | BCrypt.Net-Next (work factor configurable)               |
| Cron Jobs           | IHostedService + Cronos                                  |
| Email               | Microsoft Graph / SMTP                                   |
| Archivos            | AWS S3 / Azure Blob Storage (`IBlobStorageService`) + SharePoint Online (`ISharePointFileService`) |
| Mensajería          | Telegram Bot API (multi-bot)                             |
| Tests               | xUnit 2.9 + Moq                                          |

---

## Instalación y Uso

### 1. Registrar servicios en `Program.cs`

Cada paquete aporta sus `AddX` / `UseX`; se componen los de los paquetes instalados.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddElRosoConfiguration(args);   // AspNetCore: appsettings, appsettings.{env}, user-secrets (solo Development), env y args
builder.AddElRosoSerilog();             // Serilog: consola, archivo, Seq, Graylog

var services = builder.Services;
var configuration = builder.Configuration;

services.AddElRosoForwardedHeaders(configuration);   // detrás de un reverse proxy
services.AddEncryptionHelper(configuration);         // Core: BCrypt, work factor opcional (default 12 ≈ 250ms por hash)
services.AddMemoryCacheCustom();                     // EntityFrameworkCore
services.AddMongoDb(configuration);                  // MongoDB: MongoClient Singleton con connection pool
services.AddMailService();                           // Graph: mails por Microsoft Graph o SMTP
services.AddHealthChecks();
services.AddExceptionHandlerMiddleware();
services.AddCurrentUserMiddleware();
services.AddControllersCustom(configuration);
services.AddApiVersioningCustom(configuration);
services.AddCorsCustom(configuration);              // lee Cors:AllowedOrigins del config
services.AddAuthenticationJWT(configuration);        // o AddAuthenticationAzureAD (AzureAD)

// Archivos — opt-in, elegir uno de los dos primeros (ver sección "Archivos" más abajo):
// services.AddS3BlobStorage();
// services.AddAzureBlobStorage();
// services.AddSharePointFileService();

// Telegram — un bot o varios, cada uno con su token encapsulado
services.AddTelegramBot("alertas", o => o.Token = configuration["Telegram:BotToken"]!);
// services.AddTelegramBot("soporte", o => o.Token = configuration["Telegram:SupportBotToken"]!);

// Handler de updates entrantes (implementar ITelegramUpdateHandler)
services.AddSingleton<ITelegramUpdateHandler, MiTelegramHandler>();
// Activar long-polling (omitir si usás webhooks)
services.AddTelegramPolling();
```

### 2. Configurar el middleware pipeline

```csharp
var app = builder.Build();

app.UseElRosoForwardedHeaders();     // 🔴 primero de todo, si hay reverse proxy
app.UseExceptionHandlerMiddleware();
app.UseSwaggerCustom();              // Swagger
app.UseStaticFiles();
app.UseRouting();
app.UseEnableAllCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseCurrentUserMiddleware();      // 🔴 DESPUÉS de la autenticación, ANTES de los endpoints
app.MapControllers();
app.MapHealthChecksCustom();         // /healthz con el formato HealthCheckResponse de la librería
app.MapElRosoInfo(app.Environment);  // GET / informativo
app.Run();
```

El consumidor mínimo compilado en el CI está en [`samples/Minimal`](samples/Minimal/Program.cs).

> 🔴 **El orden de `UseCurrentUserMiddleware` no es negociable.** Antes de `UseAuthentication` el
> usuario todavía no está autenticado y el middleware pasa de largo; después de mapear los endpoints
> **no se ejecuta nunca**. En cualquiera de los dos casos `CurrentUser` queda `null` y la auditoría
> graba `CreatedBy`/`ModifiedBy` en `null` sin fallar ni avisar.
>
> ⚠️ **No se monta por defecto** a propósito: `CurrentUserMiddleware` responde 401 si falta
> cualquiera de los cuatro claims de Azure AD, así que montarlo solo rompería toda app cuyo JWT
> propio no los traiga.

> **`ElRosoApplicationBuilder.CreateBuilder` y `UseElRoso` (metapaquete `ElRoso.Commons`) están
> `[Obsolete]`.** Siguen funcionando para los consumidores que ya los usan, pero compilan con el warning
> CS0618: armar el host con los `AddX` / `UseX` de arriba. Con `UseElRoso`, `CurrentUserMiddleware` se pide
> por `beforeEndpoints: inner => inner.UseCurrentUserMiddleware()`, la única posición válida.

### 3. Configuración requerida (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=ElRosoDB;...",
    "MongoDB": "mongodb://localhost:27017"
  },
  "MongoDb": {
    "DatabaseName": "ElRosoDB"
  },
  "JWT": {
    "Issuer": "https://mi-api.com",
    "Audience": "https://mi-app.com",
    "IssuerSigningKey": "<clave-secreta-larga>"
  },
  "Encryption": {
    "WorkFactor": 12
  },
  "Cors": {
    "AllowedOrigins": ["https://mi-app.com", "https://backoffice.mi-app.com"]
  },
  "ApplicationDisplayName": "MiBackend",
  "DbCacheIsEnabled": true,
  "DbCacheReplicas": 1,
  "Logging": { "FilePath": "/logs/MiBackend" }
}
```

> 🔴 **El nombre visible de la app va en `ApplicationDisplayName`, no en `ApplicationName`.** Es el
> título de Swagger y el campo `ApplicationName` de cada línea de log. `ApplicationName` es una clave
> **reservada del host** de ASP.NET Core: la lee como el nombre del assembly de la aplicación, así
> que `--ApplicationName=LoQueSea` termina buscando un assembly con ese nombre y **la app no
> arranca**. Se sigue leyendo como respaldo desde `appsettings.json` —donde es inofensiva— para no
> romper los despliegues que ya la tienen, pero lo nuevo va en la clave propia. Si aun así se pasa
> por línea de comandos, la excepción explica esto en vez del `FileNotFoundException` original.

> 🔴 **`DbCacheIsEnabled` asume UNA sola réplica.** La cache vive dentro del proceso: con dos
> réplicas cada una cachea por su cuenta y una escritura solo invalida la que la atendió — la otra
> sigue respondiendo con datos de hasta **20 minutos** (la expiración absoluta). Ningún proceso puede
> contar sus propias réplicas, así que el despliegue las declara en **`DbCacheReplicas`**: con más de
> una y la cache encendida, la app escribe un **Warning al arrancar**. Con una sola deja una línea
> Information que dice la suposición, para que la lea quien escale. La mitigación es
> `DbCacheIsEnabled: false`.

> **CORS:** Si `Cors:AllowedOrigins` está ausente o vacío, la política permite cualquier origen. Solo usar en Development: fuera de Development no rompe el arranque y solo escribe un Warning.

> **JWT y Docker:** `RequireHttpsMetadata` está en `false` por defecto. Cuando el API corre detrás de un reverse proxy (nginx, Traefik, Azure API Gateway) que termina HTTPS, esto es correcto. En exposición directa, habilitar `UseHttps: true` en appsettings.

---

## Estructura del Proyecto

```text
src/
├── ElRoso.Commons/                      # Metapaquete + ElRosoApplicationBuilder
├── ElRoso.Commons.Core/                 # Excepciones, DTOs, respuestas, EncryptionHelper (BCrypt), CronJobService, helpers, contratos
├── ElRoso.Commons.AspNetCore/           # Middlewares, forwarded headers, health check, CORS, controllers, versionado de API, JWT
├── ElRoso.Commons.Serilog/              # AddElRosoSerilog, sinks, SeqSinkSettings
├── ElRoso.Commons.Swagger/              # Swagger / OpenAPI, una doc por versión
├── ElRoso.Commons.EntityFrameworkCore/  # ElRosoRepositoryBase<T>, BaseEntity, AddMemoryCacheCustom
├── ElRoso.Commons.MongoDB/              # Repository<T>, AuditEntity, AddMongoDb
├── ElRoso.Commons.AzureAD/              # AddAuthenticationAzureAD
├── ElRoso.Commons.Graph/                # MailService (Graph / SMTP)
├── ElRoso.Commons.SharePoint/           # ISharePointFileService
├── ElRoso.Commons.S3/                   # S3BlobStorageService
├── ElRoso.Commons.AzureBlob/            # AzureBlobStorageService
├── ElRoso.Commons.Telegram/             # Bots, polling, notifier, resiliencia
└── ElRoso.Commons.WindowsServices/      # Hosting como Windows Service
Tests/                                   # xUnit + Moq, un proyecto para todos los paquetes
samples/Minimal/                         # Consumidor mínimo
eng/                                     # Scripts de CI: gate de licencias, verify-pack, guarda de versión
```

> **Sin dominio User:** La librería no incluye `UserEntity`, `UserDAO`, ni `UserService`. Cada sistema consumidor define su propio modelo de usuario exteniendo `BaseEntity` e `ElRosoRepositoryBase<T>`.

---

## Repositorio SQL (`ElRosoRepositoryBase<TEntity>`)

Base genérica sobre EF Core con:

- `AsNoTracking()` global y `SplitQuery` habilitados en el `DbContext`
- Cache en memoria con `IMemoryCache` (invalidación automática en writes)
- Auditoría automática (`CreationDate`, `ModificationDate`, `CreatedBy`, `ModifiedBy`)
- Guard clause en updates: lanza `NotFoundException` si la entidad no existe

```csharp
// Extender la entidad base
public class ProductoEntity : BaseEntity
{
    public required string Nombre { get; set; }
    public decimal Precio { get; set; }
}

// Extender el repositorio base
public class ProductoDao : ElRosoRepositoryBase<ProductoEntity>, IProductoDao
{
    public ProductoDao(DbContext context, IConfiguration config,
        IHttpContextAccessor accessor, ILogger<ProductoDao> logger, IMemoryCache cache)
        : base(context, config, accessor, logger, cache) { }
}

// CRUD
await dao.AddAsync(entity);
await dao.UpdateAsync(entity);
await dao.DeleteAsync(entity);
var entity = await dao.GetAsync(id);

// Paginación con total
var (items, total) = await dao.GetPageAsync(page: 1, pageSize: 20);
```

> ⚠️ **`ExecuteUpdateAsync` no es un método de `ElRosoRepositoryBase`.** Para actualizar 1-2 campos
> en una sola query, se llama desde el DAO derivado sobre el `Context` protected — e **invalidando
> la cache a mano**, porque la clase base no se entera de esa escritura:
>
> ```csharp
> public async Task ActualizarPrecioAsync(Guid id, decimal precio, CancellationToken ct = default)
> {
>     await this.Context.Set<ProductoEntity>()
>         .Where(e => e.Id == id)
>         .ExecuteUpdateAsync(s => s.SetProperty(e => e.Precio, precio), ct);
>
>     this.memoryCache.Remove(this.KeyNameCache);
> }
> ```

> ⚠️ `GetAsync()` sin filtro trae **como máximo 10 000 registros**. Si la tabla tiene más, el
> resultado se trunca, queda un **Warning en el log** y **no se cachea** (cachear un resultado
> incompleto lo serviría 20 minutos como si fuera la tabla entera). En tablas grandes usar siempre
> `GetPageAsync`. El tope se puede ajustar por entidad sobrescribiendo `MaxRecordsLimit` en el DAO.

> ⚠️ **`Dispose()` del repositorio no dispone el `DbContext`.** No es suyo: lo creó el contenedor de
> DI y lo libera al cerrar el scope. Antes lo disponía, y el primer DAO dispuesto de un request
> dejaba a todos los demás con un contexto muerto.

---

## Repositorio NoSQL (`Repository<T>`)

Base genérica sobre MongoDB con API completamente async:

```csharp
await repo.InsertAsync(entity);
await repo.UpdateAsync(entity);         // ReplaceOneAsync — atómico
await repo.UpdateAsync(entities);       // BulkWrite ordenado — 1 solo viaje
await repo.DeleteAsync(entity);         // DeleteOneAsync
await repo.DeleteAsync(entities);       // DeleteManyAsync — 1 sola query
var items = await repo.GetAsync();      // todo, hasta el tope de seguridad
```

Consultas:

```csharp
var activos   = await repo.GetAsync(x => x.Activo);                   // filtro por expresión
var primero   = await repo.FirstOrDefaultAsync(x => x.Codigo == "A"); // Limit(1), o null
var hay       = await repo.AnyAsync(x => x.Activo);                   // existencia, no conteo
var total     = await repo.CountAsync();                              // cuenta en el servidor
var (pagina, cuantos) = await repo.GetPageAsync(page: 1, pageSize: 50);
var (filtrada, n)     = await repo.GetPageAsync(1, 50, x => x.Activo);
```

> ⚠️ `GetAsync()` —con filtro o sin él— trae **como máximo 10 000 documentos**. Si hay más, corta y
> escribe un **Warning**: usar `GetPageAsync`. El tope se sobrescribe con `MaxRecordsLimit` en el
> repositorio derivado. Es el mismo tope que el lado SQL, que acá no existía.

> ⚠️ `GetPageAsync` ordena por `Audit.CreationDate` — sin orden, la paginación no es determinista.
> **Conviene un índice sobre ese campo:** un orden sin índice se resuelve en memoria y Mongo lo
> aborta pasados los 32 MB.

> `IMongoClient` e `IMongoDatabase` **deben** registrarse como Singleton via `services.AddMongoDb(configuration)`.

**Auditoría:** el repositorio completa `Audit.CreationDate`, `ModificationDate`, `CreatedBy` y
`ModifiedBy`. Para que registre **quién**, hay que pasarle un `IHttpContextAccessor`:

```csharp
public class LogRepository(IMongoDatabase db, ILogger<Repository<LogEntity>> logger, IHttpContextAccessor accessor)
    : Repository<LogEntity>(db, logger, accessor) { }
```

> ⚠️ Sin el accessor solo se estampan las fechas; `CreatedBy`/`ModifiedBy` quedan en `null`. Son
> `Guid?`, porque la identidad de quien llama es el `Guid` del token.

---

## Autenticación

### JWT Bearer

```csharp
// Registro
services.AddAuthenticationJWT(configuration);

// Pipeline
app.UseAuthentication();
app.UseAuthorization();
app.UseCurrentUserMiddleware(); // DESPUÉS de la autenticación: antes, el usuario no existe todavía
```

El `CurrentUserMiddleware` extrae del token JWT los claims de Azure AD (`objectidentifier`, `GivenName`, `Surname`, `Upn`) y los expone en `HttpContext.Items["currentUser"]` como `CurrentUser`.

```csharp
public class MiController : ElRosoControllerBase
{
    [Authorize]
    public IActionResult MiEndpoint()
    {
        var usuario = this.CurrentUser; // CurrentUser { Id, GivenName, Surname, EmailAddress }
        return Ok(usuario);
    }
}
```

### Azure AD

```csharp
services.AddAuthenticationAzureAD(configuration);
// appsettings: AzureAd:ClientId + AzureAd:TenantId
```

---

## Middlewares

| Middleware                   | Función                                                                                                             |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| `ExceptionHandlerMiddleware` | RFC 7807 `application/problem+json`. Mensaje genérico en Production, detalle en Development. Excepción de dominio en Warning, desconocida en Error con stack trace. |
| `CurrentUserMiddleware`      | Extrae claims JWT → `CurrentUser` en `HttpContext.Items["currentUser"]`. Lanza 401 si falta cualquier claim.        |

---

## Hashing de Contraseñas (`EncryptionHelper`)

BCrypt con work factor configurable. Registrado como Singleton — se inicializa una vez al arrancar.

```csharp
// Registro (Program.cs)
services.AddEncryptionHelper(configuration); // lee Encryption:WorkFactor (default 12)

// Uso
public class UsuarioService(EncryptionHelper hasher)
{
    public string CrearHash(string password)
        => hasher.HashPassword(password); // "$2a$12$..."

    public bool Verificar(string password, string hashGuardado)
        => hasher.VerifyPassword(password, hashGuardado); // true/false
}
```

```json
// appsettings.json — work factor sugerido por entorno
"Encryption": {
  "WorkFactor": 12   // Production: 12 (250ms) | Development: 4 (rápido para tests)
}
```

> BCrypt embebe el salt en el hash — no se necesita almacenarlo por separado.

---

## Email

### Microsoft Graph

```csharp
await mailService.SendGraphAsync(new EmailRequest
{
    FromAddress = "notificaciones@mi-empresa.com",
    Recipients = ["cliente@example.com"],
    CopyRecipients = ["copia@mi-empresa.com"],
    Subject = "Asunto",
    Body = "<h1>Hola</h1>",
    AttachFiles = new List<FileInfo> { new FileInfo("adjunto.pdf") },
});
// appsettings: AzureMailService:TenantId, ClientId, ClientSecret, FromAddress
```

### SMTP

```csharp
await mailService.SendSMTPAsync(new EmailRequest
{
    Recipients = ["destino@example.com"],
    Subject = "Asunto",
    Body = "Cuerpo en HTML",
});
// appsettings: SmtpClient:Host, Port, UserName, Password, EnableSsl, From, DisplayName, ReplyTo
```

---

## Archivos — `IBlobStorageService` (S3 / Azure Blob) e `ISharePointFileService`

Dos contratos separados — S3 y Azure Blob comparten la misma forma (bucket/container + key + bytes),
así que van bajo un único `IBlobStorageService` con dos implementaciones intercambiables. SharePoint
(Graph, drives, carpetas) es una forma distinta y queda en su propio `ISharePointFileService`.

### `IBlobStorageService` — elegir UNA implementación por app

```csharp
// Registro (Program.cs) — uno solo de los dos:
services.AddS3BlobStorage();      // appsettings: AWS:AccessKeyId, SecretAccessKey, BucketName, Region
services.AddAzureBlobStorage();   // appsettings: AzureBlob:ConnectionString, ContainerName
// Opcionales:
//   AWS:ServiceURL = https://<host>                -> endpoint S3-compatible que no es AWS (R2, MinIO):
//                                                     activa ForcePathStyle y cambia el formato de la
//                                                     url devuelta a {endpoint}/{bucket}/{key}
//   AzureBlob:CreateContainerIfNotExists = true   -> crea el contenedor en la primera escritura
//                                                    (default false: en prod lo crea la infra)
//   AzureBlob:ContainerPublicAccess      = "blob" -> lectura pública por URL directa
//                                                    (default privado; "container" no se habilita)

// Uso — igual sin importar cuál se registró:
string url = await blobStorage.UploadAsync(new FileInfo("avatar.png"), ct);       // desde FileInfo
string url = await blobStorage.UploadAsync(bytes, "avatars/user123.png", ct);     // desde byte[]
byte[] contenido = await blobStorage.DownloadAsync("avatars/user123.png", ct);
await blobStorage.DeleteAsync("avatars/user123.png", ct);
```

`UploadAsync` devuelve la URL pública del objeto (virtual-hosted-style en S3, la del SDK en Azure
Blob). `DownloadAsync` lanza `ElRoso.Exceptions.FileNotFoundException` si la key no existe — que el
middleware responde como **404** con su mensaje; `DeleteAsync` es idempotente.

> ⚠️ Es **nuestra** `FileNotFoundException`, no la de `System.IO`. Esa última no es excepción de
> dominio: una falla de `System.IO` leyendo el disco local sigue siendo un 500, como corresponde.

### `ISharePointFileService`

Soporta `FileInfo`, `IFormFile` y `byte[]` para los uploads.

```csharp
services.AddSharePointFileService(); // appsettings: AzureSharePoint:TenantId, ClientId, ClientSecret, Site, SubSite

// Desde FileInfo
await sharePointService.UploadFileSharePointAsync(new FileInfo("doc.pdf"), folderSharePoint: "Facturas");

// Desde IFormFile (upload desde controller)
await sharePointService.UploadFileSharePointAsync(formFile, folderSharePoint: "Adjuntos", newFileName: "doc.pdf");

// Desde byte[]
byte[] pdf = await GenerarPdfAsync();
await sharePointService.UploadFileSharePointAsync(pdf, "reporte.pdf", folderSharePoint: "Reportes");
```

---

## Telegram Bot

Soporte multi-bot con token encapsulado. Dos modos: **long-polling** (background service) o **webhook**.

### Registro

```csharp
services.AddTelegramBot("alertas", o => o.Token = configuration["Telegram:BotToken"]!);
services.AddTelegramBot("soporte", o => o.Token = configuration["Telegram:SupportBotToken"]!);

// Opcional: cuánto insiste el cliente de envío cuando Telegram rechaza (defaults abajo)
services.AddTelegramBot("alertas", o => o.Token = "...", r =>
{
    r.MaxRetryAttempts = 3;                              // 0 apaga el reintento
    r.BaseDelay = TimeSpan.FromSeconds(1);               // exponencial con jitter
    r.CircuitBreakerDuration = TimeSpan.FromSeconds(30);
});

services.AddSingleton<ITelegramUpdateHandler, MiTelegramHandler>();
services.AddTelegramPolling(); // omitir si usás webhooks
```

### Envío de mensajes

```csharp
public class NotificacionService(ITelegramBotFactory telegramBots)
{
    public async Task EnviarAsync(long chatId, CancellationToken ct)
    {
        var bot = telegramBots.Get("alertas");
        await bot.SendMessageAsync(chatId, "Factura generada ✓", ct: ct);
        await bot.SendDocumentAsync(chatId, "https://cdn.mi-api.com/factura.pdf", "Factura #1234", ct: ct);
    }
}
```

### Formato y botones

Sin `TelegramSendOptions` el mensaje sale como texto plano, igual que antes: el formato es opt-in.

```csharp
var teclado = new TelegramInlineKeyboard()
    .AddRow(TelegramInlineButton.Callback("Aprobar", "ok:7"),
            TelegramInlineButton.Callback("Rechazar", "no:7"))
    .AddRow(TelegramInlineButton.Link("Ver factura", "https://elroso.ar/f/7"));

await bot.SendMessageAsync(chatId, "*Factura 7* lista", new TelegramSendOptions
{
    ParseMode = TelegramParseMode.MarkdownV2,   // None (default), Markdown, MarkdownV2, Html
    DisableNotification = true,
    InlineKeyboard = teclado,
}, ct);
```

> ⚠️ Elegir un `ParseMode` hace que Telegram **rechace** el texto con markup mal formado: un `*` sin
> cerrar o un `_` suelto en un apellido es un 400, no un mensaje que llega feo. Si el texto lleva
> datos de la base, escapalo.

> Los botones se arman con `Callback` o `Link` — no hay constructor público porque Telegram rechaza
> el botón que lleva los dos, y eso lo puede evitar el compilador. El callback tiene un tope de 64
> bytes y falla donde se escribe, nombrando el botón.

### Cuando Telegram rechaza

| Qué pasa | Qué hace la librería |
| --- | --- |
| **429** (el bot manda muy rápido: ~30 msg/s) | Reintenta respetando el `retry_after` que manda Telegram. Si el freno no cede, tira **`TelegramRateLimitException`** con `RetryAfter` |
| **5xx** | Reintenta con backoff exponencial y jitter |
| **4xx que no sea 429** | **No** reintenta: un mensaje mal armado va a seguir mal |
| **Timeout** | **No** reintenta. `sendMessage` no es idempotente y no hay forma de distinguir "no llegó" de "llegó y se perdió la respuesta": repetirlo manda el mensaje dos veces |
| Telegram caído | El circuit breaker abre 30 s y los envíos fallan al instante, en vez de dejar colgado a todo el que llame |

> 🔴 **Envío y polling salen por clientes distintos** (`TelegramClientNames.Send` y `.Polling`). El
> pipeline de resiliencia corta cada intento a los 15 s y el long-poll espera 25 s a propósito:
> compartir un cliente mataría cada poll en plena espera. El cliente de polling no reintenta —
> el loop de polling ya es el reintento. **Un test que reemplaza el handler HTTP tiene que
> reemplazar los dos clientes.**

### Handler de updates entrantes

```csharp
public sealed class AlertasHandler(IServiceScopeFactory scopeFactory) : ITelegramUpdateHandler
{
    public string BotName => "alertas"; // debe coincidir con AddTelegramBot()

    public async Task HandleAsync(TelegramUpdate update, CancellationToken ct = default)
    {
        // update.IsFirstContact == true cuando el usuario envía /start
        // update.ChatId contiene el id a almacenar para mensajes salientes
        if (!update.IsFirstContact || update.ChatId is null) return;

        using var scope = scopeFactory.CreateScope();
        // almacenar update.ChatId.Value asociado al usuario en la DB
    }
}
```

### Webhook (sin long-polling)

```csharp
[HttpPost("telegram/webhook/{botName}")]
public async Task<IActionResult> WebhookAsync(
    string botName,
    [FromBody] TelegramUpdate update,
    [FromServices] IEnumerable<ITelegramUpdateHandler> handlers,
    CancellationToken ct)
{
    var handler = handlers.FirstOrDefault(h => h.BotName == botName);
    if (handler is not null)
        await handler.HandleAsync(update, ct);
    return Ok();
}
```

---

## Cron Jobs

```csharp
public class ReporteJob : CronJobService
{
    // Recibe la configuración registrada en AddCronJob — así la expresión cron vive en un solo lugar
    public ReporteJob(ILogger<ReporteJob> logger, IScheduleConfig<ReporteJob> config)
        : base(logger, config) { }

    public override Task DoWork(CancellationToken ct)
        => base.CatchExceptionAsync(async () => { /* lógica */ return Task.CompletedTask; });
}

// Registro
services.AddCronJob<ReporteJob>(o =>
{
    o.CronExpression = "0 8 * * 1-5";   // lunes a viernes 8am
    o.TimeZoneInfo = TimeZoneInfo.Local;
});
```

> ⚠️ **Usar el constructor que recibe `IScheduleConfig<T>`.** Con el constructor que toma la
> expresión cron como string, la que se le pasa a `AddCronJob` queda registrada en DI y después se
> **ignora**: parece que funciona y no funciona. Ese constructor sigue existiendo para no romper
> jobs viejos.

**Solapamiento:** si una ejecución dura más que el intervalo, la siguiente **se saltea** y queda un
Warning en el log (`Previous run is still in progress`). No se encola: dos corridas del mismo job
sobre las mismas filas es un problema de datos, y encolarlas solo posterga el choque.

**Expresión inválida:** `AddCronJob` la parsea al registrar, así que una expresión mal escrita
**rompe el arranque** en vez de fallar más tarde.

---

## Tests

```bash
dotnet test ElRoso.Commons.sln
```

El CI exige un mínimo de 60 % de cobertura de líneas. Un solo proyecto (`Tests/ElRoso.Commons.Tests.csproj`)
cubre todos los paquetes.

---

## Contribución

- Comentarios en español, una línea, que dicen qué hace el código
- Un paquete tocado sube su `<Version>` y suma su entrada a `CHANGELOG.md` (lo valida `eng/check-version-bump.py` en el PR)
- Sin nombres de producto, cliente ni red internos en código, doc XML, ejemplos ni documentación
- Namespaces: `ElRoso.<Capa>.<SubCapa>` (ej: `ElRoso.Providers.SQL`)
- Seguir los patrones de performance y las reglas de `CLAUDE.md`
- Ejecutar `dotnet test` antes de hacer PR — todos los tests deben pasar
