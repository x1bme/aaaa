using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ApiGateway.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using ApiGateway.Models;
using ApiGateway.Repositories;
using OpenIddict.Abstractions;
using ApiGateway.Services;

namespace ApiGateway
{
    public class Startup(IConfiguration configuration)
    {
        public IConfiguration Configuration { get; } = configuration;
        
        public void ConfigureServices(IServiceCollection services)
        {
            var connectionString = Configuration.GetConnectionString("DefaultConnection");
			Console.WriteLine("=== CONNECTION STRING ===");
			Console.WriteLine($"Connection String: {connectionString}");
			Console.WriteLine("=========================");

            services.AddDbContext<GeminiDbContext>(options =>
            {
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                options.UseOpenIddict();
            });

             services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<GeminiDbContext>()
            .AddDefaultTokenProviders();

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.LoginPath = "/account/login";
                    options.Events.OnRedirectToLogin = context => 
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }
                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    };
                });
            

            services.AddOpenIddict()

                // Register the OpenIddict core components.
                .AddCore(options =>
                {
                    // Configure OpenIddict to use the EF Core stores/models.
                    options.UseEntityFrameworkCore()
                        .UseDbContext<GeminiDbContext>();
                })

                // Register the OpenIddict server components.
                .AddServer(options =>
                {
                    options.UseAspNetCore().DisableTransportSecurityRequirement();
                    options
                        .AllowClientCredentialsFlow()
                        .AllowAuthorizationCodeFlow()
                        .RequireProofKeyForCodeExchange()
                        .AllowRefreshTokenFlow();
                    
                    options.RegisterScopes("api", OpenIddictConstants.Scopes.OfflineAccess);

                    options
                        .SetTokenEndpointUris("connect/token")
                        .SetAuthorizationEndpointUris("connect/authorize")
                        .SetEndSessionEndpointUris("connect/logout")
                        .SetUserInfoEndpointUris("connect/userinfo");

                    // options.SetIssuer(new Uri("https://gemini.local:32471"));
                    // Encryption and signing of tokens
                    options
                        .AddEphemeralEncryptionKey()
                        .AddEphemeralSigningKey()
                        .DisableAccessTokenEncryption();

                    // Register scopes (permissions)
                    options.RegisterScopes("api");

                    // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
                    options
                        .UseAspNetCore()
                        .EnableTokenEndpointPassthrough()
                        .EnableAuthorizationEndpointPassthrough()
                        .EnableEndSessionEndpointPassthrough()
                        .EnableUserInfoEndpointPassthrough();            
                })
                .AddValidation(options =>
                {
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });
                
            
            services.AddSwaggerGen(c => 
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Gemini API", Version = "v1"});
                c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme 
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri("http://localhost:5000/connect/authorize"),
                            TokenUrl = new Uri("http://localhost:5000/connect/token"),
                            Scopes = new Dictionary<string, string>
                            {
                                {"api", "Access to the API"}
                            }
                        }
                    }
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
                        },
                        new[] { "api" }
                    }
                });

            });

            services.AddCors(options => 
            {
                options.AddPolicy("AllowFrontendApp", builder => 
                {
                    builder.WithOrigins(["https://gemini.local", "http://localhost:8080"])
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
                });
            });

            services.AddSingleton<IGrpcClientService, GrpcClientService>();
            services.AddScoped<IValveRepository, ValveRepository>();
            services.AddScoped<IDauRepository, DauRepository>();
            services.AddControllersWithViews();
            services.AddHostedService<TestData>();
            services.AddHostedService<FrontendData>();
            services.AddHostedService<SwaggerData>();
            services.AddHostedService<PtpServiceData>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, RoleManager<IdentityRole> roleManager, UserManager<User> userManager)
        {
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => 
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gemini API V1");
                    c.OAuthClientId("swagger");
                    c.OAuthUsePkce();
                });
                app.UseDeveloperExceptionPage();
                // Seeding functions to create admin user, see GeminiSeeder.cs
                GeminiSeeder.SeedRolesAsync(roleManager).Wait();
            }
            GeminiSeeder.SeedAdminAsync(userManager).Wait();

            app.UseCors("AllowFrontendApp");
            
            app.UseStaticFiles();

            app.UseRouting();
            
            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
				endpoints.MapGet("/health", async context =>
                {
                    context.Response.StatusCode = 200;
                    await context.Response.WriteAsync("Healthy");
                });

                endpoints.MapGet("/health/ready", async context =>
                {
                    try
                    {
                        // Check database connection
                        using var scope = app.ApplicationServices.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<GeminiDbContext>();
                        await dbContext.Database.CanConnectAsync();

                        context.Response.StatusCode = 200;
                        await context.Response.WriteAsync("Ready");
                    }
                    catch
                    {
                        context.Response.StatusCode = 503;
                        await context.Response.WriteAsync("Not Ready");
                    }
                });
            });
        }
    }
}
