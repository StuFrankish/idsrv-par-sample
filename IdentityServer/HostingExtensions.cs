using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using IdentityServerHost;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IdentityServer;

internal static class HostingExtensions
{
    private static void InitializeDatabase(IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope();

        List<Type> contexts = [
            typeof(PersistedGrantDbContext),
            typeof(ConfigurationDbContext)
        ];

        foreach (var context in contexts)
        {
            var dbContext = (DbContext)serviceScope.ServiceProvider.GetRequiredService(context);
            dbContext.Database.Migrate();
        }

        var persistedGrantContext = serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>();
        persistedGrantContext.Database.Migrate();

        var configurationContext = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        configurationContext.Database.Migrate();


        if (!configurationContext.Clients.Any())
        {
            foreach (var client in Config.Clients)
            {
                configurationContext.Clients.Add(client.ToEntity());
            }
            configurationContext.SaveChanges();
        }

        if (!configurationContext.IdentityResources.Any())
        {
            foreach (var resource in Config.IdentityResources)
            {
                configurationContext.IdentityResources.Add(resource.ToEntity());
            }
            configurationContext.SaveChanges();
        }

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