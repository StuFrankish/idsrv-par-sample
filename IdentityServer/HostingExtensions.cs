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
    private static void InitializeDatabase(this IApplicationBuilder app, bool runSeeding = false)
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

        // Exit early if seeding is not required.
        if (!runSeeding) return;

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
        // Create a local instance of the ConnectionStrings & ApplicationKeys options for immediate use.
        var connectionStrings = builder.GetCustomOptionsConfiguration<ConnectionStrings>(ConfigurationSections.ConnectionStrings);
        var applicationKeys = builder.GetCustomOptionsConfiguration<ApplicationKeys>(ConfigurationSections.ApplicationKeys);

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

        void configureDbContext(DbContextOptionsBuilder builder) =>
            builder.UseSqlServer(connectionStrings.SqlServer, sql => sql.MigrationsAssembly(typeof(Program).Assembly.GetName().Name));

        builder.Services.AddIdentityServer(options =>
            {
                options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
                options.ServerSideSessions.RemoveExpiredSessions = true;
                options.ServerSideSessions.RemoveExpiredSessionsFrequency = TimeSpan.FromMinutes(5);
                options.ServerSideSessions.ExpiredSessionsTriggerBackchannelLogout = true;
            })
            .AddConfigurationStore(options =>
            {
                options.ConfigureDbContext = configureDbContext;
            })
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = configureDbContext;
            })
            .AddServerSideSessions()
            .AddCustomAuthorizeRequestValidator<CustomAuthorizeEndpointValidator>()
            .AddTestUsers(TestUsers.Users);

        // Add Healthchecks
        builder.Services.AddHealthChecks()
            .AddUptimeHealthCheck()
            .AddSqlServer(connectionString: connectionStrings.SqlServer)
            .AddHangfire((setup) => {
                setup.MinimumAvailableServers = 1;
                setup.MaximumJobsFailed = 10;
            });

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        bool isDevEnvironment = app.Environment.IsDevelopment();

        app.UseSerilogRequestLogging();

        if (isDevEnvironment)
        {
            app.UseDeveloperExceptionPage();
        }

        app.InitializeDatabase(runSeeding: isDevEnvironment);

        app.UseHangfireDashboard(pathMatch: "/_hangfire");

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

        return app;
    }
}