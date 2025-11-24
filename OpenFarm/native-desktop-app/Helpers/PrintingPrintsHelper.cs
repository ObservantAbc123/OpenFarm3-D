using System;
using DatabaseAccess.Models;

namespace native_desktop_app.ViewModels;

/// <summary>
///     Represents a single, actively running print on the dashboard.
/// </summary>
/// <remarks>
///     This lightweight row view model (UI DTO) flattens/derives the fields
///     the Printing panel needs to render one line per <see cref="Print" />.
///     It is constructed in <see cref="HomeViewModel" /> by joining:
///     <list type="bullet">
///         <item>
///             <description>The <see cref="Print" /> entity (source of truth for start/finish/status).</description>
///         </item>
///         <item>
///             <description>The parent <c>PrintJobId</c> for display (e.g., "PR-123").</description>
///         </item>
///         <item>
///             <description>The resolved <c>PrinterName</c> from <c>PrinterId</c> (or “(unassigned)”).</description>
///         </item>
///     </list>
///     By projecting to this type, the XAML doesn’t need to dereference navigation
///     properties or perform lookups at render time.
/// </remarks>
public sealed class PrintingPrintsHelper
{
    /// <summary>
    ///     Gets the underlying <see cref="Print" /> record represented by this row.
    /// </summary>
    /// <value>
    ///     The domain object that contains canonical timestamps and status
    ///     (e.g., <see cref="DatabaseAccess.Models.Print.StartedAt" />, <see cref="DatabaseAccess.Models.Print.FinishedAt" />,
    ///     <see cref="DatabaseAccess.Models.Print.PrintStatus" />).
    /// </value>
    public required Print Print { get; init; }

    /// <summary>
    ///     Gets the identifier of the parent print job for this print.
    /// </summary>
    /// <value>
    ///     Used for human-friendly display (e.g., “PR-12345”) and for navigation
    ///     to job details.
    /// </value>
    public required long PrintJobId { get; init; }

    /// <summary>
    ///     Gets the human-readable name of the printer running this print.
    /// </summary>
    /// <value>
    ///     Resolved from <c>PrinterId</c>; falls back to “(unassigned)” if no printer
    ///     is currently associated.
    /// </value>
    public required string PrinterName { get; init; }

    /// <summary>
    ///     Gets the UTC timestamp when the print started, if any.
    /// </summary>
    /// <remarks>
    ///     Convenience pass-through to <see cref="Print.StartedAt" /> for binding.
    /// </remarks>
    public DateTime? StartedAt => Print.StartedAt;
}