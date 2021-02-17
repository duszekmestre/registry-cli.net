using System.Threading.Tasks;

namespace registry_cli.Services
{
    internal interface IRegistryService
    {
        Task RunCliAsync(RegistryCliOptions options);
    }
}