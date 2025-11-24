using DatabaseAccess.Models;

namespace native_desktop_app.ViewModels;

/// <summary>
///     Represents a single printer entry in the Printers view.
/// </summary>
/// <remarks>
///     This is a lightweight data-transfer view model (row model) used to combine
///     database entities and precomputed display values for the UI.
///     It is constructed in <see cref="PrintersViewModel" /> and bound to
///     each row of the <c>ItemsControl</c> in <c>PrintersView.axaml</c>.
///     <para>
///         Unlike <see cref="Printer" />, which represents the database entity,
///         this class adds convenience properties such as <see cref="ModelName" />
///         that are already resolved for display (e.g., so the UI doesn’t need to
///         dereference navigation properties).
///     </para>
/// </remarks>
public sealed class PrinterRow
{
    /// <summary>
    ///     Gets or sets the underlying <see cref="Printer" /> entity
    ///     associated with this row.
    /// </summary>
    /// <value>
    ///     The complete printer database record. Used for commands such as
    ///     "Delete" or "Toggle Enabled" that require the entity’s ID.
    /// </value>
    public required Printer Printer { get; init; }

    /// <summary>
    ///     Gets or sets the display name of the printer’s model.
    /// </summary>
    /// <value>
    ///     The human-readable model name (e.g., "MK4S" or "Gigabot").
    ///     This value is pre-resolved in <see cref="PrintersViewModel" />
    ///     by joining printer and model data in memory.
    /// </value>
    public required string ModelName { get; init; }
}