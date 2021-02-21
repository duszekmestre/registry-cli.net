using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using registry_cli.Services;
using System.Diagnostics;
using System.Threading.Tasks;

namespace registry_cli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<RegistryCliOptions>(args)
                .WithParsedAsync(async options =>
                {
                    Debugger.Launch();

                    ServiceProvider container = ContainerBuilder.Create().Setup(options).Build();

                    IRegistryService registryService = container.GetRequiredService<IRegistryService>();

                    await registryService.RunCliAsync(options);
                });
        }
    }
}
