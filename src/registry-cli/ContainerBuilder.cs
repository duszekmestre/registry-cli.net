using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using registry_cli.Infrastructure;
using registry_cli.Services;

namespace registry_cli
{
    internal class ContainerBuilder
    {
        private readonly IServiceCollection services;

        private ContainerBuilder()
        {
            this.services = new ServiceCollection();
        }

        internal static ContainerBuilder Create()
        {
            ContainerBuilder builder = new ContainerBuilder();

            return builder;
        }

        internal ContainerBuilder Setup(RegistryCliOptions options)
        {
            this.services.AddLogging(cfg => cfg
                .AddSimpleConsole(opt => ConfigureConsoleLogging(opt))
                .AddFilter((category, level) => FilterLoggingMessages(category, level))
            );

            services.AddTransient<IRegistryService, RegistryService>();

            this.services.AddHttpClient<IRegistryApiClient, RegistryApiClient>(client => ConfigureRegistryApiClient(client, options));

            return this;
        }

        private static void ConfigureConsoleLogging(Microsoft.Extensions.Logging.Console.SimpleConsoleFormatterOptions opt)
        {
            opt.IncludeScopes = true;
            opt.SingleLine = false;
            opt.TimestampFormat = "hh:mm:ss ";
        }

        private static bool FilterLoggingMessages(string category, LogLevel level)
        {
            if (category.StartsWith("System.Net.Http.HttpClient"))
            {
                return (int)level >= (int)LogLevel.Warning;
            }

            return true;
        }

        private static void ConfigureRegistryApiClient(System.Net.Http.HttpClient client, RegistryCliOptions options)
        {
            client.BaseAddress = new System.Uri(options.Hostname);
        }

        internal ServiceProvider Build()
        {
            return this.services.BuildServiceProvider();
        }
    }
}