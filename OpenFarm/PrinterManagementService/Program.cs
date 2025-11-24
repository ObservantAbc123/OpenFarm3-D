using DatabaseAccess;
using OctoprintHelper;
using PrintManagement;
using RabbitMQHelper;

#region Setup
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<PMSWorker>();
builder.Services.AddSingleton<IRmqHelper, RmqHelper>();
builder.Services.AddSingleton<IOctoprintHelper, OctoHelper>();
builder.Services.AddSingleton(_ => new DatabaseAccessHelper(FastFailEnv("DATABASE_CONNECTION_STRING")));
builder.Services.AddSingleton(_ => new FileServerClient.FileServerClient(FastFailEnv("FILE_SERVER_BASE_URL")));

IHost host = builder.Build();
await host.RunAsync();
#endregion 

#region Convenience Helper
static string FastFailEnv(string envKey)
{
    var envValue = Environment.GetEnvironmentVariable(envKey);
    if (string.IsNullOrWhiteSpace(envValue))
        throw new InvalidOperationException($"{envKey} missing or empty.");
    return envValue;
}
#endregion