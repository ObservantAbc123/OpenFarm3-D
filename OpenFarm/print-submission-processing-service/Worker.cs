using System.Text.Json;
using RabbitMQHelper;
using RabbitMQHelper.MessageTypes;
using DatabaseAccess;
using DatabaseAccess.Models;
using System.Data.Common;

namespace print_submission_processing_service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private IRmqHelper _rmqHelper;
    private readonly FileServerClient.FileServerClient _fileServerClient;
    private readonly DatabaseAccessHelper _databaseAccessHelper;

    public Worker(ILogger<Worker> logger, IRmqHelper rmqHelper)
    {
        _logger = logger;
        _rmqHelper = rmqHelper;

        rmqHelper.Connect();

        string? fileServerClientBaseUrl = Environment.GetEnvironmentVariable("FILE_SERVER_BASE_URL");
        if (fileServerClientBaseUrl is null)
            throw new NullReferenceException("Environment variable for File server base url was null");
        _fileServerClient = new FileServerClient.FileServerClient(fileServerClientBaseUrl);

        string? dbConnectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        if (dbConnectionString is null)
            throw new NullReferenceException("Environment variables are not set for connection string configuration");
        _databaseAccessHelper = new DatabaseAccessHelper(dbConnectionString);

        _rmqHelper.AddListener(QueueNames.FileReady,
            (RabbitMQHelper.MessageTypes.Message message) => OnFileReady(message).GetAwaiter().GetResult());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                // _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            // TODO: maintenance logic for when something can't be downloaded, crashes, etc.
            await Task.Delay(10_000, stoppingToken);
        }
    }

    private async Task<bool> OnFileReady(RabbitMQHelper.MessageTypes.Message message)
    {
        Stream? gcodeStream = _fileServerClient.GetGcodeStreamAsync(message.JobId).Result;
        if (gcodeStream == null)
        {
            _logger.LogError($"Failed to Download GCode, stream was null.");
            return false;
        }
        _logger.LogInformation($".gcode download successful for jobID {message.JobId}!");

        using StreamReader reader = new StreamReader(gcodeStream);
        string? thumbnail = ThumbnailReader.GetThumbnailAsBase64String(reader, out long bytesRead);
        if (thumbnail is not null)
        {
            TransactionResult result = await _databaseAccessHelper.Thumbnail.CreateThumbnail(message.JobId, thumbnail);
            if (result != TransactionResult.Succeeded)
                _logger.LogError("Attempt to store thumbnail was unsuccessful.");
            else
                _logger.LogInformation("Attempt to store thumbnail successful.");
        }
        GCodeParser parser = new GCodeParser();

        // critical failure condition if the metadata doesn't exist
        if (!parser.ParseGcodeFile(reader, bytesRead))
        {
            _logger.LogError($"GCode metadata was null.");
            await RejectJob(message);
        }
        else
        {
            await UpdateMetaData(parser, message);
            GCodeParser.ValidationResultTypes result = parser.ValidateParameters();
            if (result != GCodeParser.ValidationResultTypes.PASSED)
            {
                _logger.LogInformation($"Job {message.JobId} validation failed. Reason: {result.ToString()}");
                await RejectJob(message);
            }
            else
            {
                _logger.LogInformation("Job successfully validated.");
                await AcceptJob(message);
                // TODO: remove before pushing, is for simulation of payment to actually print job gcode
                // await PayForJob(message.JobId);
            }
        }
        return true;
    }

    private async Task AcceptJob(RabbitMQHelper.MessageTypes.Message message)
    {
        await _rmqHelper.QueueMessage(ExchangeNames.JobValidated, new RabbitMQHelper.MessageTypes.Message()
        {
            JobId = message.JobId
        });
        TransactionResult res = await _databaseAccessHelper.PrintJobs.UpdatePrintJobStatusAsync(message.JobId, "systemApproved");
        if (res != TransactionResult.Succeeded)
        {
            if (res == TransactionResult.NoAction)
            {
                _logger.LogInformation("Job already exists (?).");
            }
            else
            {
                _logger.LogInformation("Job not accepted.");
            }
        }
        else
        {
            _logger.LogInformation("Job accepted.");
        }
    }

    // temp. for debugging, need to "pay" for a
    // job such that it gets communicated to PMS
    // instead of lounging in JobValidated
    // private async Task PayForJob(long jobId)
    // {
    //     // notify PMS via RMQ to start printing
    //     await _rmqHelper.QueueMessage(ExchangeNames.JobPaid, new Message()
    //     {
    //         JobId = jobId
    //     });
    //
    //     // update DB accordingly
    //     TransactionResult res = await _databaseAccessHelper.PrintJobs.SetPrintJobPaymentStatusAsync(jobId, true);
    //     if (res != TransactionResult.Succeeded)
    //     {
    //         if (res == TransactionResult.NoAction)
    //         {
    //             _logger.LogInformation("Job not found.");
    //         }
    //         else
    //         {
    //             _logger.LogInformation("Failed to mark job status as paid.");
    //         }
    //     }
    //     else
    //     {
    //         _logger.LogInformation("Marked job status as paid.");
    //     }
    // }

    private async Task RejectJob(RabbitMQHelper.MessageTypes.Message message)
    {
        await _rmqHelper.QueueMessage(ExchangeNames.JobRejected, new RejectMessage()
        {
            JobId = message.JobId,
            RejectReason = RejectReason.FailedValidation
        });

        // TODO: externally, file-server may need to listen to RMQ to be aware of rejection to delete GCode (related issue \#151)
        await _databaseAccessHelper.PrintJobs.UpdatePrintJobStatusAsync(message.JobId, "rejected");
    }

    /// <summary>
    /// Expecting an input string in the format "1d 20h 19m 38s"
    /// </summary>
    /// <param name="input"></param>
    /// <returns>Double representing the number of seconds represented by the input string</returns>
    private static double ParseDurationToSeconds(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return 0d;
        double totalSeconds = 0d;

        string[] parts = input.Split(' ');
        foreach (string part in parts)
        {
            char duration = part[^1];
            string value = part[..^1];
            switch (char.ToLower(duration))
            {
                case ('s'):
                    totalSeconds += double.Parse(value);
                    break;
                case ('m'):
                    totalSeconds += double.Parse(value) * 60;
                    break;
                case ('h'):
                    totalSeconds += double.Parse(value) * 60 * 60;
                    break;
                case ('d'):
                    totalSeconds += double.Parse(value) * 60 * 60 * 24;
                    break;
            }
        }

        return totalSeconds;
    }

    private async Task UpdateMetaData(GCodeParser parser, RabbitMQHelper.MessageTypes.Message message)
    {
        string metaModel = parser.GcodeMetaData.PrinterModel;
        string metaTime = parser.GcodeMetaData.Time;
        string metaWeight = parser.GcodeMetaData.Weight;
        string metaMaterial = parser.GcodeMetaData.Material;

        PrinterModel? storedModel = _databaseAccessHelper.PrinterModels.GetPrinterModelByNameAsync(metaModel).Result;
        double timeDouble = ParseDurationToSeconds(metaTime);
        double weightDouble = Convert.ToDouble(metaWeight);
        MaterialType? matType = _databaseAccessHelper.MaterialTypes.GetMaterialTypeAsync(metaMaterial).Result;

        // critical failure condition if the metadata is invalid
        if (storedModel == null || timeDouble == 0 || weightDouble == 0 || matType == null)
        {
            _logger.LogError($"Metadata element was null.");
            await RejectJob(message);
        }
        else
        {
            parser.PrinterModel = storedModel;
            parser.MaterialType = matType;

            List<Material> materialResult = _databaseAccessHelper.Materials.GetMaterialsByTypeIdAsync(matType.Id).Result;
            int? materialId;
            if (materialResult.Count == 0)
            {
                materialId = null;
                _logger.LogError($"Requested print-material is nonexistent.");
            }
            else
                materialId = materialResult.First().Id;

            await _databaseAccessHelper.PrintJobs.UpdatePrintJobMetaData(message.JobId, weightDouble, timeDouble,
                storedModel.Id, materialId, parser.GcodeMetaData.FinishedBytePos);
        }
    }
}
