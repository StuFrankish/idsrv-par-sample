using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using IdentityServer.ValidatorExtentions;
using IdentityServerHost;
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
        var migrationsAssembly = typeof(Program).Assembly.GetName().Name;
        const string connectionString = @"Data Source=.;Initial Catalog=Duende.IdentityServer;Integrated Security=True;Connect Timeout=30;Encrypt=True;TrustServerCertificate=True;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";

        builder.Services.AddRazorPages();

        builder.Services.AddIdentityServer(options =>
            {
                options.PushedAuthorization.AllowUnregisteredPushedRedirectUris = true;
            })
            .AddConfigurationStore(options =>
            {
                options.ConfigureDbContext = builder => builder.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
            })
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = builder => builder.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
            })
            .AddServerSideSessions()
            .AddCustomAuthorizeRequestValidator<CustomAuthorizeEndpointValidator>()
            .AddTestUsers(TestUsers.Users);

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

        app.UseStaticFiles();
        app.UseRouting();
            
        app.UseIdentityServer();

        app.UseAuthorization();
        app.MapRazorPages().RequireAuthorization();

        return app;
    }
}