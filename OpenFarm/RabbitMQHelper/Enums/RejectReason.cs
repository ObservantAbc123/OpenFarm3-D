namespace RabbitMQHelper;

public enum RejectReason
{
    CancelledByUser,
    FailedValidation,
    RejectedByApprover,
    FailedToPrint,
    FailedDownload,
    JobSubmissionFailed
}