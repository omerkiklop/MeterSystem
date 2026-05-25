using MeterSystem.Api.Endpoints;
using MeterSystem.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapReadingsEndpoints();

app.Run();
