using AtriumGateway.Core.Authentication;
using AtriumGateway.Core.Configuration;
using AtriumGateway.Core.Logging;
using AtriumGateway.Core.Observation;
using AtriumGateway.Core.Upstream;
using AtriumGateway.Hosting.Configuration;
using AtriumGateway.Hosting.Errors;
using AtriumGateway.Hosting.Forwarding;
using AtriumGateway.Hosting.Http;
using AtriumGateway.Hosting.Logging;

namespace AtriumGateway.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAtriumGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpForwarder();

        services.AddSingleton(GatewayOptionsLoader.Load(configuration));

        services.AddSingleton<IAccountAuthenticator, AccountAuthenticator>();
        services.AddSingleton<IUpstreamSelector, UpstreamSelector>();
        services.AddSingleton<ForwardingPipelineFactory>();
        services.AddSingleton(provider => provider.GetRequiredService<ForwardingPipelineFactory>().CreateShared());
        services.AddSingleton(provider => new RequestBodyBufferFactory(provider.GetRequiredService<GatewayOptions>().RequestBodyBufferLimit));
        services.AddSingleton<IResponseObserverFactory, ResponseObserverFactory>();
        services.AddSingleton<GatewayErrorWriter>();
        services.AddSingleton<IRequestLogSink, ConsoleRequestLogSink>();
        services.AddSingleton<GatewayRequestHandler>();

        return services;
    }
}
