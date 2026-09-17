using Nexorsys.WindowsAgent;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The NexorSys Agent is Windows-only.");

var builder = Host.CreateApplicationBuilder(args);
var (agentConfiguration, agentCertificate) = AgentCertificateConfiguration.Load();
builder.Services.AddSingleton(agentConfiguration);
builder.Services.AddSingleton(agentCertificate);
builder.Services.AddSingleton<AgentIdentityClient>();
builder.Services.AddWindowsService(options => options.ServiceName = "NexorSysIdentityAgent");
builder.Services.AddHostedService<AgentPipeService>();
using var host = builder.Build();
try { await host.RunAsync(); }
finally { agentCertificate.Dispose(); }
