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
            new("api1") 
        ];

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

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,

                RequirePushedAuthorization = true,

                BackChannelLogoutSessionRequired = true,
                BackChannelLogoutUri = "https://localhost:44300/bff/backchannel",
                PostLogoutRedirectUris = { "https://localhost:44300/signout-callback-oidc" },

                AllowOfflineAccess = true,

                AllowedScopes = { "openid", "profile" }
            }
        ];
}