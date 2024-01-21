using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using IdentityModel;
using Serilog;

namespace IdentityServer.ValidatorExtentions;

public class CustomAuthorizeEndpointValidator(IProfileService profileService) : ICustomAuthorizeRequestValidator
{
    private readonly IProfileService _profileService = profileService;

    public Task ValidateAsync(CustomAuthorizeRequestValidationContext context)
    {
        Log.Information(messageTemplate: "Starting custom Authorize Endpoint validation");

        var validatedRequest = context.Result.ValidatedRequest;
        var claimsPrincipal = validatedRequest.Subject;

        // Only want to trigger this once the user is authenticated.
        if (!claimsPrincipal.IsAuthenticated())
            return Task.CompletedTask;

        // Setup the profile data request.
        var dataRequest = new ProfileDataRequestContext
        {
            ValidatedRequest = validatedRequest,
            Subject = claimsPrincipal,
            Client = validatedRequest.Client,

            // We're only interested in the role claim at this point.
            RequestedClaimTypes = [JwtClaimTypes.Role]
        };

        // Execute the request and determine if the user has the required role for the client.
        _profileService.GetProfileDataAsync(dataRequest);

        bool hasRequiredClaim = dataRequest.IssuedClaims
            .Any(claim =>
                JwtClaimTypes.Role.Equals(claim.Type) &&
                (validatedRequest.ClientId + "_BasicAccess").Equals(claim.Value)
            );

        // If the required role wasn't found, we raise an error.
        if (!hasRequiredClaim)
        {
            context.Result.IsError = true;
            context.Result.Error = "missing_basic_access";
            context.Result.ErrorDescription = "User doesn't have permission to access the specified client.";

            Log.Warning(messageTemplate: "Authorization rejected because of missing application permissions");
        }

        return Task.CompletedTask;
    }
}
