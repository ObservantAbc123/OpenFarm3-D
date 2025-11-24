using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

public class AiResponseHelper(OpenFarmContext context) : BaseHelper(context)
{
    public async Task<AiGeneratedResponse?> GetPendingResponseForThreadAsync(long threadId)
    {
        return await _context.AiGeneratedResponses
            .Where(r => r.ThreadId == threadId && r.Status == "Pending")
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task DeleteResponseAsync(long responseId)
    {
        var response = await _context.AiGeneratedResponses.FindAsync(responseId);
        if (response != null)
        {
            _context.AiGeneratedResponses.Remove(response);
            await _context.SaveChangesAsync();
        }
    }
}
