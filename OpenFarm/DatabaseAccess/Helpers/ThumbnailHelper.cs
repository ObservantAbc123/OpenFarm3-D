using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

public class ThumbnailHelper(OpenFarmContext context) : BaseHelper(context)
{
    public async Task<TransactionResult> CreateThumbnail(long jobId, string thumbString)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        if (string.IsNullOrWhiteSpace(thumbString))
            return TransactionResult.NoAction;
        try
        {
            await _context.Thumbnails.AddAsync(new Thumbnail
            {
                PrintJobId = jobId,
                ThumbString = thumbString
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            await transaction.RollbackAsync();
            return TransactionResult.Failed;
        }
    }

    // for use by desktop application
    public async Task<Thumbnail?> GetThumbText(long jobId)
    {
        return await _context.Thumbnails.FirstOrDefaultAsync(Thumbnail => Thumbnail.PrintJobId == jobId);
    }
}