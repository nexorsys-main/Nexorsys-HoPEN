using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Infrastructure;

namespace Nexorsys.Identity.API.Filters
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public class ApiKeyAuthAttribute : Attribute, IAsyncActionFilter
	{
		private const string ApiKeyHeaderName = "X-Api-Key";

		public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
		{
			var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
			var endpoint = context.HttpContext.GetEndpoint();
			var hasAnonymousMetadata = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null;
			var hasAuthorizationMetadata = endpoint?.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Count > 0;
			// ASP.NET authorizes the JWT on admin/browser endpoints before action filters run.
			// Keep that path, but never let [AllowAnonymous] itself bypass device authentication.
			if (!hasAnonymousMetadata && hasAuthorizationMetadata && context.HttpContext.User.Identity?.IsAuthenticated == true)
			{
				await next();
				return;
			}

			if (!context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
			{
				var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
				var clientCertificate = await context.HttpContext.Connection.GetClientCertificateAsync();
				if (!context.HttpContext.Request.IsHttps || clientCertificate is null ||
					clientCertificate.NotBefore.ToUniversalTime() > DateTime.UtcNow ||
					clientCertificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow ||
					!IsValidClientCertificate(clientCertificate))
				{
					context.Result = new UnauthorizedObjectResult("A valid enrolled device certificate is required.");
					return;
				}

				var workstationId = Guid.TryParse(context.HttpContext.Request.Headers["X-Workstation-Id"], out var id) ? id : Guid.Empty;
				var thumbprint = clientCertificate.Thumbprint?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
				var matches = await db.Workstations.IgnoreQueryFilters().Where(w => w.IsActive &&
					w.EnrollmentState == WorkstationLifecycle.Active && w.DeviceCertificateThumbprint != null &&
					w.DeviceCertificateThumbprint.ToUpper() == thumbprint).Take(2).ToListAsync();
				var enrolledWorkstation = matches.Count == 1 ? matches[0] : null;
				var organizationClaim = context.HttpContext.User.FindFirst("org_id")?.Value;
				var organizationHeader = context.HttpContext.Request.Headers["X-Organization-Id"].ToString();
				if (!WorkstationLifecycle.CanAuthenticate(enrolledWorkstation!, thumbprint) || workstationId != enrolledWorkstation!.Id ||
					(Guid.TryParse(organizationClaim, out var claimOrg) && claimOrg != enrolledWorkstation.OrganizationId) ||
					(!string.IsNullOrWhiteSpace(organizationHeader) &&
					 (!Guid.TryParse(organizationHeader, out var headerOrg) || headerOrg != enrolledWorkstation.OrganizationId)))
				{
					context.Result = new UnauthorizedObjectResult("The device certificate is not enrolled for this workstation.");
					return;
				}
				context.HttpContext.Items["AuthenticatedOrganizationId"] = enrolledWorkstation.OrganizationId;
				context.HttpContext.Items["AuthenticatedWorkstationId"] = enrolledWorkstation.Id;
				enrolledWorkstation!.LastValidatedAt = DateTime.UtcNow;
				await db.SaveChangesAsync();

				await next();
				return;
			}

#if DEBUG
			// Development-only compatibility for local Kiosk work. Production never accepts shared API keys.
			var apiKey = configuration.GetValue<string>("KioskApiKey");

			if (string.IsNullOrEmpty(apiKey))
			{
				context.Result = new StatusCodeResult(500); // Server misconfigured
				return;
			}

			if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
			{
				// Allow JWT authenticated users from the portal
				if (context.HttpContext.User.Identity?.IsAuthenticated == true)
				{
					await next();
					return;
				}

				context.Result = new UnauthorizedObjectResult("API Key was not provided.");
				return;
			}

			var extractedString = extractedApiKey.ToString().Trim();
			var expectedBytes = System.Text.Encoding.UTF8.GetBytes(apiKey);
			var providedBytes = System.Text.Encoding.UTF8.GetBytes(extractedString);
			if (expectedBytes.Length != providedBytes.Length ||
				!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
			{
				var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ApiKeyAuthAttribute>>();
				logger.LogWarning("Kiosk API key mismatch (ExpectedLen: {ELen}, RecvLen: {RLen})",
					apiKey.Length,
					extractedString.Length);

				context.Result = new UnauthorizedObjectResult("Unauthorized client.");
				return;
			}

			// Strictly validate workstation binding
			if (Guid.TryParse(context.HttpContext.Request.Headers["X-Workstation-Id"], out var developmentWorkstation) &&
				Guid.TryParse(context.HttpContext.Request.Headers["X-Organization-Id"], out var developmentOrganization))
			{
				var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
				// IgnoreQueryFilters is necessary here because the tenant context is not yet established.
				var workstation = await db.Workstations.IgnoreQueryFilters().FirstOrDefaultAsync(w =>
					w.Id == developmentWorkstation &&
					w.IsActive &&
					w.EnrollmentState == WorkstationLifecycle.Active);

				if (workstation is null || workstation.OrganizationId != developmentOrganization)
				{
					context.Result = new UnauthorizedObjectResult("Invalid workstation or cross-tenant spoofing detected.");
					return;
				}
				
				context.HttpContext.Items["AuthenticatedOrganizationId"] = workstation.OrganizationId;
				context.HttpContext.Items["AuthenticatedWorkstationId"] = workstation.Id;
			}
			else
			{
				context.Result = new UnauthorizedObjectResult("Missing required headers.");
				return;
			}

			await next();
#else
			context.Result = new UnauthorizedObjectResult("Production environment strictly requires enrolled device certificates.");
			return;
#endif
		}

		private static bool IsValidClientCertificate(X509Certificate2 certificate)
		{
			try
			{
				using var chain = new X509Chain();
				chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
				chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
				chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
				chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.2"));
				return chain.Build(certificate);
			}
			catch (CryptographicException)
			{
				return false;
			}
		}
	}
}
