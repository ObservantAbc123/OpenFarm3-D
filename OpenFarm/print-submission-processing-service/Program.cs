using print_submission_processing_service;
using RabbitMQHelper;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<IRmqHelper, RmqHelper>();

var host = builder.Build();
host.Run();