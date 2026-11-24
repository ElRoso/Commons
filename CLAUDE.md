# CLAUDE.md

## Descripción General

Librería base para backends ASP.NET Core Web API, publicada en nuget.org como 15 paquetes por área
(MIT). Repo: `https://github.com/ElRoso/Commons`, rama `mainline`.

| Campo | Valor |
| --- | --- |
| Solución | `ElRoso.Commons.sln` |
| Framework | .NET 10.0 (LTS), `net10.0` en todos los paquetes |
| Datos | EF Core 10 (sin proveedor de base de datos), MongoDB (driver 3.9) |
| Autenticación | JWT Bearer / Azure AD |
| Errores | Middleware RFC 7807 o `Result` + Problem Details RFC 9457 |
| Validación | FluentValidation |
| Logs | Serilog (consola, archivo, Seq, Graylog) |
| Hash de passwords | BCrypt.Net-Next (work factor configurable, default 12) |
| Cron jobs | `CronJobService` (`IHostedService` + Cronos) |
| Mails | Microsoft Graph / SMTP |
| Archivos | `IBlobStorageService` (S3 o Azure Blob: un contrato, dos implementaciones, se elige una) + `ISharePointFileService` (contrato aparte) |
| Telegram | Multi-bot (named factory), `ITelegramNotifier`, reintentos y circuit breaker con Polly |

---

## Comandos

```bash
dotnet build ElRoso.Commons.sln -c Release          # build
dotnet test ElRoso.Commons.sln                      # todos los tests
dotnet test Tests/ElRoso.Commons.Tests.csproj --filter "FullyQualifiedName~AddAsync"   # uno por nombre
dotnet test Tests/ElRoso.Commons.Tests.csproj --collect:"XPlat Code Coverage"         # cobertura
dotnet pack ElRoso.Commons.sln -c Release --output ./nupkg                            # los 15 .nupkg y .snupkg
python3 eng/verify-pack.py ./nupkg --all            # licencia, readme, repositorio y símbolos de cada paquete
python3 eng/license-gate.py src/*/*.csproj          # gate de licencias (después de dotnet restore)
python3 eng/check-version-bump.py <ref-base> HEAD   # guarda de versión: cada paquete tocado sube su <Version>
python3 -m unittest discover -s eng -p 'test_*.py'  # tests de los scripts de eng/
dotnet format ElRoso.Commons.sln                    # formato completo (whitespace + style + analyzers)
dotnet format whitespace ElRoso.Commons.sln --verify-no-changes   # lo que valida el hook pre-commit
```

**Git hooks (Husky.Net):** `pre-commit` corre `dotnet format whitespace --verify`, build y tests
(`.husky/task-runner.json`). Se instalan solos en el primer `dotnet build`; a mano: `dotnet tool restore
&& dotnet husky install`. Saltear: `HUSKY=0` (el CI lo setea).

---

## CI/CD y versionado

Workflows de GitHub Actions en `.github/workflows/`:

| Workflow | Cuándo | Qué hace |
| --- | --- | --- |
| `ci.yml` | PR y push a `mainline` | Tests de `eng/`, guarda de versión (solo PR), restore, gate de licencias, build, tests con cobertura (mínimo 60 % de líneas), smoke de `samples/Minimal`, smoke OpenAPI → TypeScript (`npx --yes openapi-typescript@7.13.0` sobre `samples/OpenApi/openapi/*.json`), y job `pack-verify` (pack de los 15 + `verify-pack.py`) |
| `tag-on-version-bump.yml` | Push a `mainline` que toca `src/*/*.csproj` | Crea el tag `<PackageId>/vX.Y.Z` de cada `<Version>` sin tag y llama a `release.yml` con esos paquetes |
| `release.yml` | Llamado por el anterior o `workflow_dispatch` (`packages`: ids o `all`) | Gate de licencias, build, tests, pack, `verify-pack.py`, publicación en nuget.org y un GitHub Release por paquete |
| `audit.yml` | Lunes 08:00 AR y a mano | `dotnet list package --vulnerable --include-transitive`; avisa por Telegram si hay High/Critical o no pudo escanear (secrets `TELEGRAM_NOTIFIER_TOKEN` y `TELEGRAM_NOTIFIER_RECIPIENT_IDS`; sin ellos el job falla con un warning) |

**Versionado por paquete, no lockstep.** La `<Version>` fija de cada csproj es la fuente de verdad:
el metapaquete `ElRoso.Commons` arrancó en `2027.0.0` y los paquetes por área en `1.0.0`; después SemVer. La
versión vigente de cada uno es su `<Version>`, no un número escrito acá. Tags:
`<PackageId>/vX.Y.Z` (por ejemplo `ElRoso.Commons.Core/v1.0.0`). Un cambio en `src/<PackageId>/` exige
subir su `<Version>`; un cambio en `src/Directory.Build.props` exige subir todos; un `PackageVersion`
agregado, borrado o cambiado en `Directory.Packages.props` exige subir los paquetes que lo referencian,
directo o por `ProjectReference`; si ningún csproj del repo lo referencia (un pin de seguridad) o el cambio
no es de un `PackageVersion`, exige subir todos. Lo cobra `eng/check-version-bump.py` en el PR. Un `CHANGELOG.md` por versión: ver abajo.

**Publicación sin secretos:** Trusted Publishing (OIDC) de nuget.org con `NuGet/login`. No hay API key
guardada. `--skip-duplicate` omite versiones ya publicadas.

**Gate de licencias** (`eng/license-gate.py`, lista en `eng/allowed-licenses.json`): solo MIT,
Apache-2.0, BSD-2-Clause y BSD-3-Clause, más excepciones por paquete y versión con su motivo. Una
dependencia nueva con otra licencia rompe el CI y el release.

**`verify-pack.py`** exige, por cada `.nupkg`: su `.snupkg`, licencia MIT, readme y repositorio
`https://github.com/ElRoso/Commons`.

- 🔴 **Un tag pusheado con `GITHUB_TOKEN` no dispara otros workflows.** Por eso `tag-on-version-bump.yml`
  llama a `release.yml` directamente (`uses: ./.github/workflows/release.yml`) en vez de esperar un
  `on: push: tags`. No mover la publicación a un trigger por tag.
- `CHANGELOG.md` lleva un `## ` por `<PackageId>` y versión (ver "Changelog"). Si falta, el tag se crea
  igual y el workflow avisa con un warning.

---

## Esta librería no es de ningún producto

La consumen varios backends y puede ir a proyectos de distintos clientes, y el repo es público.

| Dónde | Regla |
| --- | --- |
| Doc XML (`///`), README, ejemplos | Sin nombres de producto, cliente, red ni herramienta interna. Bots de ejemplo: `"alertas"`, `"soporte"`. El XML viaja dentro de cada paquete y lo lee el IntelliSense de todo consumidor |
| Comentarios internos (`//`), tests | Dicen qué restringe el código, no dónde se midió ni qué incidente lo originó. Eso va al commit o a la documentación privada |
| Configuración de ejemplo | Valores genéricos; nunca un secreto, un host ni una red reales |

Nada en el código está atado a un producto: los nombres de bot, rutas y demás son parámetros.

---

## Estructura

```text
/
├── src/                          # un csproj por paquete
│   ├── ElRoso.Commons/                  # metapaquete + ElRosoApplicationBuilder
│   ├── ElRoso.Commons.Core/             # excepciones, Result/Error, DTOs, respuestas, EncryptionHelper (BCrypt), CronJobService, helpers, contratos (IMailService, IBlobStorageService, ITelegramNotifier)
│   ├── ElRoso.Commons.AspNetCore/       # middlewares, forwarded headers, health check, CORS, controllers, versionado de API, JWT
│   ├── ElRoso.Commons.Serilog/          # AddElRosoSerilog, sinks, SeqSinkSettings
│   ├── ElRoso.Commons.Swagger/          # Swagger / OpenAPI, una doc por versión
│   ├── ElRoso.Commons.OpenApi/          # OpenAPI nativo, una doc por versión, esquemas Bearer/Basic
│   ├── ElRoso.Commons.EntityFrameworkCore/  # ElRosoRepositoryBase<T>, BaseEntity, AddMemoryCacheCustom, DbCacheConfigurationCheck
│   ├── ElRoso.Commons.MongoDB/          # Repository<T>, AuditEntity, AddMongoDb
│   ├── ElRoso.Commons.AzureAD/          # AddAuthenticationAzureAD
│   ├── ElRoso.Commons.Graph/            # MailService (Graph / SMTP), AddMailService
│   ├── ElRoso.Commons.SharePoint/       # ISharePointFileService, AddSharePointFileService
│   ├── ElRoso.Commons.S3/               # S3BlobStorageService, AddS3BlobStorage
│   ├── ElRoso.Commons.AzureBlob/        # AzureBlobStorageService, AddAzureBlobStorage
│   ├── ElRoso.Commons.Telegram/         # bots, polling, TelegramNotifier, resiliencia
│   └── ElRoso.Commons.WindowsServices/  # UseWindowsServiceCustom
├── Tests/ElRoso.Commons.Tests.csproj    # xUnit + Moq, un solo proyecto para todos los paquetes
├── samples/Minimal/                     # consumidor mínimo (Core + AspNetCore + Serilog + EntityFrameworkCore)
├── eng/                                 # scripts de CI en Python: license-gate, verify-pack, check-version-bump
├── Directory.Build.props                # NuGetAudit
├── src/Directory.Build.props            # metadatos y compilación comunes de los paquetes
└── Directory.Packages.props             # versiones centralizadas y pins de seguridad
```

Dependencias entre paquetes: todos dependen de `Core`, salvo `AzureAD`, `Swagger` y `OpenApi`, que dependen de
`AspNetCore`. El metapaquete referencia los otros 14. `PackageBoundaryTests` vigila que cada tipo viva en el paquete de su área y que los paquetes base
no arrastren SDKs de terceros (MongoDB, Graph, AWS, Azure, Identity.Web, SqlClient, Polly, WindowsServices).

Un paquete nuevo es un `src/<PackageId>/<PackageId>.csproj` con su `<Version>`; lo recogen solos el CI,
la guarda de versión, `release.yml` (`all`) y `verify-pack.py --all`.

---

## Bootstrap de la aplicación

El host se arma por módulos con `AddX` / `UseX`; cada paquete aporta los suyos.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddElRosoConfiguration(args);   // AspNetCore: appsettings, appsettings.{env}, user-secrets (solo Development), env y args
builder.AddElRosoSerilog();             // Serilog

builder.Services.AddElRosoForwardedHeaders(builder.Configuration);   // detrás de un reverse proxy
builder.Services.AddEncryptionHelper(builder.Configuration);         // Core: obligatorio si usa BCrypt
builder.Services.AddMemoryCacheCustom();                             // EntityFrameworkCore
builder.Services.AddMongoDb(builder.Configuration);                  // MongoDB: obligatorio si usa MongoDB
builder.Services.AddMailService();                                   // Graph
builder.Services.AddHealthChecks();
builder.Services.AddExceptionHandlerMiddleware();
// Alternativa: builder.Services.AddElRosoProblemDetails();   // reemplaza al middleware RFC 7807
builder.Services.AddCurrentUserMiddleware();
builder.Services.AddControllersCustom(builder.Configuration);
builder.Services.AddAuthenticationJWT(builder.Configuration);        // o AddAuthenticationAzureAD (AzureAD)

var app = builder.Build();

app.UseElRosoForwardedHeaders();   // PRIMERO en el pipeline
app.UseExceptionHandlerMiddleware();
// Alternativa: app.UseElRosoProblemDetails();   // montar uno solo, nunca ambos
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseCurrentUserMiddleware();    // después de la autorización, antes de los endpoints
app.MapControllers();
app.MapHealthChecksCustom();       // /healthz
app.MapElRosoInfo(app.Environment);   // GET / informativo
app.Run();
```

`samples/Minimal/Program.cs` es el consumidor mínimo compilado en el CI.

**`ElRosoApplicationBuilder.CreateBuilder` y `UseElRoso` (metapaquete) siguen existiendo `[Obsolete]`**
para no romper a los consumidores anteriores: un consumidor que los usa ve el warning CS0618. Arman el host
entero de una vez (configuración, Serilog, mails, versionado de API, CORS, controllers, middlewares y,
en `UseElRoso`, Swagger y archivos estáticos). Orden de `UseElRoso`:
ExceptionHandler, HSTS + HTTPS (solo si `UseHttps=true`), Swagger, Static Files, Cookie Policy,
Routing, Request Localization, CORS, Authentication + Authorization, Response Caching,
`beforeEndpoints`, Endpoints (`MapControllers` + `GET /`).

> 🔴 **`CurrentUserMiddleware` no se monta solo.** Un middleware agregado después de `UseEndpoints`
> nunca se ejecuta; uno montado antes de `UseAuthentication` pasa de largo porque el usuario todavía no
> está autenticado. Si no se monta bien, `CurrentUser` es `null` y la auditoría graba
> `CreatedBy`/`ModifiedBy` en `null` sin fallar ni loguear. Con `UseElRoso` se pide por
> `beforeEndpoints: inner => inner.UseCurrentUserMiddleware()`. No se monta por defecto a propósito:
> responde 401 si falta cualquiera de los cuatro claims de Azure AD.

> 🔒 **El endpoint de health check no publica la descripción de un chequeo fallado tal cual:** ASP.NET
> Core mete ahí el mensaje de la excepción (un connection string con su contraseña, un host interno).
> Las descripciones escritas a mano se publican; las que vienen de una excepción se reemplazan por un
> texto que remite a los logs. Responde 200 si está sana y 503 si no.

---

## La dirección de quien llama (`ForwardedHeadersSetup`)

Detrás de un reverse proxy, `Connection.RemoteIpAddress` es la dirección del proxy. Con
`AddElRosoForwardedHeaders` + `UseElRosoForwardedHeaders` (primero en el pipeline) pasa a ser la del
cliente. **Nadie debe parsear `X-Forwarded-For` a mano.**

| Error (silencioso) | Qué provoca |
| --- | --- |
| No aplicarlo | Todos los que llaman parecen una máquina: un rate limiter por dirección mete a todo el mundo en un solo cupo |
| Leer la primera entrada del header | El proxy appendea; un cliente que manda su propio `X-Forwarded-For` elige la dirección que queda registrada |

- `ForwardLimit = 1` lee el último salto, el que escribió el proxy propio. No es una perilla de tuning: es la propiedad de seguridad.
- El default de redes confiables es loopback + RFC 1918. Si el proxy está en otro rango, setear `ForwardedHeaders:KnownNetworks` (CIDR separados por coma); con los defaults el proxy no es confiable, el header se ignora y se ve idéntico a no haber llamado a esto.
- 🔴 **Una entrada ilegible en `KnownNetworks` rompe el arranque** con `InvalidOperationException` que nombra el valor; no se saltea. Ausente o en blanco cae a los defaults, a propósito. La validación corre al registrar.

## 🔴 El logger del framework que escribía tokens en el log

`ElRosoLoggerConfiguration` silencia `Microsoft.AspNetCore.Hosting.Diagnostics` a Warning: ese logger
escribe la url cruda en Information en cada request y SignalR lleva el JWT en el query string.

- ⛔ **No sacar el `MinimumLevel.Override` ni ampliarlo a `"Microsoft"`.** Sacarlo devuelve la fuga; ampliarlo esconde el log de comandos de EF Core y de los handlers de auth.
- 🔴 **Tiene que estar en la librería y no en el consumidor.** `AddElRosoSerilog` (y `UseSerialLogCustom`) hace `ClearProviders()` e instala el logger ya construido y hardcodea `MinimumLevel.Debug()` sin `ReadFrom.Configuration`: las reglas de filtro de `Microsoft.Extensions.Logging` del consumidor nunca corren. Una supresión escrita en el consumidor parece aplicada y no hace nada.
- ⚠️ Una app sin request logging propio, con el query string saneado, se queda sin log de requests.

## 🔴 El sink de Seq lleva API key

`SEQ:ApiKey` viaja al sink (`SeqSinkSettings.Read`). Es opcional (un Seq anónimo no la necesita), y por eso su ausencia no se detecta sola.

- 🔴 Un Seq con autenticación rechaza la ingesta anónima con 401 y `Serilog.Sinks.Seq` se traga el error: la app loguea normal y el servidor no recibe un solo evento.
- Vacía y ausente son lo mismo: "sin key". Una variable en blanco no se manda tal cual.
- Una sección `SEQ` sin `ServerURL` no registra sink.
- El sink manda `Warning` para arriba: un `Information` que no aparece en Seq no es este bug.

---

## Decisiones de performance

### SQL — EF Core

| Patrón | Razón |
| --- | --- |
| `AsNoTracking()` en todas las lecturas | Aplicado query por query en `ElRosoRepositoryBase` |
| `QuerySplittingBehavior.SplitQuery` | Evita cartesian explosion; lo configura cada consumidor en su `DbContext` |
| `ExecuteUpdateAsync` para updates de 1-2 campos | 3 round trips → 1. No está en `ElRosoRepositoryBase`: se usa desde el DAO derivado sobre `Context`, invalidando la cache a mano |
| Proyecciones con `Select` | Traer solo los campos necesarios |
| `Task.WhenAll` para count + data | `GetPageAsync` ejecuta ambas en paralelo |
| `OrderBy` antes de `Skip/Take` | Sin él, SQL Server no garantiza orden entre páginas |
| `Take(MaxRecordsLimit)` en `GetAsync()` sin filtro | Tope de 10.000 contra OOM |

El paquete no depende de ningún proveedor de base de datos: lo elige el consumidor.

### MongoDB

| Patrón | Razón |
| --- | --- |
| `IMongoClient` e `IMongoDatabase` Singleton | `MongoClient` gestiona el pool; jamás instanciarlo por repositorio |
| `ReplaceOneAsync` para updates | Delete+Insert no era atómico |
| `BulkWriteAsync` ordenado para N documentos | Un viaje en vez de N; ordenado a propósito. Con lista vacía no llama a la base |
| `DeleteManyAsync` con filtro `In` | Una query en vez de N |
| `GetAsync()` topea en `MaxRecordsLimit` (10.000) | Pide un documento de más para saber que cortó y avisa en Warning |
| `GetPageAsync` ordena por `Audit.CreationDate` | ⚠️ Quiere un índice: un orden sin índice se resuelve en memoria y Mongo lo aborta pasados los 32 MB |
| `AnyAsync` es `Limit(1)` | El servidor para en la primera coincidencia |
| API completamente `async` | La síncrona bloqueaba threads del pool |

### Cifrado

`EncryptionHelper` es Singleton (BCrypt se configura una vez; work factor de appsettings). Los valores
JWT se cachean en el constructor.

### Cache — `ElRosoRepositoryBase`

`GetAsync()` sin filtro usa doble-check locking con `ConcurrentDictionary<string, SemaphoreSlim>` por
clave (evita thundering herd). Toda escritura invalida la cache. 20 min absoluto, 2 min sliding.

### Registraciones

Por paquete: `AddEncryptionHelper` (Core, obligatorio con BCrypt), `AddMongoDb` (MongoDB, obligatorio con
MongoDB), `AddMailService` (Graph), `AddMemoryCacheCustom` (EntityFrameworkCore), `AddS3BlobStorage` o
`AddAzureBlobStorage` (**uno solo**: los dos registran `IBlobStorageService`), `AddSharePointFileService`,
`AddTelegramBot("nombre", o => o.Token = ...)`, `AddTelegramPolling`, `AddTelegramNotifier(configuration)`
(lee `Telegram:Notifier:Token` y `Telegram:Notifier:RecipientIds` separados por `;`; si faltan registra
`NullTelegramNotifier` y la app arranca igual). Claves de configuración que lee cada uno: `README.md`.

### Contenedor de Azure Blob

`AzureBlobStorageService` no crea el contenedor por defecto: sin él, cada subida falla con
`ContainerNotFound` (404). Es deliberado: en producción lo crea la infraestructura.

| Setting | Default | Para qué |
| --- | --- | --- |
| `AzureBlob:CreateContainerIfNotExists` | `false` | `true` lo crea en la primera escritura; pensado para desarrollo (Azurite) |
| `AzureBlob:ContainerPublicAccess` | privado | Solo `"blob"` (case-insensitive) habilita lectura pública por URL; cualquier otro valor, incluido `"container"`, cae a privado |

`"blob"` no permite listar el contenedor: conviene que las keys lleven un GUID. El default es privado
porque el modo de fallar es asimétrico: un contenedor que debía ser público y no lo es se nota enseguida;
uno público sin querer no se nota nunca.

---

## Configuración (`appsettings.json`)

`AddElRosoConfiguration` borra las fuentes del host y carga, en orden: `appsettings.json`,
`appsettings.{env}.json`, user-secrets del assembly de la app (solo Development), variables de entorno y
argumentos. Las claves de dev van a `dotnet user-secrets`, nunca al `appsettings.Development.json`
versionado.

```json
{
  "ConnectionStrings": { "MongoDB": "mongodb://localhost:27017" },
  "MongoDb": { "DatabaseName": "ElRosoDB" },
  "JWT": { "Issuer": "...", "Audience": "...", "IssuerSigningKey": "..." },
  "Encryption": { "WorkFactor": 12 },
  "ApplicationDisplayName": "MiBackend",
  "DbCacheIsEnabled": true,
  "DbCacheReplicas": 1
}
```

> 🔴 **Nunca usar `ApplicationName` para el nombre de la app: es una clave reservada del host.** Por
> línea de comandos hace que el host busque un assembly con ese nombre y la app no arranca (el error real
> aparece al registrar los controllers, como un `FileNotFoundException` que no menciona la clave). El
> nombre visible va en `ApplicationDisplayName` (`ApplicationDisplayName.Resolve`, en Core). La reservada
> se sigue leyendo como respaldo.

> ℹ️ **`AWS:ServiceURL` (opcional) apunta el blob storage a un endpoint S3-compatible que no es AWS**
> (R2, MinIO). Activa además `ForcePathStyle` y cambia la url de `UploadAsync` a `{endpoint}/{bucket}/{key}`.
> ⚠️ Van juntos y ninguno sirve solo: sin path-style el SDK arma urls virtual-hosted que esos proveedores
> no resuelven, y la falla llega como error de DNS al primer llamado. `AWS:Region` se sigue necesitando
> para firmar.

> 🔴 **La cache es por proceso y `DbCacheReplicas` es cómo el despliegue lo declara.** Con más de una
> réplica cada una cachea por su cuenta y una escritura solo invalida la suya: datos rancios hasta 20
> minutos. `AddMemoryCacheCustom()` registra `DbCacheConfigurationCheck`: **Warning** al arrancar si la
> cache está encendida con `DbCacheReplicas > 1`, **Information** con una sola réplica.

> ⚠️ **El fallback abierto de CORS no rompe el arranque.** Sin `Cors:AllowedOrigins` (ausente o vacío),
> `AddCorsCustom` permite cualquier origen y fuera de Development solo avisa en Warning; no lanza. Un
> despliegue que olvida la clave arranca abierto.

---

## Tests

- xUnit v2 + Moq (versiones en `Directory.Packages.props`), un solo proyecto (`Tests/ElRoso.Commons.Tests.csproj`) para todos los paquetes.
- El CI exige 60 % de cobertura de líneas.
- `EncryptionHelper` en tests: `static readonly` con work factor bajo (4), pagando BCrypt una vez por clase.

```csharp
private static readonly EncryptionHelper Sut = new(workFactor: 4);
```

---

## Reglas críticas al modificar código

1. **Nunca instanciar `MongoClient` dentro de un repositorio**: inyectar `IMongoDatabase`.
2. **Nunca instanciar `EncryptionHelper` por request**: inyectar el Singleton.
3. **Nunca llamar `GetAsync()` sin filtro sobre tablas o colecciones grandes**: `GetPageAsync` / `GetAllInPaginationAsync` (SQL), `GetPageAsync` / `GetAsync(filtro)` (Mongo). Los dos topean en 10.000 y avisan en Warning.
4. **Updates de 1-2 campos con `ExecuteUpdateAsync`** desde el DAO derivado, invalidando la cache a mano (`this.memoryCache.Remove(this.KeyNameCache)`): la base no se entera de esa escritura.
5. **Toda operación NoSQL es `async`.**
6. **`GetAllInPaginationAsync` y `GetPageAsync` requieren `OrderBy`**: no quitarlo en overrides.
7. **La librería no incluye `UserEntity`, `UserDAO` ni `UserService`**: cada consumidor define el suyo extendiendo `BaseEntity` y `ElRosoRepositoryBase<T>`.
8. **No re-agregar `AutoMapper`**: nunca se usó y desde la v15 es de licencia comercial. Una dependencia que se agrega acá la heredan todos los consumidores, con su licencia; el gate de licencias lo vigila.
9. **Nunca usar `CustomRandom` con `Random`**: sale de `RandomNumberGenerator`. Para contraseñas, `CustomRandom.Password(int length)`; el `Password()` sin parámetros es `[Obsolete]` (~38 bits de entropía).
10. ⛔ **`ExceptionHandlerMiddleware` no vacía el `IMemoryCache`: no re-agregarlo.** Hay un solo `IMemoryCache` por proceso y los consumidores guardan ahí contadores de seguridad (throttle de login, permission stamps): un borrado global convertía cualquier 500 sin autenticar en un reset del freno a la fuerza bruta. Invalidar una entrada es tarea de quien la escribe. El constructor con `IMemoryCache` sigue `[Obsolete]` solo por compatibilidad e ignora la cache.
11. **`ExceptionHandlerMiddleware` y `DomainExceptionHandler` loguean una excepción de dominio en Warning y una desconocida en Error con stack trace.** No subir las de dominio a Error: un 404 o un 400 no es una falla del servidor.
12. **`AddControllersCustom` apaga el filtro de ModelState (`SuppressModelStateInvalidFilter = true`)**: la validación es FluentValidation + `BadRequestException`. El body ilegible lo corta `UnreadableBodyFilter` (400 RFC 7807, errores de ModelState en `details`); sin él, `[FromBody]` llega null y termina en 500.
13. 🔴 **`Microsoft.Extensions.*`, ASP.NET Core, EF Core y `System.Security.Cryptography.*` se quedan en la familia de net10.0 (`10.0.x`).** No subirlos a otra línea mayor aunque exista una versión nueva.
14. 🔴 **`StyleCop.Analyzers` queda en `1.1.118`: la serie 2.x depreca reglas que este repo usa.** No actualizarlo.
15. **Un paquete tocado sube su `<Version>` en el mismo PR** y suma su entrada al `CHANGELOG.md` (guarda `eng/check-version-bump.py`).

---

## Telegram — envío, polling y resiliencia

**Son DOS clientes HTTP nombrados** (`TelegramClientNames.Send` y `.Polling`): enviar y hacer long-polling
quieren cosas opuestas.

| | `Send` | `Polling` |
| --- | --- | --- |
| Reintentos | 429 y 5xx, backoff exponencial + jitter | ninguno: el loop ya es el reintento |
| Timeout por intento | 15 s (pipeline de resiliencia) | 60 s de cliente, para un long poll de 25 s |
| Circuit breaker | sí, 30 s | no |
| Logging por defecto | quitado (el token va en la URI) | quitado, por lo mismo |

> 🔴 **No unificar los dos clientes.** El handler de resiliencia corta cada intento a los 15 s y el long
> poll espera 25 s: con uno solo, cada poll muere en plena espera y el síntoma es "el polling anda lento".

> ⚠️ **Un test que reemplaza el handler HTTP tiene que reemplazar los dos**, o el que quede sale a
> Telegram de verdad.

**No se reintenta un timeout:** `sendMessage` no es idempotente y "no llegó" no se distingue de "llegó y
se perdió la respuesta". Un 4xx que no sea 429 tampoco: el mensaje está mal y va a seguir mal.

**429:** Telegram limita a ~30 msg/s e informa cuánto esperar (`parameters.retry_after` y normalmente el
header). Ese número le gana al backoff propio. Si el freno no cede sale `TelegramRateLimitException` con
`RetryAfter`: no se traga, porque tragarlo pierde el mensaje en silencio.

**Formato:** `TelegramSendOptions` (parse mode, notificación silenciosa, teclado inline). Sin opciones, texto
plano. Los botones se arman con `TelegramInlineButton.Callback` / `.Link`: Telegram rechaza el que lleva
ambos.

---

## Backlog

El backlog vive en el board del proyecto (work items citados por `#ID`), no en este repo. La deuda que se
liste acá cita su `#ID`. Un backlog viejo lista cosas hechas: verificar contra el código antes de cargarlo.

## Seguridad de dependencias

`Directory.Build.props` activa `NuGetAudit` en modo `all` con `NU1903` (HIGH) y `NU1904` (CRITICAL) como
**error**: un advisory HIGH o CRITICAL, directo o transitivo, rompe el restore. Se pinnea el paquete
transitivo a la versión parcheada en `Directory.Packages.props` (comentario de una línea con los GHSA que
cierra; `CentralPackageTransitivePinningEnabled`).

- 🔴 **Salida única ante un HIGH sin parche:** `NuGetAuditSuppress` sobre ese advisory, con el `#ID` del work item que lo sigue. Nada de bajar el gate ni de silenciar `NU1903`/`NU1904`.
- 🔴 **Un pin transitivo que el framework de ASP.NET Core poda no viaja en el nuspec.** `Cryptography.Xml` lo provee el framework y se poda en este build, pero un consumidor sin ASP.NET Core (un worker) lo resuelve en la versión vieja y rompe su restore. Una referencia directa tampoco alcanza: también se poda. Por eso `AzureAD` (que lo arrastra por Identity.Web → DataProtection) tiene `RestoreEnablePackagePruning=false` y el pin viaja como dependencia. Un paquete nuevo que arrastre un paquete del framework con pin va igual; se verifica restaurando un worker (`Microsoft.NET.Sdk.Worker`) contra el pack local.

| Pin activo | Versión | Cierra |
| --- | --- | --- |
| Familia `Microsoft.Kiota` (7 paquetes) | 2.2.0 | CVE-2026-44503 (GHSA-7j59-v9qr-6fq9): fuga de headers `Cookie`/`Proxy-Authorization` en redirects cross-host; la familia 2.x incluye el fix |
| `Microsoft.OpenApi` | 2.13.1 | GHSA-v5pm-xwqc-g5wc: referencia circular de schema corta el parseo; piso de `Microsoft.AspNetCore.OpenApi` 10.0.15 |
| `System.Security.Cryptography.Xml` | 10.0.15 | GHSA-r955-8q5j-3h83, GHSA-7hqp-7c5m-4vq6, GHSA-c399-9853-6jmj |
| `System.Security.Cryptography.Pkcs` | 10.0.15 | GHSA-846f-f764-76mx |

🔴 **La auditoría semanal** (`audit.yml`) corre aunque no haya commits: el gate solo actúa cuando cambia el
grafo. Necesita los secrets `TELEGRAM_NOTIFIER_TOKEN` y `TELEGRAM_NOTIFIER_RECIPIENT_IDS`.

⚠️ `dotnet list package` restaura solo (SDK 10) y con el gate el restore corta antes del listado. Verificar con
`dotnet list ElRoso.Commons.sln package --vulnerable --include-transitive --no-restore`, después de un `dotnet restore`.

## Changelog

`CHANGELOG.md` en la raíz, un `## ` por paquete y versión, con el formato `## <PackageId> <versión>`
(por ejemplo `## ElRoso.Commons.Core 1.0.0`). `tag-on-version-bump.yml` busca una línea `## ` que contenga
el `PackageId` y la versión.
