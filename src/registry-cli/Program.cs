using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using registry_cli.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace registry_cli
{
    class Program
    {
        protected Program() { }

        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<RegistryCliOptions>(args)
                .WithParsedAsync(async options => await SetupAndRun(options));
        }

        private static async Task SetupAndRun(RegistryCliOptions options)
        {
            Debugger.Launch();

            ServiceProvider container = ContainerBuilder.Create().Setup(options).Build();

            IRegistryService registryService = container.GetRequiredService<IRegistryService>();
            ILogger<Program> programLogger = container.GetRequiredService<ILogger<Program>>();

            try
            {
                await registryService.RunCliAsync(options);
            }
            catch (Exception exception)
            {
                programLogger.LogError(exception, exception.Message);
                throw;
            }
        }
    }
}
