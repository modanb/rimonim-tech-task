using MeterSystem.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<PostgresMeterStore>();
builder.Services.AddHostedService<MeterReadingsWorker>();

var host = builder.Build();
await host.RunAsync();
