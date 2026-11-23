namespace ElRoso.Commons.Tests;

using ElRoso.Services;
using ElRoso.Telegram;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public class TelegramNotifierTests
{
    private static TelegramNotifier Build(ITelegramBot bot, long[] recipients)
        => new(bot, recipients, NullLogger<TelegramNotifier>.Instance);

    // --- ITelegramNotifier contract ---

    [Fact]
    public async Task NotifyAsync_WhenNoRecipients_NeverCallsBot()
    {
        var bot = new Mock<ITelegramBot>(MockBehavior.Strict);
        var sut = Build(bot.Object, []);

        await sut.NotifyAsync("hello");

        bot.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NotifyAsync_BroadcastsToAllRecipients()
    {
        var bot = new Mock<ITelegramBot>();
        long[] recipients = [111L, 222L, 333L];
        var sut = Build(bot.Object, recipients);

        await sut.NotifyAsync("alert");

        foreach (var id in recipients)
        {
            bot.Verify(b => b.SendMessageAsync(id, "alert", default), Times.Once);
        }
    }

    [Fact]
    public async Task NotifyAsync_WhenOneRecipientFails_OthersStillReceive()
    {
        var bot = new Mock<ITelegramBot>();
        bot.Setup(b => b.SendMessageAsync(111L, It.IsAny<string>(), default))
           .ThrowsAsync(new HttpRequestException("timeout"));

        var sut = Build(bot.Object, [111L, 222L]);

        // Must not throw despite the first recipient failing.
        // No debe lanzar excepción aunque el primer destinatario falle.
        await sut.NotifyAsync("alert");

        bot.Verify(b => b.SendMessageAsync(222L, "alert", default), Times.Once);
    }

    // --- NullTelegramNotifier ---

    [Fact]
    public async Task NullTelegramNotifier_CompletesWithoutThrowing()
    {
        ITelegramNotifier sut = new NullTelegramNotifier();
        await sut.NotifyAsync("anything");
    }

    // --- TelegramNotifierOptions.ParsedRecipientIds ---

    [Theory]
    [InlineData("123;456;789", new long[] { 123, 456, 789 })]
    [InlineData("123", new long[] { 123 })]
    [InlineData("", new long[] { })]
    [InlineData("abc;123", new long[] { 123 })]         // invalid entry skipped
    [InlineData(" 123 ; 456 ", new long[] { 123, 456 })] // whitespace trimmed
    public void ParsedRecipientIds_ReturnsExpectedArray(string raw, long[] expected)
    {
        var options = new TelegramNotifierOptions { RecipientIds = raw };
        Assert.Equal(expected, options.ParsedRecipientIds);
    }
}
