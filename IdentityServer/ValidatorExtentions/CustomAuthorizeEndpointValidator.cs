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
        var validatedRequest = context.Result.ValidatedRequest;
        var subject = validatedRequest.Subject;
        
        if (subject is not null && subject.IsAuthenticated())
        {
            var requiredClaim = validatedRequest.ClientId + "_BasicAccess";

            var dataRequest = new ProfileDataRequestContext
            {
                ValidatedRequest = validatedRequest,
                Subject = subject,
                Client = validatedRequest.Client,
                RequestedClaimTypes = [JwtClaimTypes.Role]
            };

            _profileService.GetProfileDataAsync(dataRequest);

            bool hasRequiredClaim = dataRequest.IssuedClaims.Any(claim => claim.Type == JwtClaimTypes.Role && claim.Value == requiredClaim);

            if (!hasRequiredClaim)
            {
                // Always return an error when the user doesn't have a permission required for the app.
                context.Result.IsError = true;
                context.Result.Error = "missing_basic_access";
                context.Result.ErrorDescription = "User doesn't have permission to access the specified client.";

                Log.Warning(messageTemplate: "Authorization rejected because of missing application permissions");
            }
        }

        return Task.CompletedTask;
    }
}
