namespace ElRoso.Commons.Tests;

using ElRoso.Domains.Session;
using ElRoso.Exceptions;
using ElRoso.Presentation.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

public class CurrentUserMiddlewareTests
{
    private const string ObjectIdentifierClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private static readonly Guid TestUserId = Guid.NewGuid();

    private static readonly CurrentUserMiddleware Sut = new(NullLogger<CurrentUserMiddleware>.Instance);

    private static DefaultHttpContext CreateAuthenticatedContext(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return ctx;
    }

    private static DefaultHttpContext CreateUnauthenticatedContext()
    {
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        return ctx;
    }

    private static Claim[] AllClaims() =>
    [
        new Claim(ObjectIdentifierClaimType, TestUserId.ToString()),
        new Claim(ClaimTypes.Surname, "Doe"),
        new Claim(ClaimTypes.GivenName, "John"),
        new Claim(ClaimTypes.Upn, "john.doe@elroso.ar"),
    ];

    [Fact]
    public async Task InvokeAsync_UnauthenticatedRequest_PassesThroughWithoutSettingUser()
    {
        var ctx = CreateUnauthenticatedContext();
        var called = false;

        await Sut.InvokeAsync(ctx, _ => { called = true; return Task.CompletedTask; });

        Assert.True(called);
        Assert.False(ctx.Items.ContainsKey("currentUser"));
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUserWithAllClaims_CallsNext()
    {
        var ctx = CreateAuthenticatedContext(AllClaims());
        var called = false;

        await Sut.InvokeAsync(ctx, _ => { called = true; return Task.CompletedTask; });

        Assert.True(called);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUserWithAllClaims_SetsCurrentUserInHttpContext()
    {
        var ctx = CreateAuthenticatedContext(AllClaims());

        await Sut.InvokeAsync(ctx, _ => Task.CompletedTask);

        var currentUser = ctx.Items["currentUser"] as CurrentUser;
        Assert.NotNull(currentUser);
        Assert.Equal(TestUserId, currentUser.Id);
        Assert.Equal("Doe", currentUser.Surname);
        Assert.Equal("John", currentUser.GivenName);
        Assert.Equal("john.doe@elroso.ar", currentUser.EmailAddress);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_MissingObjectId_ThrowsUnauthorizedException()
    {
        var claims = AllClaims().Where(c => c.Type != ObjectIdentifierClaimType).ToArray();
        var ctx = CreateAuthenticatedContext(claims);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Sut.InvokeAsync(ctx, _ => Task.CompletedTask));
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_MissingSurname_ThrowsUnauthorizedException()
    {
        var claims = AllClaims().Where(c => c.Type != ClaimTypes.Surname).ToArray();
        var ctx = CreateAuthenticatedContext(claims);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Sut.InvokeAsync(ctx, _ => Task.CompletedTask));
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_MissingGivenName_ThrowsUnauthorizedException()
    {
        var claims = AllClaims().Where(c => c.Type != ClaimTypes.GivenName).ToArray();
        var ctx = CreateAuthenticatedContext(claims);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Sut.InvokeAsync(ctx, _ => Task.CompletedTask));
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_MissingEmail_ThrowsUnauthorizedException()
    {
        var claims = AllClaims().Where(c => c.Type != ClaimTypes.Upn).ToArray();
        var ctx = CreateAuthenticatedContext(claims);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Sut.InvokeAsync(ctx, _ => Task.CompletedTask));
    }

    [Theory]
    [InlineData("no-es-un-guid")]
    [InlineData("")]
    [InlineData("12345")]
    public async Task InvokeAsync_WhenTheObjectIdClaimIsNotAGuid_ThrowsUnauthorized(string objectId)
    {
        // EN: Guid.Parse on an unvalidated claim threw FormatException, which is not a domain
        //     exception — so a malformed token came back as a 500 with the message masked in
        //     production, instead of the 401 that says what actually happened.
        // ES: Guid.Parse sobre un claim sin validar tiraba FormatException, que no es excepción de
        //     dominio — así que un token mal formado volvía como 500 con el mensaje enmascarado en
        //     producción, en vez del 401 que dice lo que realmente pasó.
        var context = CreateAuthenticatedContext(
            new Claim(ObjectIdentifierClaimType, objectId),
            new Claim(ClaimTypes.Surname, "Doe"),
            new Claim(ClaimTypes.GivenName, "John"),
            new Claim(ClaimTypes.Upn, "john.doe@elroso.ar"));

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Sut.InvokeAsync(context, _ => Task.CompletedTask));
    }
}
