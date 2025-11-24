using DatabaseAccess;
using DatabaseAccess.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Forms.v1;
using Google.Apis.Forms.v1.Data;
using Google.Apis.Services;
using RabbitMQHelper;
using RabbitMQHelper.MessageTypes;

namespace google_form_listener;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IRmqHelper _rmqHelper;
    private readonly FormsService _formsService;
    private readonly DatabaseAccessHelper _databaseAccessHelper;

    /// <summary>
    /// Google form ID we should monitor for responses. Set in .env
    /// </summary>
    private readonly string? _formId;

    /// <summary>
    /// Tracks the last time we got responses from the form. The first run after restart, we check all responses.
    /// After that, only responses submitted since the last time we checked are pulled.
    /// </summary>
    private DateTimeOffset? _lastFormProcessTime;

    /// <summary>
    /// Holds the question IDs for responses. Is populated by querying for the form on the first loop the service runs after starting.
    /// </summary>
    private Dictionary<string, string> _questionIds = new Dictionary<string, string>();

    private bool _questionIdsParsed = false;

    public Worker(ILogger<Worker> logger, IRmqHelper rmqHelper)
    {
        _logger = logger;
        _rmqHelper = rmqHelper;

        // Setup Database access
        string? conn = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        if (conn is null)
            throw new NullReferenceException("Environment variables are not set for connection string configuration");
        _databaseAccessHelper = new DatabaseAccessHelper(conn);

        // Google form credentials
        String? credFilePath = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_CRED_FILE");

        var credential = GoogleCredential.FromFile(credFilePath)
            .CreateScoped(FormsService.Scope.FormsResponsesReadonly, FormsService.Scope.FormsBodyReadonly);

        _formsService = new FormsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "OpenFarm Listener"
        });

        _formId = Environment.GetEnvironmentVariable("GOOGLE_FORM_ID");

        // Add keys that we need to find questionIds for.
        // The google form must be setup correctly following the instructions in the README
        _questionIds.Add("Name", "");
        _questionIds.Add("File", "");
        _questionIds.Add("copies", "");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            // Check RMQHelper for connection, do not process responses if we aren't able to send messages.
            if (!_rmqHelper.IsConnected())
            {
                _logger.LogInformation("RMQ connection is not available, connecting...");
                bool connResult = await _rmqHelper.Connect();
                if (connResult)
                    _logger.LogInformation("RMQ connection acquired");
                else
                {
                    _logger.LogError("RMQ connection could not be acquired");
                    await Task.Delay(15000, stoppingToken);
                    continue;
                }
            }

            // The first time the worker runs, we must get the question IDs from the form.
            if (!_questionIdsParsed)
            {
                Form? form = await _formsService.Forms.Get(_formId).ExecuteAsync(stoppingToken);
                ParseQuestionIds(form);
            }

            var listResponsesRequest = _formsService.Forms.Responses.List(_formId);

            // Will be null on the first run after a restart.
            if (_lastFormProcessTime is not null)
            {
                // Google Forms filter requires RFC3339 timestamp string
                string timeStampString = _lastFormProcessTime.Value.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFF'Z'");
                listResponsesRequest.Filter = $"timestamp > {timeStampString}";
            }

            _logger.LogInformation($"Requesting Responses...");
            ListFormResponsesResponse? responseResult = await listResponsesRequest.ExecuteAsync(stoppingToken);

            if (responseResult?.Responses is null)
                _logger.LogInformation("No responses received");
            else
            {
                _logger.LogInformation($"New responses received: {responseResult.Responses.Count}");

                foreach (var response in responseResult.Responses)
                {
                    try
                    {
                        // Track the most recently submitted form that we have processed
                        if (_lastFormProcessTime is null || response.LastSubmittedTimeDateTimeOffset > _lastFormProcessTime)
                            _lastFormProcessTime = response.LastSubmittedTimeDateTimeOffset;

                        // Check if there is already a job in the DB for this responseID
                        PrintJob? existingJob = await _databaseAccessHelper.PrintJobs.GetPrintJobByResponseIdAsync(response.ResponseId);
                        if (existingJob is not null)
                        {
                            _logger.LogInformation($"Found existing job for responseID: {response.ResponseId}");
                            continue;
                        }

                        string? fileId = "";
                        string? userName = null;
                        int numCopies = 1;

                        _logger.LogInformation($"Started Processing ResponseId: {response.ResponseId}, Email: {response.RespondentEmail}");
                        foreach (var answer in response.Answers)
                            if (_questionIds["Name"] == answer.Key)
                                userName = answer.Value.TextAnswers.Answers.First().Value;
                            else if (_questionIds["File"] == answer.Key)
                                fileId = answer.Value.FileUploadAnswers.Answers.First().FileId;
                            else if (_questionIds["copies"] == answer.Key)
                                numCopies = int.Parse(answer.Value.TextAnswers.Answers.First().Value);

                        // Verify fileID is present
                        if (string.IsNullOrWhiteSpace(fileId))
                        {
                            _logger.LogError($"Unable to get fileId for email: {response.RespondentEmail}, responseID: {response.ResponseId}");
                            await _rmqHelper.QueueMessage(ExchangeNames.JobRejected, new RejectMessage()
                            {
                                JobId = 0,
                                Email = response.RespondentEmail,
                                RejectReason = RejectReason.JobSubmissionFailed
                            });
                            continue;
                        }

                        // Verify we have a user or create one if this is their first submission
                        User? user = await _databaseAccessHelper.Users.CreateOrGetUserByEmailAsync(response.RespondentEmail, userName ?? "");
                        if (user is null)
                        {
                            _logger.LogError($"Unable to create or get user for email: {response.RespondentEmail}, responseID: {response.ResponseId}");
                            await _rmqHelper.QueueMessage(ExchangeNames.JobRejected, new RejectMessage()
                            {
                                JobId = 0,
                                Email = response.RespondentEmail,
                                RejectReason = RejectReason.JobSubmissionFailed
                            });
                            continue;
                        }

                        // Create job
                        PrintJob job = await _databaseAccessHelper.PrintJobs.CreatePrintJobAsync(user.Id, response.ResponseId, numCopies, response.CreateTimeDateTimeOffset!.Value.DateTime);
                        await _rmqHelper.QueueMessage(ExchangeNames.JobAccepted, new AcceptMessage()
                        {
                            DownloadType = DownloadType.GoogleDrive,
                            DownloadUrl = fileId,
                            JobId = job.Id
                        });
                        _logger.LogInformation($"Finished processing ResponseId: {response.ResponseId}");
                    }
                    catch (Exception ex)
                    {
                        await _rmqHelper.QueueMessage(ExchangeNames.JobRejected, new RejectMessage()
                        {
                            JobId = 0,
                            Email = response.RespondentEmail,
                            RejectReason = RejectReason.JobSubmissionFailed
                        });
                        _logger.LogError(ex, $"Error processing: {response.RespondentEmail}, responseID: {response.ResponseId}");
                    }
                }
            }

            await Task.Delay(60000, stoppingToken);
        }
    }

    private void ParseQuestionIds(Form? form)
    {
        if (form is null)
            throw new NullReferenceException("Could not get the google submission form. Ensure .env values are properly set.");
        foreach (KeyValuePair<string, string> kvp in _questionIds)
        {
            Item item = form.Items.Single(item => item.Title.Contains(kvp.Key));
            _questionIds[kvp.Key] = item.QuestionItem.Question.QuestionId;
        }
        _questionIdsParsed = true;
    }
}
