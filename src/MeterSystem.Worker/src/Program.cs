using MeterSystem.Worker.Consumers;
using MeterSystem.Worker.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddHostedService<MeterReadingsConsumer>();

var host = builder.Build();
host.Run();
