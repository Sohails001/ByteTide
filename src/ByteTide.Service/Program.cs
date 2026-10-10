using ByteTide.Service;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Makes this behave correctly whether launched by the Windows Service
// Control Manager or run directly from a console for testing.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ByteTideService";
});

builder.Services.AddHostedService<MonitorBackgroundService>();

var host = builder.Build();
host.Run();
