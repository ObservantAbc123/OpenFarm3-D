using System;
using DatabaseAccess.Models;

namespace native_desktop_app.ViewModels;

/// <summary>
///     Lightweight row model for the Users view.
///     Combines the DB user with pre-fetched display fields (primary email, created-at).
/// </summary>
public sealed class UserRow
{
    /// <summary>The underlying database user entity.</summary>
    public required User User { get; init; }

    /// <summary>The user's primary email, if any.</summary>
    public string PrimaryEmail { get; init; } = "—";

    /// <summary>The user's created-at in local time, if available.</summary>
    public DateTime? CreatedAtLocal { get; init; }

    /// <summary>Formatted for the UI (e.g. button/content binding).</summary>
    public string CreatedAtDisplay =>
        CreatedAtLocal.HasValue
            ? CreatedAtLocal.Value.ToString("MMM dd, yyyy")
            : "—";
}