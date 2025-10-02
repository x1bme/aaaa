using ApiGateway.Data;
using OpenIddict.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApiGateway
{
    public class FrontendData : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public FrontendData(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<GeminiDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            var existingApp = await manager.FindByClientIdAsync("frontend", cancellationToken);
            if (existingApp != null)
            {
                await manager.DeleteAsync(existingApp, cancellationToken);
            }

            if (await manager.FindByClientIdAsync("frontend", cancellationToken) is null)
            {
                var application = new OpenIddictApplicationDescriptor
                {
                    ClientId = "frontend",
                    ClientSecret = "frontend-secret",
                    DisplayName = "Frontend",
                    RedirectUris = { new Uri("http://localhost:8080/callback"),
                    new Uri("https://gemini.local/callback"),
                    new Uri("https://gemini.local:32471/callback"),
                    },
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Authorization,
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                        OpenIddictConstants.Permissions.ResponseTypes.Code,
                        OpenIddictConstants.Permissions.Prefixes.Scope + "api",
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,
                    },
                    Requirements = 
                    {
                        OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
                    }
                };
                await manager.CreateAsync(application, cancellationToken);
            }
        }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}