using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Hangfire;
using HealthChecks.UI.Client;
using HealthChecks.Uptime;
using IdentityServer.Options;
using IdentityServer.ValidatorExtentions;
using IdentityServerHost;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IdentityServer;

internal static class HostingExtensions
{
    private static void InitializeDatabase(IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope();

        // Create a list of contexts to loop through and migrate.
        List<Type> contexts = [
            typeof(PersistedGrantDbContext),
            typeof(ConfigurationDbContext)
        ];

        foreach (var context in contexts)
        {
            var dbContext = (DbContext)serviceScope.ServiceProvider.GetRequiredService(context);
            dbContext.Database.Migrate();
        }

        // Create an instance of the ConfigurationDbContext so we can seed data.
        var configurationContext = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        
        // Seed clients
        if (!configurationContext.Clients.Any())
        {
            foreach (var client in Config.Clients)
            {
                configurationContext.Clients.Add(client.ToEntity());
            }
            configurationContext.SaveChanges();
        }

        // Seed resources
        if (!configurationContext.IdentityResources.Any())
        {
            foreach (var resource in Config.IdentityResources)
            {
                configurationContext.IdentityResources.Add(resource.ToEntity());
            }
            configurationContext.SaveChanges();
        }

        // Seed API Scopes
        if (!configurationContext.ApiScopes.Any())
        {
            foreach (var resource in Config.ApiScopes)
            {
                configurationContext.ApiScopes.Add(resource.ToEntity());
            }
            configurationContext.SaveChanges();
        }
    }

    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        // Configure our connections strings object.
        builder.Services.Configure<ConnectionStrings>(builder.Configuration.GetSection(key: ConfigurationSections.ConnectionStrings));

        // Create a local instance of the options for immediate use.
        var connectionStrings = new ConnectionStrings();
        builder.Configuration.GetSection(ConfigurationSections.ConnectionStrings).Bind(connectionStrings);

        builder.Services.AddRazorPages();

        // Add Hangfire services.
        builder.Services.AddHangfire(configuration => configuration
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(() => new SqlConnection(connectionStrings.SqlServer), options: new()
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(value: 5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(value: 5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        // Add the processing server as IHostedService
        builder.Services.AddHangfireServer();

        // Add Identity Server Middleware

        var migrationsAssembly = typeof(Program).Assembly.GetName().Name;
        builder.Services.AddIdentityServer(options =>
            {
                options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
            })
            .AddConfigurationStore(options =>
            {
                options.ConfigureDbContext = builder => builder.UseSqlServer(connectionStrings.SqlServer, sql => sql.MigrationsAssembly(migrationsAssembly));
            })
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = builder => builder.UseSqlServer(connectionStrings.SqlServer, sql => sql.MigrationsAssembly(migrationsAssembly));
            })
            .AddServerSideSessions()
            .AddCustomAuthorizeRequestValidator<CustomAuthorizeEndpointValidator>()
            .AddTestUsers(TestUsers.Users);

        // Add Healthchecks
        builder.Services.AddHealthChecks()
            .AddSqlServer(connectionString: connectionStrings.SqlServer)
            .AddHangfire((setup) => {
                setup.MinimumAvailableServers = 1;
                setup.MaximumJobsFailed = 10;
            });

        // Add custom healthcheck (handles service registration and AddHealthChecks call)
        builder.Services.AddApplicationUptimeHealthCheck();

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    { 
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        InitializeDatabase(app);

        app.UseHealthChecks(path: "/_health", options: new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
            AllowCachingResponses = false
        });

        app.UseStaticFiles();
        app.UseRouting();
            
        app.UseIdentityServer();

        app.UseAuthorization();
        app.MapRazorPages().RequireAuthorization();

        app.UseHangfireDashboard(pathMatch: "/_hangfire");


        return app;
    }
}