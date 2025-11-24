using RabbitMQHelper;

namespace google_form_listener;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddSingleton<IRmqHelper, RmqHelper>();

        var host = builder.Build();
        host.Run();
    }
}