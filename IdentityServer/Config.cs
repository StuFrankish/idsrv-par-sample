using Duende.IdentityServer.Models;

namespace IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        [ 
            new IdentityResources.OpenId(),
            new IdentityResources.Profile()
        ];

    public static IEnumerable<ApiScope> ApiScopes =>
        [ 
            new("api1", "MyAPI") 
        ];

    public static IEnumerable<ApiResource> ApiResources => [];

    public static IEnumerable<Client> Clients =>
        [
            new Client
            {
                ClientId = "mvc.par",
                ClientName = "MVC PAR Client",

                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                RequireRequestObject = false,

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,

                RequirePushedAuthorization = true,

                // Note that redirect uris are optional for PAR clients when the
                // AllowUnregisteredPushedRedirectUris flag is enabled
                // RedirectUris = { "https://localhost:44300/signin-oidc" },
                
                FrontChannelLogoutUri = "https://localhost:44300/signout-oidc",
                PostLogoutRedirectUris = { "https://localhost:44300/signout-callback-oidc" },

                AllowOfflineAccess = true,

                AllowedScopes = { "openid", "profile" }
            }
        ];
}