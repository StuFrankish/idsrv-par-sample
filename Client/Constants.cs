namespace Client;

public class ConfigurationSections
{
    public const string IdentityProvider = "IdentityProvider";
    public const string Licenses = "Licenses";
}

public static class OpenIdConnectScopes
{
    public const string OpenId = "openid";
    public const string Profile = "profile";
    public const string OfflineAccess = "offline_access";
}