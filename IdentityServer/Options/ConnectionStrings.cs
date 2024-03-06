namespace IdentityServer.Options;

public class ConnectionStrings : ICustomOptions
{
    public string SqlServer { get; set; } = String.Empty;
}
