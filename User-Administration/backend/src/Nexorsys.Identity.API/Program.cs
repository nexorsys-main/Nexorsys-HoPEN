using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Nexorsys.Identity.Infrastructure;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Nexorsys.Identity.API.BackgroundServices;
using Nexorsys.Identity.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Nexorsys.Identity.API.Middlewares;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.ConfigureHttpsDefaults(https =>
		https.ClientCertificateMode = ClientCertificateMode.AllowCertificate));

// Fix for Npgsql 6.0+ DateTime issue
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
	builder.WebHost.UseUrls("http://127.0.0.1:5000");

// Load custom settings if they exist
builder.Configuration.AddJsonFile("custom_settings.json", optional: true, reloadOnChange: true);
var useForwardedHeaders = Nexorsys.Identity.API.Services.TrustedForwardedHeadersConfiguration.Configure(
	builder.Configuration, builder.Services);

var revocationSnapshotStore = new Nexorsys.Identity.API.Services.SignedLicenseRevocationSnapshotStore(
	builder.Configuration, TimeProvider.System, NullLogger<Nexorsys.Identity.API.Services.SignedLicenseRevocationSnapshotStore>.Instance);

if (builder.Environment.IsProduction())
{
	var revocationSnapshot = SignedLicenseRevocationSnapshotVerifier.Verify(
		revocationSnapshotStore.ReadCurrentSnapshot(), builder.Configuration["Licensing:PublicKeyPem"], DateTimeOffset.UtcNow);
	if (!revocationSnapshot.IsValid)
		throw new InvalidOperationException("Production requires a current vendor-signed license revocation snapshot and verification public key.");
}

// Add services to the container.
builder.Services.AddControllers()
	.AddNewtonsoftJson(options =>
	{
		options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
		options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
	});

builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IOrganizationContext, Nexorsys.Identity.API.Services.OrganizationContext>();
builder.Services.AddScoped<Nexorsys.Identity.API.Filters.AgentCertificateAuthenticationFilter>();
builder.Services.AddSingleton<Nexorsys.Identity.API.Services.IWorkstationClientCertificateValidator,
	Nexorsys.Identity.API.Services.SystemWorkstationClientCertificateValidator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(revocationSnapshotStore);
builder.Services.AddScoped<Nexorsys.Identity.API.Services.LicenseEntitlementGuard>();
builder.Services.AddScoped<Nexorsys.Identity.API.Services.ILicenseEntitlementGuard>(sp => sp.GetRequiredService<Nexorsys.Identity.API.Services.LicenseEntitlementGuard>());
if (!string.IsNullOrWhiteSpace(builder.Configuration["Licensing:RevocationFeed:Url"]))
{
	builder.Services.AddSingleton<Nexorsys.Identity.API.Services.SignedLicenseRevocationFeedClient>();
	builder.Services.AddHostedService<Nexorsys.Identity.API.Services.LicenseRevocationRefreshService>();
}

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
{
	var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

	// Si on a des paramètres personnalisés, on reconstruit la chaîne de connexion
	var dbHost = builder.Configuration["dbHost"];
	var dbPort = builder.Configuration["dbPort"];
	var dbName = builder.Configuration["dbName"];
	var dbUser = builder.Configuration["dbUsername"];
	var dbPass = builder.Configuration["dbPassword"];

	if (!string.IsNullOrEmpty(dbHost) && !string.IsNullOrEmpty(dbName))
	{
		connectionString = $"Host={dbHost};Port={dbPort ?? "5432"};Database={dbName};Username={dbUser};Password={dbPass}";
	}

	options.UseNpgsql(connectionString)
		   .UseSnakeCaseNamingConvention();
});

// Infrastructure Services
builder.Services.AddInfrastructure();
builder.Services.AddScoped<Nexorsys.Identity.API.Services.IGlobalSettingsOperationProcessor, Nexorsys.Identity.API.Services.GlobalSettingsOperationProcessor>();
builder.Services.AddHostedService<Nexorsys.Identity.API.Services.GlobalSettingsOperationRecoveryService>();

// Production Data Protection keys must be durable and encrypted with a
// customer-provisioned certificate. Missing configuration is a startup error.
if (builder.Environment.IsProduction())
{
	var keyDirectory = builder.Configuration["DataProtection:KeysDirectory"];
	var encryptionThumbprint = builder.Configuration["DataProtection:EncryptionCertificateThumbprint"];
	if (string.IsNullOrWhiteSpace(keyDirectory) || !Directory.Exists(keyDirectory))
		throw new InvalidOperationException("Production requires an existing DataProtection:KeysDirectory with service-only ACLs.");
	Nexorsys.Identity.API.Services.DataProtectionKeyDirectoryValidator.ValidateAndProbe(
		keyDirectory, builder.Configuration.GetSection("DataProtection:AllowedAccessSids").Get<string[]>());
	if (string.IsNullOrWhiteSpace(encryptionThumbprint))
		throw new InvalidOperationException("Production requires DataProtection:EncryptionCertificateThumbprint.");

	var storeLocation = OperatingSystem.IsWindows() ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
	var store = new X509Store(StoreName.My, storeLocation);
	store.Open(OpenFlags.ReadOnly);
	var normalizedThumbprint = encryptionThumbprint.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
	var certificate = store.Certificates.Cast<X509Certificate2>().SingleOrDefault(c =>
		c.Thumbprint?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant() == normalizedThumbprint);
	var decryptionThumbprints = builder.Configuration.GetSection("DataProtection:DecryptionCertificateThumbprints")
		.Get<string[]>() ?? [];
	var resolvedDecryptionCertificates = decryptionThumbprints
		.Select(value => value?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant())
		.Where(value => !string.IsNullOrWhiteSpace(value) && value != normalizedThumbprint)
		.Distinct(StringComparer.Ordinal)
		.Select(thumbprint => store.Certificates.Cast<X509Certificate2>().SingleOrDefault(c =>
			c.Thumbprint?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant() == thumbprint))
		.ToArray();
	store.Close();
	if (certificate is null || !certificate.HasPrivateKey || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
		throw new InvalidOperationException("Configured Data Protection encryption certificate is missing, inaccessible, or invalid.");
	if (resolvedDecryptionCertificates.Any(c => c is null || !c.HasPrivateKey))
		throw new InvalidOperationException("A configured Data Protection decryption certificate is missing or its private key is inaccessible.");
	var decryptionCertificates = resolvedDecryptionCertificates.Cast<X509Certificate2>().ToArray();

	var dataProtectionBuilder = builder.Services.AddDataProtection()
		.SetApplicationName("NexorSys.Identity")
		.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
	Nexorsys.Identity.API.Services.DataProtectionCertificateRotation.Configure(
		dataProtectionBuilder, certificate, decryptionCertificates);
}
else
{
	builder.Services.AddDataProtection().SetApplicationName("NexorSys.Identity.Development");
}

// Background Security Policies
builder.Services.AddHostedService<InactiveUserRevocationService>();
builder.Services.AddHostedService<RetentionPolicyService>();


// Custom API Services
builder.Services.AddSingleton<Nexorsys.Identity.API.Services.ActiveKioskService>();
builder.Services.AddScoped<Nexorsys.Identity.API.Services.Authentication.RiskBasedAuthService>();
builder.Services.AddScoped<Nexorsys.Identity.API.Services.Authentication.AuthenticationProviderResolver>();

// Federation Providers
builder.Services.AddScoped<Nexorsys.Identity.Core.Abstractions.IAuthenticationProvider, Nexorsys.Identity.API.Services.Authentication.NexorsysProvider>();
builder.Services.AddScoped<Nexorsys.Identity.Core.Abstractions.IAuthenticationProvider, Nexorsys.Identity.API.Services.Authentication.ActiveDirectoryProvider>();
builder.Services.AddScoped<Nexorsys.Identity.Core.Abstractions.IAuthenticationProvider, Nexorsys.Identity.API.Services.Authentication.WindowsProvider>();
builder.Services.AddScoped<Nexorsys.Identity.Core.Abstractions.IAuthenticationProvider, Nexorsys.Identity.API.Services.Authentication.PsiProvider>();
builder.Services.AddScoped<Nexorsys.Identity.Core.Abstractions.IAuthenticationProvider, Nexorsys.Identity.API.Services.Authentication.ECpsProvider>();
builder.Services.AddScoped<Nexorsys.Identity.Core.Abstractions.IAuthenticationProvider, Nexorsys.Identity.API.Services.Authentication.OAuthProvider>();

// Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32 || jwtKey.Contains("change", StringComparison.OrdinalIgnoreCase))
{
	throw new InvalidOperationException("Jwt:Key must be supplied externally and must be at least 32 characters.");
}
if (builder.Environment.IsProduction())
{
	if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
		throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required in Production.");
	if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]) || string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]))
		throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience are required in Production.");
	var productionOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>();
	if (productionOrigins is null || productionOrigins.Length == 0 || productionOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
		throw new InvalidOperationException("Production requires explicit HTTPS origins in Security:AllowedOrigins.");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.Events = new JwtBearerEvents
		{
			OnMessageReceived = context =>
			{
				if (context.Request.Cookies.TryGetValue("nexorsys_session", out var cookieToken))
					context.Token = cookieToken;
				return Task.CompletedTask;
			},
			OnTokenValidated = async context =>
			{
				if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
					!Guid.TryParse(context.Principal?.FindFirst("org_id")?.Value, out var organizationId) ||
					!Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var sessionId))
				{
					context.Fail("Session claims are incomplete.");
					return;
				}
				var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
				var user = await db.Users.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId && x.OrganizationId == organizationId);
				var session = await db.UserSessions.IgnoreQueryFilters().FirstOrDefaultAsync(x =>
					x.Id == sessionId && x.UserId == userId && x.OrganizationId == organizationId &&
					x.Status == "Active" && x.SessionEndedAt == null && x.LastActivityAt > DateTime.UtcNow.AddHours(-1));
				if (user is null || !user.IsActive || (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow) || session is null)
					context.Fail("Session is revoked or user is inactive.");
				else
				{
					session.LastActivityAt = DateTime.UtcNow;
					await db.SaveChangesAsync();
				}
			}
		};
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
		ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "NexorSys.Identity",
		ValidAudience = builder.Configuration["Jwt:Audience"] ?? "NexorSys.Identity.Clients",
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
		};
	});

// Authorization (including authenticated-by-default protection for newly mapped endpoints).
builder.Services.AddNexorsysAuthorizationPolicies();

var configuredOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var allowedOrigins = builder.Environment.IsDevelopment()
	? configuredOrigins.Concat(new[] { "http://localhost:3000", "http://127.0.0.1:3000", "http://localhost:3005", "http://127.0.0.1:3005", "https://localhost:3000", "https://127.0.0.1:3000", "https://localhost:3005", "https://127.0.0.1:3005" }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
	: configuredOrigins;
builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(policy =>
	{
		policy.WithOrigins(allowedOrigins)
			  .AllowAnyHeader()
			  .AllowAnyMethod()
			  .AllowCredentials();
	});
});

// Conformité PGSSI-S: Protection contre le brute-force et déni de service (DDoS)
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = 429;

	// Limite globale par IP (5000 requêtes / minute) — réseau interne clinique
	options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
		RateLimitPartition.GetFixedWindowLimiter(
			partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
			factory: partition => new FixedWindowRateLimiterOptions
			{
				AutoReplenishment = true,
				PermitLimit = 5000,
				QueueLimit = 100,
				Window = TimeSpan.FromMinutes(1)
			}));

	Nexorsys.Identity.API.Services.ApiRateLimitPolicies.Configure(options);

});

var app = builder.Build();

// Must run before HTTPS redirection, rate limiting, and any middleware consuming client IP.
if (useForwardedHeaders)
	app.UseForwardedHeaders();

app.UseErrorHandling();
if (app.Environment.IsProduction())
	app.UseHttpsRedirection();
app.UseCors();

// Cookie-authenticated browser mutations require an explicit anti-CSRF header.
// Kiosk/API-key traffic has no browser session cookie and is handled separately.
var configuredConnectSources = builder.Configuration.GetSection("Security:AdditionalConnectSources").Get<string[]>() ?? [];
var contentSecurityPolicy = SecurityHeadersPolicy.BuildContentSecurityPolicy(app.Environment, configuredConnectSources);
app.Use(async (context, next) =>
{
	var unsafeMethod = HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) ||
		HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method);
	var isKioskRequest = context.Request.Path.StartsWithSegments("/api/kiosk") || context.Request.Path.StartsWithSegments("/api/fleet");
	var isLogin = context.Request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase);
	if (unsafeMethod && !isKioskRequest && !isLogin && context.Request.Cookies.ContainsKey("nexorsys_session"))
	{
		var csrfCookie = context.Request.Cookies["nexorsys_csrf"];
		var csrfHeader = context.Request.Headers["X-CSRF-TOKEN"].ToString();
		if (string.IsNullOrWhiteSpace(csrfCookie) || string.IsNullOrWhiteSpace(csrfHeader) ||
			!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
				Encoding.UTF8.GetBytes(csrfCookie), Encoding.UTF8.GetBytes(csrfHeader)))
		{
			context.Response.StatusCode = StatusCodes.Status403Forbidden;
			await context.Response.WriteAsync("CSRF validation failed.");
			return;
		}
	}
	await next();
});

// Production schema changes are deployed explicitly with reviewed EF migrations.
// Startup verifies readiness and fails closed; it never mutates schema or seeds operational data.
using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	if (!await db.Database.CanConnectAsync())
		throw new InvalidOperationException("Database is unavailable; API startup aborted.");

	var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
	if (pendingMigrations.Any())
		throw new InvalidOperationException("Database schema is behind the application; apply reviewed EF migrations before starting the API.");
}

Console.WriteLine("--- Database connectivity and migration readiness verified ---");
app.Use(async (context, next) =>
{
	var headers = context.Response.Headers;
	headers.Append("X-Content-Type-Options", "nosniff");
	headers.Append("X-Frame-Options", "DENY"); // Anti Clickjacking
	headers.Append("X-XSS-Protection", "1; mode=block");
	headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload"); // Forcer HTTPS
	headers.Append("Content-Security-Policy", contentSecurityPolicy);
	headers.Append("Referrer-Policy", "no-referrer"); // Confidentialité absolue
	headers.Append("Permissions-Policy", "geolocation=(), camera=(), microphone=()");

	// Supprimer les en-têtes bavards (Fingerprinting)
	headers.Remove("X-Powered-By");
	headers.Remove("Server");

	await next();
});

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Nexorsys.Identity.API.Hubs.IdentityHub>("/hubs/identity");

app.Run();
