using DatabaseAccess;
using EmailService.Services;
using RabbitMQHelper;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<IRmqHelper, RmqHelper>();

builder.Services.AddTransient<DatabaseAccessHelper>(_ =>
{
    var conn = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(conn))
        throw new ArgumentException("DATABASE_CONNECTION_STRING environment variable is not set");
    return new DatabaseAccessHelper(conn);
});

builder.Services.AddHostedService<EmailQueueWorker>();
builder.Services.AddHostedService<EmailReceivingService>();
builder.Services.AddTransient<EmailAutoReplyService>();

var host = builder.Build();
host.Run();