using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Nexorsys.Identity.API.Controllers;

var checkPath = ParseCheckPath(args);
var output = ParseOutputPath(args);
var controllerAssembly = typeof(AuthController).Assembly;
var endpoints = controllerAssembly.GetTypes()
    .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
    .SelectMany(type =>
    {
        var controllerRoute = type.GetCustomAttributes<RouteAttribute>(inherit: true).FirstOrDefault()?.Template ?? "";
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsDefined(typeof(NonActionAttribute), inherit: true))
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                .Select(attribute => BuildEndpoint(type, method, controllerRoute, attribute)));
    })
    .OrderBy(endpoint => endpoint.Route, StringComparer.OrdinalIgnoreCase)
    .ThenBy(endpoint => endpoint.Methods[0], StringComparer.Ordinal)
    .ThenBy(endpoint => endpoint.Action, StringComparer.Ordinal)
    .ToArray();

var document = new RouteInventoryDocument(
    "NexorSys Identity + Kiosk API route inventory",
    GetGenerationTimestamp(),
    controllerAssembly.GetName().Version?.ToString() ?? "unknown",
    "authenticated-user (explicit AllowAnonymous remains opt-in)",
    endpoints);
var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
if (checkPath is not null)
{
    var fullPath = Path.GetFullPath(checkPath);
    if (!File.Exists(fullPath)) throw new FileNotFoundException("The committed API route inventory is missing.", fullPath);
    var expected = JsonNode.Parse(json)!;
    var actual = JsonNode.Parse(await File.ReadAllTextAsync(fullPath))!;
    expected["GeneratedAtUtc"] = null;
    actual["GeneratedAtUtc"] = null;
    if (!JsonNode.DeepEquals(expected, actual))
        throw new InvalidDataException("The API route inventory is stale. Regenerate docs/api-route-inventory.json from the current API assembly.");
    Console.WriteLine($"API route inventory is current ({endpoints.Length} route/action entries).");
}
else if (output is null) Console.WriteLine(json);
else
{
    var fullPath = Path.GetFullPath(output);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    await File.WriteAllTextAsync(fullPath, json + Environment.NewLine);
    Console.WriteLine($"Generated {endpoints.Length} route/action entries.");
}

static RouteEndpoint BuildEndpoint(Type controller, MethodInfo action, string controllerRoute, HttpMethodAttribute http)
{
    var route = controllerRoute.Replace("[controller]", controller.Name.EndsWith("Controller", StringComparison.Ordinal)
        ? controller.Name[..^"Controller".Length] : controller.Name, StringComparison.OrdinalIgnoreCase);
    var actionRoute = http.Template ?? "";
    var fullRoute = string.Join('/', new[] { route.Trim('/'), actionRoute.Trim('/') }.Where(part => part.Length > 0));
    var allowAnonymous = action.IsDefined(typeof(AllowAnonymousAttribute), true) || controller.IsDefined(typeof(AllowAnonymousAttribute), true);
    var authorization = action.GetCustomAttributes<AuthorizeAttribute>(true)
        .Concat(controller.GetCustomAttributes<AuthorizeAttribute>(true)).ToArray();
    var filters = action.GetCustomAttributes(true).Concat(controller.GetCustomAttributes(true)).ToArray();
    var customBoundaries = filters.Select(attribute => attribute switch
        {
            Microsoft.AspNetCore.Mvc.ServiceFilterAttribute serviceFilter => serviceFilter.ServiceType.Name,
            _ when attribute.GetType().Name.Contains("ApiKeyAuth", StringComparison.Ordinal) => attribute.GetType().Name,
            _ => null
        })
        .OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    var customBoundary = customBoundaries.Length > 0;
    var boundary = customBoundary ? "custom-auth" : allowAnonymous ? "allow-anonymous" : authorization.Length > 0 ? "authorize" : "fallback-authenticated";
    return new RouteEndpoint(controller.Name, action.Name, http.HttpMethods.Order(StringComparer.Ordinal).ToArray(),
        "/" + fullRoute, boundary, authorization.Select(attribute => attribute.Policy).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray(),
        authorization.SelectMany(attribute => (attribute.Roles ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).Distinct().ToArray(),
        customBoundary, allowAnonymous, customBoundaries);
}

static string? ParseOutputPath(string[] arguments)
{
    if (arguments.Length == 0) return null;
    if (arguments.Length == 2 && arguments[0] == "--output") return arguments[1];
    if (arguments.Length == 2 && arguments[0] == "--check") return null;
    throw new ArgumentException("Usage: Nexorsys.RouteInventory [--output <file> | --check <file>]");
}

static string? ParseCheckPath(string[] arguments) => arguments.Length == 2 && arguments[0] == "--check"
    ? arguments[1] : null;

static DateTimeOffset GetGenerationTimestamp()
{
    var sourceDateEpoch = Environment.GetEnvironmentVariable("SOURCE_DATE_EPOCH");
    return long.TryParse(sourceDateEpoch, out var epoch)
        ? DateTimeOffset.FromUnixTimeSeconds(epoch)
        : DateTimeOffset.UtcNow;
}

sealed record RouteInventoryDocument(string Product, DateTimeOffset GeneratedAtUtc, string ApiAssemblyVersion,
    string DefaultAuthorizationBoundary, RouteEndpoint[] Endpoints);
sealed record RouteEndpoint(string Controller, string Action, string[] Methods, string Route, string AuthenticationBoundary,
    string?[] Policies, string[] Roles, bool HasCustomAuthenticationFilter, bool FrameworkAllowsAnonymous,
    string[] AdditionalAuthenticationBoundaries);
