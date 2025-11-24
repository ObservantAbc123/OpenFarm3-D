namespace DatabaseAccess;

/// <summary>
///     Represents the result of a database transaction operation.
/// </summary>
public enum TransactionResult
{
    /// <summary>
    ///     The transaction completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    ///     The transaction failed due to a database error.
    /// </summary>
    Failed,

    /// <summary>
    ///     The requested entity was not found.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The transaction was abandoned.
    /// </summary>
    Abandoned,

    /// <summary>
    ///     No action was taken.
    /// </summary>
    NoAction
}