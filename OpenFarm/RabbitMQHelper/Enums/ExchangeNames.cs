namespace RabbitMQHelper;

/*
 * Defines exchange names.
 * Used to select exchange to publish to.
 * Exchange can push message to multiple queues.
 */
public enum ExchangeNames
{
    /// <summary>
    ///     Job has been received by a submission listener and been accepted into the system.
    /// </summary>
    JobAccepted,

    /// <summary>
    ///     The File Storage System has received a request to acquire a file and done so.
    /// </summary>
    FileReady,

    /// <summary>
    ///     The initial job validation has been completed and the job is ready for human review.
    /// </summary>
    JobValidated,

    /// <summary>
    ///     The job has been reviewed by a human and is now awaiting patron payment.
    /// </summary>
    JobApproved,

    /// <summary>
    ///     The job has been marked as paid and is now ready to be printed.
    /// </summary>
    JobPaid,

    /// <summary>
    ///     The job has been assigned a printer and begun printing.
    /// </summary>
    PrintStarted,

    /// <summary>
    ///     The job has been completed, but not yet removed from the printer.
    /// </summary>
    PrintFinished,

    /// <summary>
    ///     The print has been removed from the printer and is now ready for patron pickup.
    /// </summary>
    PrintCleared,

    /// <summary>
    ///     The print has been cancelled for some reason.
    /// </summary>
    JobRejected,
    JobCompleted,
    /// <summary>
    /// An operator has sent a reply to a customer that needs to be emailed.
    /// </summary>
    OperatorReply
}
