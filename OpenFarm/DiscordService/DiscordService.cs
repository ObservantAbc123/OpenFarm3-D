using DatabaseAccess;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQHelper;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IRmqHelper, RmqHelper>();
builder.Services.AddSingleton<DatabaseAccessHelper>(_ =>
{
    var conn = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(conn))
        throw new ArgumentException("DATABASE_CONNECTION_STRING environment variable is not set");
    return new DatabaseAccessHelper(conn);
});

builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton<CommandService>();
builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();
host.Run();