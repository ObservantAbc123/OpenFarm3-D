using DatabaseAccess.Models;
using RabbitMQHelper.MessageTypes;
using RabbitMQHelper;

namespace PrintManagement;

public static class RmqUpdateHelper
{
    public static async Task RunRmqUpdatePrintStart(long jobId, ILogger<PMSWorker> _logger, RmqHelper _rmqHelper)
    {
        _logger.LogInformation("Print for PrintJob {jid} started, publishing message to RMQ.", jobId);
        TimeSpan currentTimeOfDay = DateTime.Now.TimeOfDay;

        // similar to below regarding my semantic assumption(s)
        PrintStartedMessage msg = new PrintStartedMessage { JobId = (int)jobId, StartTime = DateTime.Now, PrintTime = currentTimeOfDay }; // TODO: unsure of how to handle this System.TimeSpan
        await _rmqHelper.QueueMessage(ExchangeNames.PrintStarted, msg);
    }

    // a PRINT has been finished (Not cleared)
    public static async Task RunRmqUpdatePrintFinish(long printerId, ILogger<PMSWorker> _logger, RmqHelper _rmqHelper)
    {
        _logger.LogInformation("Print on Printer {pid} finished, publishing message to RMQ.", printerId);
        PrintFinishedMessage msg = new PrintFinishedMessage { JobId = (int)printerId };
        await _rmqHelper.QueueMessage(ExchangeNames.PrintFinished, msg);
    }

    // a PrintJob has been cleared (i.e., final print finished, all Prints for PrintJob completed thus completing the PrintJob)
    public static async Task RunRmqUpdatePrintJobCleared(long jobId, ILogger<PMSWorker> _logger, RmqHelper _rmqHelper)
    {
        _logger.LogInformation("Final print for job {jid} cleared, publishing message to RMQ 'PrintCleared' exchange.", jobId);
        RabbitMQHelper.MessageTypes.Message msg = new PrintClearedMessage { JobId = (int)jobId, FinishTime = DateTime.Now}; // says PrinterId but is/should be JobId, lmk if misinterpreting
        await _rmqHelper.QueueMessage(ExchangeNames.PrintCleared, msg);
    }
}
