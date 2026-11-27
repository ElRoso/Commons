namespace ElRoso.Commons.Tests;

using ElRoso.DependencyInjection;
using ElRoso.Domains.Requests;
using ElRoso.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class MailServiceCompositionTests
{
    [Fact]
    public void AddGraphMailService_RegistersOnlyGraphAsSingleton()
    {
        var services = CreateServices();
        services.AddGraphMailService();
        services.AddGraphMailService();

        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, item => item.ServiceType == typeof(IGraphMailService)).Lifetime);
        using var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<IGraphMailService>();
        Assert.Same(graph, provider.GetRequiredService<IGraphMailService>());
        Assert.Null(provider.GetService<IMailService>());
        Assert.Null(provider.GetService<ISmtpMailService>());
    }

    [Fact]
    public async Task AddMailService_UsesRegisteredTransportsAndForwardsRequestAndToken()
    {
        var smtp = new Mock<ISmtpMailService>(MockBehavior.Strict);
        var graph = new Mock<IGraphMailService>(MockBehavior.Strict);
        var request = new EmailRequest { Recipients = ["recipient@example.com"] };
        using var cancellation = new CancellationTokenSource();
        smtp.Setup(service => service.SendAsync(It.Is<EmailRequest>(value => ReferenceEquals(value, request)), cancellation.Token))
            .Returns(Task.CompletedTask);
        graph.Setup(service => service.SendAsync(It.Is<EmailRequest>(value => ReferenceEquals(value, request)), cancellation.Token))
            .Returns(Task.CompletedTask);

        var services = CreateServices();
        services.AddSingleton(smtp.Object);
        services.AddSingleton(graph.Object);
        services.AddMailService();

        using var provider = services.BuildServiceProvider();
        var facade = provider.GetRequiredService<IMailService>();
        Assert.IsType<MailService>(facade);
        Assert.Same(facade, provider.GetRequiredService<IMailService>());
        Assert.Same(smtp.Object, provider.GetRequiredService<ISmtpMailService>());
        Assert.Same(graph.Object, provider.GetRequiredService<IGraphMailService>());

        await facade.SendSMTPAsync(request, cancellation.Token);
        await facade.SendGraphAsync(request, cancellation.Token);

        smtp.Verify(service => service.SendAsync(It.Is<EmailRequest>(value => ReferenceEquals(value, request)), cancellation.Token), Times.Once);
        graph.Verify(service => service.SendAsync(It.Is<EmailRequest>(value => ReferenceEquals(value, request)), cancellation.Token), Times.Once);
    }

    [Fact]
    public void AddMailService_RegistersFacadeAndDefaultTransports()
    {
        var services = CreateServices();
        services.AddMailService();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<MailService>(provider.GetRequiredService<IMailService>());
        Assert.IsType<SmtpMailService>(provider.GetRequiredService<ISmtpMailService>());
        Assert.NotNull(provider.GetRequiredService<IGraphMailService>());
    }

    private static IServiceCollection CreateServices()
    {
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddLogging();
    }
}
