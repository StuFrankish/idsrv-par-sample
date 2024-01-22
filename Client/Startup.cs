using Client.Options;
using IdentityModel;
using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;

namespace Client;

public class Startup(IConfiguration configuration)
{
    private readonly IConfiguration _configuration = configuration;

    public void ConfigureServices(IServiceCollection services)
    {
        // Configure our options object.
        services.Configure<IdentityProviderOptions>(_configuration.GetSection(key: ConfigurationSections.IdentityProvider));

        // Create a local instance of the options for immediate use.
        var identityProviderOptions = new IdentityProviderOptions();
        _configuration.GetSection(ConfigurationSections.IdentityProvider).Bind(identityProviderOptions);

        // Setup the rest of the client.
        services.AddTransient<ParOidcEvents>();
        services.AddSingleton<IDiscoveryCache>(_ => new DiscoveryCache(identityProviderOptions.Authority));

        // add automatic token management
        services.AddOpenIdConnectAccessTokenManagement();

        // Add PAR interaction httpClient
        services.AddHttpClient<ParOidcEvents>(name: "par_interaction_client", options => {
            options.BaseAddress = new Uri(uriString: identityProviderOptions.Authority);
        });

        // add MVC
        services.AddControllersWithViews();

        // add cookie-based session management with OpenID Connect authentication
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "cookie";
                options.DefaultChallengeScheme = "oidc";
            })
            .AddCookie("cookie", options =>
            {
                options.Cookie.Name = "mvc.par";

                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;

                options.Events.OnSigningOut = async e =>
                {
                    // automatically revoke refresh token at signout time
                    await e.HttpContext.RevokeRefreshTokenAsync();
                };
            })
            .AddOpenIdConnect("oidc", options =>
            {
                // Needed to add PAR support
                options.EventsType = typeof(ParOidcEvents);

                // Setup Client
                options.Authority = identityProviderOptions.Authority;
                options.ClientId = identityProviderOptions.ClientId;
                options.ClientSecret = identityProviderOptions.ClientSecret;

                // code flow + PKCE (PKCE is turned on by default and required by the identity provider in this sample)
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;

                options.Scope.Clear();
                options.Scope.Add(OpenIdConnectScopes.OpenId);
                options.Scope.Add(OpenIdConnectScopes.Profile);
                options.Scope.Add(OpenIdConnectScopes.OfflineAccess);

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
                options.MapInboundClaims = false;
                options.DisableTelemetry = true;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = JwtClaimTypes.Name,
                    RoleClaimType = JwtClaimTypes.Role
                };
            });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseDeveloperExceptionPage();
        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints
                .MapDefaultControllerRoute()
                .RequireAuthorization();
        });
    }
}