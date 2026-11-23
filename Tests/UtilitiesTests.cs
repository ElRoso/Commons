namespace ElRoso.Commons.Tests;

using ElRoso.Commons;
using ElRoso.Domains.Responses;
using ElRoso.Helpers;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text;

public class UtilitiesTests
{
    // ── ObjectExtension ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("texto", true)]
    [InlineData("", false)]
    public void HasValue_ForStrings_TreatsEmptyAsNoValue(string value, bool expected)
    {
        Assert.Equal(expected, value.HasValue());
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, true)]
    public void HasValue_ForIntegers_TreatsZeroAsNoValue(int value, bool expected)
    {
        Assert.Equal(expected, value.HasValue());
    }

    [Fact]
    public void HasValue_ForNull_IsFalse()
    {
        object? value = null;

        Assert.False(value.HasValue());
    }

    [Fact]
    public void HasValue_ForOtherTypes_IsTrueWhenNotNull()
    {
        Assert.True(new DateTime(2026, 1, 1).HasValue());
    }

    [Fact]
    public void HasValue_ForAnEmptyList_IsFalse()
    {
        Assert.False(new List<string>().HasValue());
    }

    [Fact]
    public void HasValue_ForANullList_IsFalse()
    {
        List<string> list = null!;

        Assert.False(list.HasValue());
    }

    [Fact]
    public void HasValue_ForASingleItemList_LooksAtThatItem()
    {
        Assert.False(new List<string> { string.Empty }.HasValue());
        Assert.True(new List<string> { "algo" }.HasValue());
    }

    [Fact]
    public void HasValue_ForAListWithSeveralItems_LooksAtThemToo()
    {
        // EN: The rule used to change with the length: with exactly one item the element was
        //     evaluated, with two or more the contents were never looked at, so a list of two empty
        //     strings claimed to have a value while a list of one did not. Same question, opposite
        //     answers, decided by the count. Now it is the same rule at every length: a list has a
        //     value when at least one of its items does.
        // ES: La regla cambiaba con el largo: con exactamente un elemento se evaluaba el elemento,
        //     con dos o más no se miraba el contenido, así que una lista de dos cadenas vacías decía
        //     tener valor y una de una sola decía que no. La misma pregunta, respuestas opuestas,
        //     decididas por la cantidad. Ahora es la misma regla en todos los largos: una lista tiene
        //     valor cuando al menos uno de sus elementos lo tiene.
        Assert.False(new List<string> { string.Empty, string.Empty }.HasValue());
        Assert.True(new List<string> { string.Empty, "algo" }.HasValue());
        Assert.True(new List<string> { "algo", "otro" }.HasValue());
    }

    [Fact]
    public void HasValue_ForAListOfNumbers_UsesTheSameRuleAtEveryLength()
    {
        Assert.False(new List<int> { 0 }.HasValue());
        Assert.False(new List<int> { 0, 0, 0 }.HasValue());
        Assert.True(new List<int> { 0, 7 }.HasValue());
    }

    [Fact]
    public void HasValue_ForAListOfObjects_IsTrueWhenAnyIsNotNull()
    {
        Assert.False(new List<object?> { null, null }.HasValue());
        Assert.True(new List<object?> { null, new() }.HasValue());
    }

    [Fact]
    public async Task ToByteArrayAsync_ReadsTheWholeUploadedFile()
    {
        var content = Encoding.UTF8.GetBytes("contenido del archivo");
        var formFile = new FormFile(new MemoryStream(content), 0, content.Length, "file", "doc.txt");

        var bytes = await formFile.ToByteArrayAsync();

        Assert.Equal(content, bytes);
    }

    // ── ConvertTo ────────────────────────────────────────────────────────────────────────────

    private sealed class Fila
    {
        public string Nombre { get; set; } = string.Empty;

        public int Cantidad { get; set; }
    }

    [Fact]
    public void ListToDataTable_BuildsAColumnPerProperty()
    {
        var table = new List<Fila> { new() { Nombre = "uno", Cantidad = 1 } }.ListToDataTable();

        Assert.Equal(2, table.Columns.Count);
        Assert.Single(table.Rows);
        Assert.Equal("uno", table.Rows[0]["Nombre"]);
    }

    [Fact]
    public void ListToDataTable_WithAnEmptySource_StillBuildsTheColumns()
    {
        var table = new List<Fila>().ListToDataTable();

        Assert.Equal(2, table.Columns.Count);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void DataTableToList_MapsColumnsOntoPropertiesIgnoringCase()
    {
        var table = new DataTable();
        table.Columns.Add("Nombre", typeof(string));
        table.Columns.Add("Cantidad", typeof(int));
        table.Rows.Add("dos", 2);

        var list = ConvertTo.DataTableToList<Fila>(table);

        var fila = Assert.Single(list);
        Assert.Equal("dos", fila.Nombre);
        Assert.Equal(2, fila.Cantidad);
    }

    [Fact]
    public void DataTableToList_IgnoresColumnsWithNoMatchingProperty()
    {
        var table = new DataTable();
        table.Columns.Add("Nombre", typeof(string));
        table.Columns.Add("ColumnaQueNadiePidio", typeof(string));
        table.Rows.Add("tres", "ignorada");

        var list = ConvertTo.DataTableToList<Fila>(table);

        Assert.Equal("tres", Assert.Single(list).Nombre);
    }

    [Fact]
    public void DataTableToList_WithAnIncompatibleColumnType_Throws()
    {
        // EN: Values are assigned by reflection with no conversion, so a type mismatch blows up at
        //     runtime rather than being coerced. Worth knowing before feeding it a query result.
        // ES: Los valores se asignan por reflexión sin conversión, así que un tipo que no coincide
        //     explota en runtime en vez de convertirse. Conviene saberlo antes de darle el
        //     resultado de una query.
        var table = new DataTable();
        table.Columns.Add("Cantidad", typeof(string));
        table.Rows.Add("no-es-un-numero");

        Assert.ThrowsAny<Exception>(() => ConvertTo.DataTableToList<Fila>(table));
    }

    // ── EnumHelper ───────────────────────────────────────────────────────────────────────────

    private enum EstadoFactura : byte
    {
        [Display(Name = "Pendiente de pago")]
        Pendiente = 1,

        Pagada = 2,
    }

    [Fact]
    public void GetDisplayName_ReturnsTheDisplayAttribute()
    {
        Assert.Equal("Pendiente de pago", EstadoFactura.Pendiente.GetDisplayName());
    }

    [Fact]
    public void GetDisplayName_WithoutTheAttribute_ReturnsNull()
    {
        Assert.Null(EstadoFactura.Pagada.GetDisplayName());
    }

    private enum EstadoAncho : int
    {
        Grande = 300,
    }

    [Fact]
    public void EnumResponse_PacksTheEnumForTheApi()
    {
        var response = new EnumResponse(EstadoFactura.Pendiente);

        Assert.Equal("Pendiente", response.Name);
        Assert.Equal("Pendiente de pago", response.DisplayName);
        Assert.Equal((byte)1, response.Value);
    }

    [Fact]
    public void EnumResponse_WithAValueThatDoesNotFitInAByte_SaysWhy()
    {
        // EN: The team's enums are declared ': byte' — one byte in SQL Server (TINYINT) instead of
        //     four, in columns, indexes and JWT payloads. Value was 'uint', four bytes wide,
        //     contradicting the very convention every enum reaching it follows. Narrowing it means
        //     an enum that does not follow the convention no longer fits, and that has to say so:
        //     an OverflowException about a conversion explains nothing to whoever wrote the enum.
        // ES: Los enums del equipo se declaran ': byte' — un byte en SQL Server (TINYINT) en vez de
        //     cuatro, en columnas, índices y payloads JWT. Value era 'uint', de cuatro bytes,
        //     contradiciendo la convención que sigue todo enum que le llega. Angostarlo implica que
        //     un enum que no sigue la convención ya no entra, y eso tiene que decirlo: un
        //     OverflowException sobre una conversión no le explica nada a quien escribió el enum.
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new EnumResponse(EstadoAncho.Grande));

        Assert.Contains("byte", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnumResponse_Parameterless_IsUsableForDeserialization()
    {
        var response = new EnumResponse();

        Assert.Equal(string.Empty, response.Name);
    }

    // ── Respuestas ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GenericResponse_CarriesDataAndTotalCount()
    {
        var response = new GenericResponse<List<string>> { Data = ["a", "b"], Count = 57 };

        Assert.Equal(2, response.Data.Count);
        Assert.Equal(57, response.Count);
    }

    [Fact]
    public void ProblemResponse_StartsWithAnEmptyDetailDictionary()
    {
        // EN: Never null — a controller can add entries without checking first.
        // ES: Nunca null — un controller puede agregar entradas sin chequear antes.
        var problem = new ProblemResponse { Title = "algo salió mal" };

        Assert.NotNull(problem.Details);
        Assert.Empty(problem.Details);
    }

    [Fact]
    public void HealthCheckResponse_StartsEmpty()
    {
        var response = new HealthCheckResponse();

        Assert.Empty(response.HealthChecks);
        Assert.Equal(string.Empty, response.Status);
    }

    [Fact]
    public void CurrentUser_BuildsTheBusinessName()
    {
        var user = new ElRoso.Domains.Session.CurrentUser
        {
            Id = Guid.NewGuid(),
            Surname = "Di Rosolini",
            GivenName = "Ezequiel",
            EmailAddress = "ezequiel@elroso.ar",
        };

        Assert.Equal("Di Rosolini, Ezequiel", user.BusinessName);
    }

    [Fact]
    public void EmailResponse_DefaultsToNotSent()
    {
        var response = new EmailResponse();

        Assert.False(response.SendOK);
    }
}
