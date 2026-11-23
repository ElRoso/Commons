namespace ElRoso.Commons.Tests;

using ElRoso.Commons;
using Microsoft.Extensions.Configuration;

/// <summary>
/// EN: What the Seq sink is told to do, read off configuration. It is a separate readable piece
///     because the sink itself cannot be inspected once built: a logger with a misconfigured Seq
///     sink looks exactly like a working one, and the only symptom is that nothing ever arrives.
/// ES: Qué se le dice al sink de Seq, leído de la configuración. Es una pieza aparte y legible
///     porque al sink ya construido no se le puede preguntar nada: un logger con el sink de Seq mal
///     configurado se ve idéntico a uno que anda, y el único síntoma es que nunca llega nada.
/// </summary>
public class SeqSinkSettingsTests
{
    private static IConfiguration ConfigFrom(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    [Fact]
    public void WithNoSeqSection_ThereIsNoSink()
    {
        Assert.Null(SeqSinkSettings.Read(ConfigFrom(new() { ["ApplicationName"] = "Tests" })));
    }

    /// <summary>
    /// EN: 🔴 The API key is OPTIONAL: a Seq that ingests anonymously needs none, and demanding one
    ///     would silence every deployment that never had it.
    /// ES: 🔴 La API key es OPCIONAL: un Seq que ingesta anónimo no necesita ninguna, y exigirla
    ///     callaría todo despliegue que nunca la tuvo.
    /// </summary>
    [Fact]
    public void WithAServerUrlAndNoApiKey_TheSinkStillShipsWithoutOne()
    {
        var settings = SeqSinkSettings.Read(ConfigFrom(new() { ["SEQ:ServerURL"] = "http://seq" }));

        Assert.NotNull(settings);
        Assert.Equal("http://seq", settings!.ServerUrl);
        Assert.Null(settings.ApiKey);
    }

    /// <summary>
    /// EN: 🔴 The case this whole piece exists for. A Seq with authentication turned on answers
    ///     401 "A valid API key is required for writing events" to every batch, and
    ///     Serilog.Sinks.Seq swallows that: the app logs normally, the sink is registered, the
    ///     network is fine, and the server stays empty forever. Production ran that way — the
    ///     first thing anybody reached for during an incident had nothing in it.
    /// ES: 🔴 El caso por el que existe toda esta pieza. Un Seq con autenticación prendida contesta
    ///     401 "A valid API key is required for writing events" a cada lote, y Serilog.Sinks.Seq se
    ///     lo traga: la app loguea normal, el sink está registrado, la red anda, y el servidor
    ///     queda vacío para siempre. Producción corrió así — lo primero que alguien abre en un
    ///     incidente no tenía nada adentro.
    /// </summary>
    [Fact]
    public void WithAnApiKey_ItTravelsToTheSink()
    {
        var settings = SeqSinkSettings.Read(ConfigFrom(new()
        {
            ["SEQ:ServerURL"] = "http://seq",
            ["SEQ:ApiKey"] = "abc123",
        }));

        Assert.Equal("abc123", settings!.ApiKey);
    }

    /// <summary>
    /// EN: A blank key is somebody who set the variable and left it empty — that is "no key", not a
    ///     key made of spaces. Sending it verbatim would turn an anonymous Seq into a 401 too.
    /// ES: Una key en blanco es alguien que seteó la variable y la dejó vacía — eso es "sin key", no
    ///     una key hecha de espacios. Mandarla tal cual convertiría en 401 también a un Seq anónimo.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WithABlankApiKey_ItIsTreatedAsAbsent(string apiKey)
    {
        var settings = SeqSinkSettings.Read(ConfigFrom(new()
        {
            ["SEQ:ServerURL"] = "http://seq",
            ["SEQ:ApiKey"] = apiKey,
        }));

        Assert.NotNull(settings);
        Assert.Null(settings!.ApiKey);
    }

    /// <summary>
    /// EN: A section with no URL is not a sink: Serilog would take the empty address and drop every
    ///     event into it, which reads exactly like a Seq that is up and receiving nothing.
    /// ES: Una sección sin URL no es un sink: Serilog tomaría la dirección vacía y tiraría ahí cada
    ///     evento, que se lee exactamente igual que un Seq levantado y sin recibir nada.
    /// </summary>
    [Fact]
    public void WithASectionButNoServerUrl_ThereIsNoSink()
    {
        Assert.Null(SeqSinkSettings.Read(ConfigFrom(new() { ["SEQ:ApiKey"] = "abc123" })));
    }
}
