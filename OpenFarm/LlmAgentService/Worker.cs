using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace LlmAgentService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly LlmClient _llmClient;
    private readonly RagService _ragService;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider, LlmClient llmClient, RagService ragService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _llmClient = llmClient;
        _ragService = ragService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for DB and Ollama to be ready
        await Task.Delay(5000, stoppingToken);
        await _llmClient.EnsureModelLoadedAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessThreadsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing threads");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessThreadsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenFarmContext>();

        // Get active threads where the last message is from a user and no pending AI response exists for it
        var threadsToProcess = await context.Threads
            .Include(t => t.Messages)
            .Include(t => t.AiGeneratedResponses)
            .Where(t => t.ThreadStatus == "active")
            .ToListAsync(stoppingToken);

        foreach (var thread in threadsToProcess)
        {
            var lastMessage = thread.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();

            if (lastMessage == null || lastMessage.SenderType != "user")
            {
                continue;
            }

            // Check if we already have a response for this message
            if (thread.AiGeneratedResponses.Any(r => r.MessageId == lastMessage.Id))
            {
                continue;
            }

            _logger.LogInformation("Generating response for Thread {ThreadId}, Message {MessageId}", thread.Id, lastMessage.Id);

            var contextInfo = await _ragService.GetRelevantContextAsync(lastMessage.MessageContent, stoppingToken);
            
            var systemPrompt = @"You are a helpful assistant for OpenFarm, a 3D printing service. 
            Use the provided context to answer the user's question. 
            If the context doesn't contain the answer, be polite and say you will forward the request to a human operator.
            Draft a response that an operator can review and send.";

            if (!string.IsNullOrWhiteSpace(contextInfo))
            {
                systemPrompt += $"\n\nContext:\n{contextInfo}";
            }

            var generatedResponse = await _llmClient.GenerateCompletionAsync(lastMessage.MessageContent, systemPrompt, stoppingToken);

            if (!string.IsNullOrWhiteSpace(generatedResponse))
            {
                var aiResponse = new AiGeneratedResponse
                {
                    ThreadId = thread.Id,
                    MessageId = lastMessage.Id,
                    GeneratedContent = generatedResponse,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                context.AiGeneratedResponses.Add(aiResponse);
                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Saved AI response for Thread {ThreadId}", thread.Id);
            }
        }
    }
}
