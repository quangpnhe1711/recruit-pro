using Microsoft.AspNetCore.Server.Kestrel.Core;
using RecruitPro.ScoringService.Services;

// `dotnet run --selftest` runs the scoring self-check and exits (no server).
if (args.Contains("--selftest"))
{
    ScoreCalculator.SelfTest();
    Console.WriteLine("scoring self-test OK");
    return;
}

var builder = WebApplication.CreateBuilder(args);

// gRPC needs HTTP/2. Serve cleartext h2c on every endpoint (URL/port from "Urls" config or
// ASPNETCORE_URLS) so it works the same locally and in Docker without TLS setup.
builder.WebHost.ConfigureKestrel(o => o.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http2));

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<ScorerService>();
app.MapGet("/", () => "RecruitPro.ScoringService (gRPC over HTTP/2). Call the Scorer service with a gRPC client.");

app.Run();
