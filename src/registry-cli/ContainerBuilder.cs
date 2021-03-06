﻿using Microsoft.Extensions.DependencyInjection;
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
            services.AddLogging(cfg => cfg
                .AddConsole(opt =>
                {
                    
                })
                .AddFilter((category, level) =>
                {
                    if (category.StartsWith("System.Net.Http.HttpClient"))
                    {
                        return (int)level >= (int)LogLevel.Warning;
                    }

                    return true;
                })
            );

            services.AddTransient<IRegistryService, RegistryService>();

            services.AddHttpClient<IRegistryApiClient, RegistryApiClient>(client =>
            {
                client.BaseAddress = new System.Uri(options.Hostname);
            });

            return this;
        }

        internal ServiceProvider Build()
        {
            return this.services.BuildServiceProvider();
        }
    }
}