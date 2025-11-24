using DatabaseAccess.Models;
using LlmAgentService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration["DATABASE_CONNECTION_STRING"] ?? "Host=localhost;Database=openfarm;Username=postgres;Password=postgres";

builder.Services.AddDbContext<OpenFarmContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector()));

builder.Services.AddHttpClient<LlmClient>(client =>
{
    var ollamaUrl = builder.Configuration["OLLAMA_BASE_URL"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromMinutes(5); // LLMs can be slow
});

builder.Services.AddSingleton<RagService>();
builder.Services.AddSingleton<DynamicKnowledgeGenerator>();
builder.Services.AddSingleton<KnowledgeSeeder>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Run seeder
using (var scope = host.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<KnowledgeSeeder>();
    // We run this in background or wait? For simplicity, let's just fire and forget or wait with timeout.
    // Better to run it.
    try 
    {
        await seeder.SeedAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Seeding failed: {ex.Message}");
    }
}

host.Run();
