using Microsoft.Extensions.Logging;
using SaviaUp.PrintAgent.Core;
using SaviaUp.PrintAgent.Infrastructure;
using SaviaUp.PrintAgent.Worker;
using SaviaUp.PrintAgent.Worker.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "SaviaUp Print Agent");
builder.Logging.AddJsonConsole();
builder.Services
    .AddCore()
    .AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
builder.Services.AddHostedService<PrintAgentWorker>();

await builder.Build().RunAsync();
