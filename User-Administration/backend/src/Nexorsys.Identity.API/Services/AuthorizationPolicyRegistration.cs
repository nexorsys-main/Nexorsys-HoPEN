using Microsoft.AspNetCore.Authorization;

namespace Nexorsys.Identity.API.Services;

public static class AuthorizationPolicyRegistration
{
    public static IServiceCollection AddNexorsysAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin", "ADMIN", "ADMIN_DSI", "SUPERADMIN"));
            options.AddPolicy("SystemAdminOnly", policy => policy.RequireRole("SYSTEM_ADMIN", "SUPERADMIN"));
            options.AddPolicy("RhOrAdmin", policy => policy.RequireRole("Admin", "ADMIN", "ADMIN_RH", "ADMIN_DSI", "SUPERADMIN", "DIRECTOR"));

            // New endpoints, including minimal API/SignalR endpoints without explicit metadata,
            // must authenticate by default. [AllowAnonymous] stays opt-in and explicit.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        return services;
    }
}
