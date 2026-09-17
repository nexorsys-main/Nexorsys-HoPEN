using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Infrastructure;

namespace Nexorsys.Identity.API.Filters;

/// <summary>Enforces the Agent's mTLS and active workstation binding before any Agent action executes.</summary>
public sealed class AgentCertificateAuthenticationFilter : IAsyncActionFilter
{
    public const string AuthenticatedAgentItemKey = "AuthenticatedAgentWorkstation";

    private readonly AppDbContext _db;
    private readonly ILogger<AgentCertificateAuthenticationFilter> _logger;

    public AgentCertificateAuthenticationFilter(AppDbContext db, ILogger<AgentCertificateAuthenticationFilter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cancellationToken = context.HttpContext.RequestAborted;
        if (!context.HttpContext.Request.IsHttps)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var certificate = await context.HttpContext.Connection.GetClientCertificateAsync(cancellationToken);
        if (certificate is null || !IsTrustedAgentCertificate(certificate))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var thumbprint = WorkstationLifecycle.NormalizeThumbprint(certificate.Thumbprint ?? string.Empty);
        var workstation = await _db.Workstations.IgnoreQueryFilters().SingleOrDefaultAsync(
            candidate => candidate.AgentCertificateThumbprint == thumbprint, cancellationToken);
        var now = DateTime.UtcNow;
        if (workstation is null || !WorkstationLifecycle.CanAuthenticateAgent(workstation, thumbprint, now))
        {
            if (workstation is not null)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(), OrganizationId = workstation.OrganizationId,
                    Action = "AgentAuthenticationDenied", ResourceType = "Agent", ResourceId = workstation.Id,
                    CreatedAt = now
                });
                await _db.SaveChangesAsync(cancellationToken);
            }

            _logger.LogWarning("Agent mTLS authentication denied; code={FailureCode}", "AGENT_NOT_ACTIVE_OR_UNPROVED");
            context.Result = new UnauthorizedResult();
            return;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), OrganizationId = workstation.OrganizationId,
            Action = "AgentAuthenticationSucceeded", ResourceType = "Agent", ResourceId = workstation.Id,
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        context.HttpContext.Items[AuthenticatedAgentItemKey] = workstation;
        await next();
    }

    private static bool IsTrustedAgentCertificate(X509Certificate2 certificate)
    {
        if (certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
            return false;

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
