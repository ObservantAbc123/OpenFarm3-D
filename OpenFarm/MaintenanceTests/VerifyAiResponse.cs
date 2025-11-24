using System;
using System.Linq;
using System.Threading.Tasks;
using DatabaseAccess;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace MaintenanceTests;

public class VerifyAiResponse
{
    private readonly ITestOutputHelper _output;

    public VerifyAiResponse(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CheckResponse()
    {
        var conn = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(conn))
        {
            // Fallback to a default if not set, assuming localhost
            conn = "Host=localhost;Port=5432;Database=openfarm;Username=postgres;Password=postgres";
            _output.WriteLine("Using default connection string: " + conn);
        }

        var db = new DatabaseAccessHelper(conn);
        
        long threadId = 1; // Alice Johnson's thread
        _output.WriteLine($"Checking AI response for Thread {threadId}...");

        var response = await db.AiResponses.GetPendingResponseForThreadAsync(threadId);

        if (response != null)
        {
            _output.WriteLine($"Found response! ID: {response.Id}, Status: {response.Status}");
            _output.WriteLine($"Content: {response.GeneratedContent.Substring(0, Math.Min(50, response.GeneratedContent.Length))}...");
        }
        else
        {
            _output.WriteLine("No pending response found.");
        }
    }
}
