using DatabaseAccess.Helpers;
using DatabaseAccess.Models;
using System.Collections.Concurrent;
using System.ComponentModel;
using DatabaseAccess;

namespace PrintManagement;

public static class DbUpdateHelper
{
    /// <summary>
    /// Helper that exists primarily to aggregate
    /// the DB state updates required by the PMS.
    /// </summary>
    /// <param name="jobId"></param>
    /// <param name="available"></param>
    /// <param name="_databaseAccessHelper"></param>
    /// <param name="_logger"></param>
    /// <returns></returns>
    public static async Task RunDbUpdatePrintStart(long jobId, RegisteredInstance available, DatabaseAccessHelper _databaseAccessHelper, ILogger<PMSWorker> _logger, long printId)
    {
        TransactionResult result;
        result = await _databaseAccessHelper.Printers.StartPrinterPrintingAsync(available._pid);
        if (result != TransactionResult.Succeeded)
        {
            if (result == TransactionResult.NotFound)
            {
                _logger.LogWarning("Printer {pid} not found.", available._pid);
            }
            else if (result == TransactionResult.NoAction)
            {
                _logger.LogWarning("Printer {pid} already marked as started printing in DB.", available._pid);
            }
            else if (result == TransactionResult.Failed)
            {
                _logger.LogError("Failed to start printer printing for job {jobId}.", jobId);
            }
        }
        result = await _databaseAccessHelper.Prints.UpdatePrintPrinterAsync(printId, available._pid);
        if (result != TransactionResult.Succeeded)
        {
            if (result == TransactionResult.NotFound)
            {
                _logger.LogWarning("Print {printId} not found.", printId);
            }
            else if (result == TransactionResult.NoAction)
            {
                _logger.LogWarning("Print {printId} already associated with printer {pid}.", printId, available._pid);
            }
            else if (result == TransactionResult.Failed)
            {
                _logger.LogError("Failed to update print printer for job {jobId}.", jobId);
            }
        }
        result = await _databaseAccessHelper.Prints.MarkPrintStartedAsync(printId);
        if (result != TransactionResult.Succeeded)
        {
            if (result == TransactionResult.NotFound)
            {
                _logger.LogWarning("Printer {pid} not found.", available._pid);
            }
            else if (result == TransactionResult.NoAction)
            {
                _logger.LogWarning("Print {printId} already marked started in DB.", printId);
            }
            else if (result == TransactionResult.Failed)
            {
                _logger.LogError("Failed to start printer printing for job {jobId}.", jobId);
            }
        }

        // ensure associated print job gets marked as 'printing' for detection upon finish
        result = await _databaseAccessHelper.PrintJobs.UpdatePrintJobStatusAsync(jobId, "printing");
        if (result != TransactionResult.Succeeded)
        {
            if (result == TransactionResult.NotFound)
            {
                _logger.LogWarning("Job {jid} not found.", jobId);
            }
            else if (result == TransactionResult.NoAction)
            {
                _logger.LogWarning("Job {jid} already has status 'printing'.", jobId);
            }
            else if (result == TransactionResult.Failed)
            {
                _logger.LogError("Failed to update status of job {jobId} as 'printing'.", jobId);
            }
        }
    }

    // i.e., all prints denoted by 'numCopies' finished printing for PrintJob with ID=jobId
    public static async Task RunDBUpdatePrintJobComplete(DatabaseAccessHelper _databaseAccessHelper, ILogger<PMSWorker> _logger, long jobId)
    {
        _logger.LogInformation("All prints for job {jid} complete, marking complete in DB.", jobId);
        TransactionResult result = await _databaseAccessHelper.PrintJobs.MarkPrintJobCompletedAsync(jobId);
        if (result != TransactionResult.Succeeded)
        {
            if (result == TransactionResult.NoAction)
                _logger.LogInformation("Job {jid} already marked completed.", jobId);
            else if (result == TransactionResult.Failed)
                _logger.LogInformation("Failed to mark Job {jid} completed.", jobId);
        }
        else
            _logger.LogInformation("Job {jid} marked completed.", jobId);
    }

    public static async Task RunDBUpdatePrintFinish(long jobId, RegisteredInstance printer, DatabaseAccessHelper _databaseAccessHelper, ILogger<PMSWorker> _logger)
    {
        long printId = printer._activePrintId;
        if (printId == -1)
        {
            _logger.LogCritical("Printer {pid} did not possess active print upon print completion.", printer._pid);
            return;
        }

        _logger.LogInformation("Print {printId} finished on printer {pid} for job {jobId}, updating DB states.", printer._activePrintId, printer._pid, jobId);
        TransactionResult result = await _databaseAccessHelper.Printers.StopPrinterPrintingAsync(printer._pid);

        if (result != TransactionResult.Succeeded)
        {
            if (result == TransactionResult.NotFound)
            {
                _logger.LogWarning("Printer {pid} not found.", printer._pid);
            }
            else if (result == TransactionResult.NoAction)
            {
                _logger.LogWarning("Printer {pid} already marked as finished printing in DB.", printer._pid);
            }
            else if (result == TransactionResult.Failed)
            {
                _logger.LogError("Failed to stop printer printing for job {jobId}.", jobId);
            }
        }
        result = await _databaseAccessHelper.Prints.MarkPrintFinishedAsync(printId!);
        if (result != TransactionResult.Succeeded)
        {
            if (result == TransactionResult.NotFound)
            {
                _logger.LogWarning("Print {printId} not found.", printId);
            }
            else if (result == TransactionResult.NoAction)
            {
                _logger.LogWarning("Print {printId} already marked finished in DB.", printId);
            }
            else if (result == TransactionResult.Failed)
            {
                _logger.LogError("Failed to mark print as finished for job {jobId}.", jobId);
            }
        }
        else
        { // success, update maintenance report accordingly
            await _databaseAccessHelper.Maintenance.UpdatePrintCountsAsync(printer._pid, 1, 0);
        }
    }
}
