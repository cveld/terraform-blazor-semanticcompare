using BlazorSemanticCompare.Components;
using BlazorSemanticCompare.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Grace period after a connection drop before the circuit (and the agent plan) is released.
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2);
    });

// Agent API: a plan pushed with the code of an open page lives and dies with that page's circuit.
builder.Services.AddSingleton<AgentSessionRegistry>();
builder.Services.AddScoped<AgentSession>();
builder.Services.AddScoped<CircuitHandler, AgentCircuitHandler>();

// Behind the Container Apps ingress: use the forwarded client address for rate limiting.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1; // only the rightmost X-Forwarded-For entry (added by the ingress) is trustworthy
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Register the JSON diff service for DI
builder.Services.AddSingleton<JsonProcessingService>();

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

const int MaxPlanBytes = 20_000_000;

app.MapPost("/api/plan", async (HttpContext http, AgentSessionRegistry registry) =>
{
    var client = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    if (registry.IsBlocked(client))
    {
        return Results.Json(new { error = "Too many failed attempts. Try again later." }, statusCode: StatusCodes.Status429TooManyRequests);
    }

    var header = http.Request.Headers.Authorization.ToString();
    const string scheme = "Bearer ";
    var code = header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) ? header[scheme.Length..].Trim() : "";
    var session = code.Length == 0 ? null : registry.Find(code);
    if (session?.PlanHandler is not { } handler)
    {
        registry.RecordFailure(client);
        return Results.Json(new { error = "Unknown or expired code. Read the current code from the open page." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    http.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize = MaxPlanBytes;
    string body;
    try
    {
        using var reader = new StreamReader(http.Request.Body);
        body = await reader.ReadToEndAsync(http.RequestAborted);
    }
    catch (BadHttpRequestException)
    {
        return Results.Json(new { error = $"Plan exceeds {MaxPlanBytes / 1_000_000} MB." }, statusCode: StatusCodes.Status413PayloadTooLarge);
    }

    var result = await handler(body);
    return result.Success
        ? Results.Ok(new { resourceChanges = result.ResourceChanges })
        : Results.BadRequest(new { error = result.Error });
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
