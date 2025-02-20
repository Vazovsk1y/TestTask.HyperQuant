using Connector.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Connector;

public static class Registrator
{
    public static void AddConnector(this IServiceCollection services)
    {
        services.AddSingleton<ITestConnector, BitfinexConnector>();
        services.AddHttpClient(BitfinexConnector.HttpClientName, config =>
        {
            config.BaseAddress = new Uri("https://api-pub.bitfinex.com/v2/");
        });
    }
}