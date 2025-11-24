using System.Collections.Concurrent;
using DatabaseAccess;
using DatabaseAccess.Models;
using OctoprintHelper;
using RabbitMQHelper;
using RabbitMQHelper.MessageTypes;

namespace PrintManagement;

/// <summary>
///     Worker service that ties together functionality
///     from the RabbitMQ Helper, DatabaseAccess Helper,
///     and Octoprint Helper in order to issue prints,
///     manage hardware connections, and update external
///     system state.
/// </summary>
public class PMSWorker : BackgroundService
{
    #region Service Construction

    /// <summary>
    /// Initializes a new instance of the PMSWorker class with the required dependencies.
    /// </summary>
    /// <param name="logger">Logger instance for logging operations and errors.</param>
    /// <param name="rmqHelper">RabbitMQ helper for message channel operations.</param>
    /// <param name="octohelper">Octoprint helper for 3d printer API operations.</param>
    /// <param name="databaseAccessHelper">Database access helper for data operations.</param>
    /// <param name="fileClient">File server client for retrieving GCode files.</param>
    public PMSWorker(ILogger<PMSWorker> logger, IRmqHelper rmqHelper, IOctoprintHelper octohelper,
        DatabaseAccessHelper databaseAccessHelper, FileServerClient.FileServerClient fileClient)
    {
        // initialize all PMS helper services
        _logger = logger;
        _rmqHelper = rmqHelper;
        _octoHelper = octohelper;
        _databaseAccessHelper = databaseAccessHelper;
        _fileClient = fileClient;
    }

    #endregion

    #region Primary Daemon

    /// <summary>
    ///     Worker loop that runs once every ten
    ///     seconds, asynchronously checking that
    ///     all registered printers are connected,
    ///     printing previously enqueued jobs, and
    ///     maintaining the physical state of each
    ///     connected printer.
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                // basic health indicator
                _logger.LogInformation("PMSWorker running at: {time}", DateTimeOffset.Now);

                // check if any client-db discrepancies necessitating reconnection exist
                await RunConnectionCheckAsync();

                // check whether any printers have been added at runtime
                await RunNewlyAddedPrintersCheck();

                // poll the DB for an enqueued job to pull and print all copies for
                await AttemptProcessEnqueuedJobs();
                await RunFinishedJobCheckAsync();

                // poll printers, update state of printers registered w/ PMS
                await UpdatePrinterStatesAsync(stoppingToken);

                // repeat with dealy
                await Task.Delay(10_000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in printer polling loop.");
                await Task.Delay(10_000, stoppingToken);
            }
    }

    #endregion

    // helper services

    #region Service Globals

    /// <summary>
    /// Logger instance for recording service operations, errors, and diagnostic information.
    /// </summary>
    private readonly ILogger<PMSWorker> _logger;

    /// <summary>
    /// RabbitMQ helper service for handling message queue operations and job notifications.
    /// </summary>
    private readonly IRmqHelper _rmqHelper;

    /// <summary>
    /// Octoprint helper service for communicating with 3D printer APIs and managing print operations.
    /// </summary>
    private readonly IOctoprintHelper _octoHelper;

    /// <summary>
    /// Database access helper for performing CRUD operations on printers, jobs, and prints.
    /// </summary>
    private readonly DatabaseAccessHelper _databaseAccessHelper;

    /// <summary>
    /// File server client for downloading GCode files associated with print jobs.
    /// </summary>
    private readonly FileServerClient.FileServerClient _fileClient;

    /// <summary>
    /// Thread-safe dictionary storing registered printer instances, keyed by printer ID from database.
    /// Maintains active connections and state information for all configured printers.
    /// </summary>
    private ConcurrentDictionary<int, RegisteredInstance> _registeredInstances = new(); // p(rinter)id from db is key

    #endregion

    #region Service Setup

    /// <summary>
    ///     Helps set up RMQ, register printer instances,
    ///     and start the service.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PMSWorker starting async initialization...");

        // setup RabbitMQ connection and listeners
        await RabbitSetupHelperAsync();
        _logger.LogInformation("RMQHelper successfully instantiated");

        // initialize connections to all printers configured in the DB
        _logger.LogInformation("Registering printer instances...");
        _registeredInstances = await RegisterInstancesAsync();
        _logger.LogInformation("Registered {count} printer instance(s).", _registeredInstances.Count);
        _logger.LogInformation("PMSWorker async initialization completed successfully.");
        await base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Establishes connection to RabbitMQ service and configures message listeners for incoming print jobs.
    /// This method sets up the messaging infrastructure required for receiving job payment notifications.
    /// </summary>
    /// <returns>A task representing the asynchronous RabbitMQ setup operation.</returns>
    /// <exception cref="ApplicationException">Thrown when unable to connect to RabbitMQ service.</exception>
    private async Task RabbitSetupHelperAsync()
    {
        try
        {
            _logger.LogInformation("Connecting to RMQ...");
            var connectResult = await _rmqHelper.Connect();
            if (!connectResult)
                // i figure fail fast as RMQ is critical to service functionality
                throw new ApplicationException("Could not connect to RMQ service.");
            _logger.LogInformation("Connected to RMQ.");

            _logger.LogInformation("Attaching listener to queue: {queue}", QueueNames.JobPaid);
            _rmqHelper.AddListener(QueueNames.JobPaid,
                (RabbitMQHelper.MessageTypes.Message message) => JobPaidCallback(message).GetAwaiter().GetResult());
            _logger.LogInformation("Listener attached to queue: {queue}", QueueNames.JobPaid);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex.Message);
            throw; // fail initialization
        }
    }

    /// <summary>
    /// Performs batch registration of all printers configured in the database.
    /// Creates RegisteredInstance objects for each printer and establishes initial connections.
    /// </summary>
    /// <returns>A concurrent dictionary containing registered printer instances, keyed by printer ID.</returns>
    private async Task<ConcurrentDictionary<int, RegisteredInstance>> RegisterInstancesAsync()
    {
        var instances = new ConcurrentDictionary<int, RegisteredInstance>();
        var printers = await _databaseAccessHelper.Printers.GetPrintersAsync();

        foreach (var printer in printers)
            try
            {
                var instance = await RegisterClientInstanceAsync(printer);
                _logger.LogInformation($"Registered printer with ID: {printer.Id}.");
                instances.TryAdd(printer.Id, instance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

        return instances;
    }

    /// <summary>
    /// Asynchronously registers a single printer instance and performs initial connection verification.
    /// Creates HTTP client with proper API key configuration and tests connectivity.
    /// </summary>
    /// <param name="printer">The printer entity from database containing connection details.</param>
    /// <returns>A RegisteredInstance object representing the configured printer connection.</returns>
    private async Task<RegisteredInstance> RegisterClientInstanceAsync(Printer printer)
    {
        // set up params.
        var identifier = printer.Id;
        var handle = printer.Name;
        var clientConnection = new HttpClient();
        clientConnection.BaseAddress = new Uri(printer.IpAddress!);
        clientConnection.DefaultRequestHeaders.Add("X-Api-Key", printer.ApiKey);

        // verify connectivity
        var isConnected = await QueryPrinterConnectionStatusAsync(clientConnection);

        // register instance in DB, return object
        MinimalStateReport? initialState;
        if (isConnected)
            initialState = await _octoHelper.GetPrinterStateMinimal(clientConnection);
        else
            initialState = null;

        var newInstance = new RegisteredInstance(identifier, handle, clientConnection, isConnected, initialState);
        return newInstance;
    }

    /// <summary>
    /// Callback method invoked when a job payment notification is received via RabbitMQ.
    /// Initiates the print job enqueueing process for paid jobs.
    /// </summary>
    /// <param name="message">The RabbitMQ message containing job payment information.</param>
    /// <returns>True if the job was successfully enqueued for printing, false otherwise.</returns>
    private async Task<bool> JobPaidCallback(RabbitMQHelper.MessageTypes.Message message)
    {
        var jobSuccessfullyEnqueued = await EnqueuePrintJob(message.JobId);
        if (!jobSuccessfullyEnqueued)
            return false;
        return true;
    }

    #endregion

    #region Service State Management

    #region Job Processing

    /// <summary>
    /// Attempts to process all enqueued print jobs by finding available printers and issuing print requests.
    /// Iterates through printable jobs and assigns them to available printers for execution.
    /// </summary>
    /// <returns>A task representing the asynchronous job processing operation.</returns>
    private async Task AttemptProcessEnqueuedJobs()
    {
        var jobs = await GetPrintablePrintJobs();

        foreach (var job in jobs)
        {
            if (await PrintJobIsComplete(job))
            {
                _logger.LogInformation("Job {jid} already completed, skipped.", job.Id);
                continue;
            }

            var printer = await GetFirstAvailablePrinterAsync();
            if (printer == null)
            {
                // _logger.LogWarning("No printers are available for printing.");
                break;
            }

            _logger.LogInformation(
                "Found available printer with ID: {pid}, attempting to issue print-copy request...",
                printer._pid);
            var prints = await _databaseAccessHelper.Prints.GetPrintsByPrintJobIdAsync(job.Id);
            if (prints.Count == 0)
            {
                _logger.LogWarning("No prints retrieved for job {jid}, skipped.", job.Id);
                continue;
            }

            foreach (var print in prints)
            {
                var inProgressOrFinished = print.PrintStatus == "printing" || print.StartedAt != null;
                if (inProgressOrFinished)
                {
                    _logger.LogInformation(
                        "Print {printId} for job {jid} is in-progress or finished, skipped.",
                        print.Id, job.Id);
                    continue;
                }

                _logger.LogInformation(
                    "Attempting to issue print {printId} for job {jid} to printer {pid}...",
                    print.Id, job.Id, printer._pid);
                var result = await IssuePrintCopyRequestForJob(job.Id, printer, print.Id);
                if (!result) // return false means couldn't print for some nebulous reason
                    _logger.LogWarning("Print {printId} could not be issued, skipped.");
            }
        }
    }

    /// <summary>
    ///     Create all prints for the PrintJob,
    ///     and enqueue them via the Prints table
    ///     in the DB.
    /// </summary>
    /// <param name="jobId">The ID of the associated PrintJob</param>
    /// <returns></returns>
    private async Task<bool> EnqueuePrintJob(long jobId)
    {
        try
        {
            // PMS creates Prints in DB when processing job on RMQ notification, then pulls from there on update loop
            var associatedJob = await _databaseAccessHelper.PrintJobs.GetPrintJobAsync(jobId);
            if (associatedJob != null)
            {
                var printJobCopies = associatedJob.NumCopies;
                var jobSuccessfullyEnqueued = await EnqueuePrintJobCopiesAsync(jobId, printJobCopies);
                if (!jobSuccessfullyEnqueued)
                    return false;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Creates the specified number of print copies in the database for a given print job.
    /// Each copy represents an individual print instance that will be processed by the system.
    /// </summary>
    /// <param name="jobId">The ID of the print job for which to create copies.</param>
    /// <param name="copiesToPrint">The number of print copies to create.</param>
    /// <returns>True if all copies were successfully created, false if any creation failed.</returns>
    private async Task<bool> EnqueuePrintJobCopiesAsync(long jobId, int copiesToPrint)
    {
        for (var i = 0; i < copiesToPrint; i++)
        {
            var result = _databaseAccessHelper.Prints.CreatePrintAsync(jobId).Result;
            if (result != null)
            {
                var printId = result.Id;
                _logger.LogInformation("Print {printId} created for PrintJob {jobId}", printId, jobId);
            }
            else
            {
                _logger.LogInformation("Could not create/enqueue Print for job {jid} in DB.", jobId);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether a print job has been completed by checking if all required copies have finished printing.
    /// Compares the number of completed prints against the target copy count.
    /// </summary>
    /// <param name="job">The print job to evaluate for completion status.</param>
    /// <returns>True if all copies have been completed, false otherwise.</returns>
    private async Task<bool> PrintJobIsComplete(PrintJob job)
    {
        var targetCount = job.NumCopies;
        var completeCounter = 0;
        var prints = await _databaseAccessHelper.Prints.GetPrintsByPrintJobIdAsync(job.Id);
        foreach (var jobPrint in prints)
            // has been completed
            if (jobPrint.FinishedAt != null)
            {
                // _logger.LogInformation("Print {printId} completed for job {jid}", jobPrint.Id, job.Id);
                completeCounter++;
            }

        _logger.LogInformation("{current} Prints completed for job {jid} with {remain} remaining.", completeCounter,
            job.Id, targetCount - completeCounter);
        return targetCount - completeCounter == 0 && job.CompletedAt == null;
    }

    /// <summary>
    ///     Issue a request to a printer to print
    ///     a copy of the GCode associated with the
    ///     relevant PrintJob.
    /// </summary>
    /// <param name="jid">The associated JobId of the PrintJob</param>
    /// <returns></returns>
    private async Task<bool> IssuePrintCopyRequestForJob(long jid, RegisteredInstance available, long printId)
    {
        try
        {
            // attempt to download GCode to print, associated with job
            var gcode = await _fileClient.GetGcodeBytesAsync(jid);
            if (gcode == null)
            {
                _logger.LogWarning("GCode was null for job {jobId}.", jid);
                return false;
            }

            _logger.LogInformation("GCode successfully retrieved for Job {jid}", jid);
            var fileHandle = $"job_{jid}.gcode";
            _logger.LogInformation("Submitting print {printId} to printer {pid} with job {jobId}.", printId,
                available._pid, jid);
            var printRequestResponse =
                await _octoHelper.UploadAndPrintFile(available._clientConnection, gcode, fileHandle);
            if (printRequestResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Printer {pid} accepted print for job {jobId}.", available._pid, jid);
                await UpdatePrinterStateAsync(available);
                _registeredInstances[available._pid]._activePrintId = printId;
                await DbUpdateHelper.RunDbUpdatePrintStart(jid, available, _databaseAccessHelper, _logger, printId);
                await RmqUpdateHelper.RunRmqUpdatePrintStart(jid, _logger, (RmqHelper)_rmqHelper);
                return true;
            }

            _logger.LogWarning("Printer {pid} failed to accept print for job {jobId}. Status: {statusCode}",
                available._pid, jid, printRequestResponse.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Retrieves all print jobs that are approved by operators and ready for printing.
    /// Orders results by creation time to ensure first-in-first-out processing.
    /// </summary>
    /// <returns>A list of print jobs with "operatorApproved" status, ordered by creation time.</returns>
    private async Task<List<PrintJob>> GetPrintablePrintJobs()
    {
        // var printJobs = _databaseAccessHelper.PrintJobs.GetPrintJobsByStatusAsync("operatorApproved").Result
        //     .OrderBy(pj => pj.CreatedAt).ToList();
        var printJobs = _databaseAccessHelper.PrintJobs.GetPrintJobsAsync().Result
            .Where(pj => pj.Paid)
            .OrderBy(pj => pj.CreatedAt).ToList();
        return printJobs;
    }

    #endregion

    #region Ancillary Query Daemons

    /// <summary>
    /// Checks for newly added printers in the database that aren't registered in the PMS yet.
    /// Automatically registers any new printers discovered during runtime.
    /// </summary>
    /// <returns>A task representing the asynchronous printer discovery and registration operation.</returns>
    private async Task RunNewlyAddedPrintersCheck()
    {
        var printers = await _databaseAccessHelper.Printers.GetPrintersAsync();
        if (printers.Count > _registeredInstances.Count)
        {
            // find printer ids added during runtime
            var updatedPrinterIds = printers.Select(p => p.Id).ToHashSet();
            var oldPrinterIds = _registeredInstances.Keys.ToHashSet();
            var printerIdsToRegister = updatedPrinterIds.Where(id => !oldPrinterIds.Contains(id)).ToHashSet();

            // then register each in the PMS
            foreach (var printerId in printerIdsToRegister)
            {
                var printerToRegister = await _databaseAccessHelper.Printers.GetPrinterAsync(printerId);
                var instance = await RegisterClientInstanceAsync(printerToRegister);
                _registeredInstances.TryAdd(printerId, instance);
            }
        }
    }

    /// <summary>
    ///     Check whether any prints have finished so
    ///     that their associated PrintJobs can continue
    ///     to be processed.
    /// </summary>
    /// <param name="instance">The associated printer</param>
    /// <returns></returns>
    private async Task RunFinishedPrintCheckAsync(RegisteredInstance instance)
    {
        // check for finished prints, update DB accordingly
        try
        {
            // context: Printer finished printing Print, Printer in 'paused' state waiting for finished Print to be cleared
            var printProgress = await _octoHelper.GetPrintProgress(instance._clientConnection);
            var associatedJobId =
                (await _databaseAccessHelper.Prints.GetPrintAsync(instance._activePrintId))!.PrintJobId;
            var targetJob = (await _databaseAccessHelper.PrintJobs.GetPrintJobAsync(associatedJobId.Value))!;
            var targetFilePos = targetJob.FinishedBytePos;
            if (targetFilePos == null)
            {
                _logger.LogError("No file pos. detected in PrintJob GCode.");
                return;
            }

            // if hit marker denoting print-finish
            if (printProgress.FilePos >= targetFilePos)
            {
                // update volume, travel for printer in DB each time a Print is completed
                Task.Run(() =>
                {
                    decimal extrusionCubicM = MaintenanceHelpers.CalculateVolume(_fileClient, associatedJobId.Value).Result;
                    _databaseAccessHelper.Maintenance.UpdateExtrusionVolume(instance._pid, extrusionCubicM);
                });

                Task.Run(() =>
                {
                    decimal linearTravelM = MaintenanceHelpers.CalculateLinearTravel(_fileClient, associatedJobId.Value).Result;
                    _databaseAccessHelper.Maintenance.UpdateExtruderTravel(instance._pid, linearTravelM);
                });

                await _octoHelper.ResumePrintingJob(instance._clientConnection);
                _logger.LogInformation("Print {printId} flagged as having finished, updating DB, RMQ.",
                    instance._activePrintId);
                await DbUpdateHelper.RunDBUpdatePrintFinish(instance._activePrintId, instance, _databaseAccessHelper,
                    _logger);
                await RmqUpdateHelper.RunRmqUpdatePrintFinish(instance._pid, _logger, (RmqHelper)_rmqHelper);
                _registeredInstances[instance._pid]._activePrintId = -1; // show that printer has no active print
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex.Message);
        }
    }

    /// <summary>
    /// Checks for completed print jobs and updates their status in the database.
    /// Examines jobs in "printing" status to determine if all copies have finished.
    /// </summary>
    /// <returns>A task representing the asynchronous job completion check operation.</returns>
    private async Task RunFinishedJobCheckAsync()
    {
        // check whether DB storing any completed jobs and mark them as such
        var inProgressJobs = await _databaseAccessHelper.PrintJobs.GetPrintJobsByStatusAsync("printing");
        foreach (var job in inProgressJobs)
        {
            var targetCount = job.NumCopies;
            var completeCounter = 0;
            var prints = await _databaseAccessHelper.Prints.GetPrintsByPrintJobIdAsync(job.Id);
            foreach (var jobPrint in prints)
                // has been completed
                if (jobPrint.FinishedAt != null)
                    completeCounter++;

            // mark complete in db and publish print-cleared message to rmq exchange, otherwise log info
            if (targetCount - completeCounter == 0 && job.CompletedAt == null)
            {
                await DbUpdateHelper.RunDBUpdatePrintJobComplete(_databaseAccessHelper, _logger, job.Id);

                // despite naming convention, this semantically indicates PrintJob completion
                await RmqUpdateHelper.RunRmqUpdatePrintJobCleared(job.Id, _logger, (RmqHelper)_rmqHelper);
            }
        }
    }

    #endregion


    #region Ancillary Request Daemons

    /// <summary>
    ///     Runs in recurring work-loop,
    ///     updates PMS printer registry,
    ///     runs a check for finished prints,
    ///     and updates DB state.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task UpdatePrinterStatesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var registered in _registeredInstances)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            var instance = registered.Value;
            try
            {
                await UpdatePrinterStateAsync(instance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update printer {pid} state.", instance._pid);
            }
        }
    }

    /// <summary>
    /// Updates the state information for a single registered printer instance.
    /// Polls the printer's current status, checks for finished prints, and synchronizes database state.
    /// </summary>
    /// <param name="instance">The registered printer instance to update.</param>
    /// <returns>A task representing the asynchronous state update operation.</returns>
    private async Task UpdatePrinterStateAsync(RegisteredInstance instance)
    {
        try
        {
            // retrieve printer state, update existing tracked state in PMS
            MinimalStateReport? previousState = null;
            previousState = _registeredInstances.GetValueOrDefault(instance._pid)._state;
            if (previousState != null)
            {
                var currentState = await _octoHelper.GetPrinterStateMinimal(instance._clientConnection);

                // check for any finished prints at this printer
                if (instance._activePrintId != -1)
                    await RunFinishedPrintCheckAsync(instance);

                // update existing PMS state, then update tracked state in DB
                instance._state = new MinimalStateReport(currentState.Operational, currentState.Printing,
                    currentState.Error, currentState.Ready);
                var result = await _databaseAccessHelper.Printers.SetPrinterCurrentlyPrintingStatusAsync(instance._pid,
                    currentState.Printing);

                // check for errors, record if necessary
                if (currentState.Error)
                    await _databaseAccessHelper.Maintenance.UpdatePrinterErrorCountAsync(instance._pid);
                if (result != TransactionResult.Succeeded)
                {
                    if (result == TransactionResult.NotFound)
                        _logger.LogWarning("Printer {pid} not found.", instance._pid);
                    else
                        _logger.LogError("Failed to update printer currently printing status for printer {pid}.",
                            instance._pid);
                }

                // increment record of Printer uptime in DB if Printer is Operational, Ready, or Printing
                bool printerReflectsActive = currentState.Operational || currentState.Ready || currentState.Printing;
                bool systemReflectsActive = (await _databaseAccessHelper.Printers.GetPrinterAsync(instance._pid)).Enabled;
                if (printerReflectsActive && systemReflectsActive)
                {
                    _databaseAccessHelper.Maintenance.UpdatePrinterUptime(instance._pid); // default to 10 sec. to maintain sync with PMS poll-rate
                }

                // update extruder-head thermals
                decimal tempC = (decimal)await MaintenanceHelpers.GetCurrentPrinterToolTemp(_octoHelper, instance, _logger);
                _databaseAccessHelper.Maintenance.UpdatePrinterTempC(instance._pid, tempC);
                decimal tempF = (tempC * (9 / 5)) + 32;
                _databaseAccessHelper.Maintenance.UpdatePrinterTempF(instance._pid, tempF);

            }
            else
            {
                _logger.LogWarning("Printer {pid} has a null state.", instance._pid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update state of printer {pid}.", instance._pid);
        }
    }

    #endregion

    #endregion

    #region Hardware Query Helpers

    /// <summary>
    ///     Performs a check for any unconnected yet registered
    ///     printers.
    /// </summary>
    /// <returns></returns>
    private async Task RunConnectionCheckAsync()
    {
        // check for registered, yet unconnected printers
        var unConnected = new List<int>();
        foreach (var pair in _registeredInstances)
            if (!pair.Value._connected)
                unConnected.Add(pair.Key);
        if (unConnected.Count > 0)
            await ReconnectPrintersAsync(unConnected);
    }

    /// <summary>
    ///     Queries a printers connection status
    ///     in the PMS.
    /// </summary>
    /// <param name="clientConnection">The connection to the printer</param>
    /// <returns></returns>
    private async Task<bool> QueryPrinterConnectionStatusAsync(HttpClient clientConnection)
    {
        try
        {
            _logger.LogInformation("Querying printer connection status.");
            var connResponse = await _octoHelper.Connect(clientConnection);
            connResponse.EnsureSuccessStatusCode();
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to printer.");
            return false;
        }
    }

    /// <summary>
    /// Attempts to reconnect all database-configured and PMS-registered printers that are currently disconnected.
    /// Iterates through each disconnected printer and attempts to reestablish communication.
    /// </summary>
    /// <param name="unConnected">List of printer IDs that are currently disconnected.</param>
    /// <returns>A task representing the asynchronous reconnection operation.</returns>
    private async Task ReconnectPrintersAsync(List<int> unConnected)
    {
        _logger.LogInformation("Unconnected printers, attempting reconnection...");
        foreach (var printerId in unConnected)
        {
            var clientRegisteredPrinter = _registeredInstances[printerId];
            {
                _logger.LogInformation("Attempting reconnection for printer {pid} ({handle})...",
                    clientRegisteredPrinter._pid, clientRegisteredPrinter._handle);
                var connected = await QueryPrinterConnectionStatusAsync(clientRegisteredPrinter._clientConnection);
                if (connected)
                {
                    _logger.LogInformation("Reconnected to printer {pid}.", clientRegisteredPrinter._pid);
                    _registeredInstances[printerId]._connected = true;
                    _registeredInstances[printerId]._state =
                        await _octoHelper.GetPrinterStateMinimal(_registeredInstances[printerId]._clientConnection);
                }
                else
                {
                    _logger.LogWarning("Failed to reconnect printer {pid}.", clientRegisteredPrinter._pid);
                }
            }
        }
    }

    /// <summary>
    ///     Method for quick, single-printer retrieval;
    ///     'available' means operational=1, printing=0, error=0, ready=1,
    ///     'Enabled' is true in DB, and active PrintId has sentinel value
    ///     of -1.
    /// </summary>
    /// <returns>Free printer for printing</returns>
    private async Task<RegisteredInstance?> GetFirstAvailablePrinterAsync()
    {
        RegisteredInstance? available = null;
        foreach (var registered in _registeredInstances)
        {
            var state = (MinimalStateReport)registered.Value._state!;
            var isEnabled =
                await _databaseAccessHelper.Printers.GetPrinterEnabledStatusAsync(registered.Key);
            if (!isEnabled.HasValue)
            {
                _logger.LogError("Printer {pid} does not exist in the database.", registered.Key);
            }
            else // won't consider printers that are disabled from the operator interface
            {
                if (state is { Operational: true, Printing: false, Error: false, Ready: true } && isEnabled.Value &&
                    registered.Value._activePrintId == -1)
                {
                    available = registered.Value;
                    break;
                }
            }
        }

        return await Task.FromResult(available);
    }

    #endregion
}
