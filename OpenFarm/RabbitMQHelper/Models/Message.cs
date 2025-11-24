using System.Text.Json.Serialization;

namespace RabbitMQHelper.MessageTypes;

public class Message
{
    [JsonInclude] public long JobId;
}

public class EmailMessage : Message
{
    [JsonInclude] public required string Email;
}

public class RejectMessage : Message
{
    [JsonInclude] public string? Email;

    [JsonInclude] public RejectReason RejectReason;
}

public class AcceptMessage : Message
{
    [JsonInclude] public DownloadType DownloadType;

    [JsonInclude] public required string DownloadUrl;
}

public class PrintStartedMessage : Message
{
    [JsonInclude] public int PrinterId;

    [JsonInclude] public TimeSpan PrintTime;

    [JsonInclude] public DateTime StartTime;
}

public class PrintFinishedMessage : Message
{
    [JsonInclude] public int PrinterId;
}

public class PrintClearedMessage : Message
{
    [JsonInclude]
    public int PrinterId;
    [JsonInclude]
    public DateTime FinishTime;
}

public class OperatorReplyMessage : Message
{
    [JsonInclude]
    public required string CustomerEmail;
    [JsonInclude]
    public required string Subject;
    [JsonInclude]
    public required string Body;
    [JsonInclude]
    public long ThreadId;
    [JsonInclude]
    public long MessageId;
}
