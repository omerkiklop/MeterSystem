using MeterSystem.Worker.Consumers;
using MeterSystem.Worker.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId());

    builder.Services.AddMessaging(builder.Configuration);
    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddCache(builder.Configuration);
    builder.Services.AddHostedService<MeterReadingsConsumer>();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Worker host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
