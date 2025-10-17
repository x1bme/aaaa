using DataAccess;
using OpenIddict.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApiGateway.Data
{
    public class PtpServiceData : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public PtpServiceData(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<GeminiDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            // Remove existing if present (for development)
            var existingApp = await manager.FindByClientIdAsync("ptp-service", cancellationToken);
            if (existingApp != null)
            {
                await manager.DeleteAsync(existingApp, cancellationToken);
            }

            if (await manager.FindByClientIdAsync("ptp-service", cancellationToken) is null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "ptp-service",
                    ClientSecret = "ptp-service-secret-change-in-production",
                    DisplayName = "PTP Configuration Service",
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                        OpenIddictConstants.Permissions.Prefixes.Scope + "api"
                    }
                }, cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
