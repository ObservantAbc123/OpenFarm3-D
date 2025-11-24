using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying <see cref="Color" /> entities in the database.
/// </summary>
/// <param name="context">The database context to use for operations.</param>
public class ColorHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<Color> ColorsAsNoTracking => _context.Colors.AsNoTracking();

    private IQueryable<Color> OrderedColorsAsNoTracking => ColorsAsNoTracking.OrderBy(color => color.Name);

    private static string NormalizeColorName(string colorName)
    {
        return colorName.Trim().ToLowerInvariant();
    }

    private Task<Color?> FindByNormalizedNameAsync(string normalizedName)
    {
        return ColorsAsNoTracking.FirstOrDefaultAsync(color => color.Name == normalizedName);
    }

    /// <summary>
    ///     Asynchronously retrieves all <see cref="Color" /> entities from the database.
    /// </summary>
    /// <returns>A task that resolves to all colors sorted alphabetically.</returns>
    public async Task<List<Color>> GetColorsAsync()
    {
        return await OrderedColorsAsNoTracking.ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves all <see cref="Color" /> names from the database.
    /// </summary>
    /// <returns>A task that resolves to all color names sorted alphabetically.</returns>
    public async Task<List<string>> GetColorsNamesAsync()
    {
        return await OrderedColorsAsNoTracking
            .Select(color => color.Name)
            .ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves a <see cref="Color" /> by identifier.
    /// </summary>
    /// <param name="colorId">The identifier of the <see cref="Color" /> to retrieve.</param>
    /// <returns>The matching color, or <c>null</c> if not found.</returns>
    public async Task<Color?> GetColorAsync(int colorId)
    {
        if (colorId <= 0)
            return null;

        return await ColorsAsNoTracking.FirstOrDefaultAsync(color => color.Id == colorId);
    }

    /// <summary>
    ///     Asynchronously retrieves a <see cref="Color" /> by name (case-insensitive).
    /// </summary>
    /// <param name="colorName">The name of the color to retrieve.</param>
    /// <returns>The matching color, or <c>null</c> if not found.</returns>
    public async Task<Color?> GetColorAsync(string colorName)
    {
        if (string.IsNullOrWhiteSpace(colorName))
            return null;

        return await FindByNormalizedNameAsync(NormalizeColorName(colorName));
    }

    /// <summary>
    ///     Asynchronously retrieves the name of the <see cref="Color" /> with the specified identifier.
    /// </summary>
    /// <param name="colorId">The identifier of the <see cref="Color" />.</param>
    /// <returns>The color name, or <c>null</c> if not found.</returns>
    public async Task<string?> GetColorNameAsync(int colorId)
    {
        if (colorId <= 0)
            return null;

        return await ColorsAsNoTracking
            .Where(color => color.Id == colorId)
            .Select(color => color.Name)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the identifier of the <see cref="Color" /> with the specified name (case-insensitive).
    /// </summary>
    /// <param name="colorName">The name of the <see cref="Color" />.</param>
    /// <returns>The color identifier, or <c>null</c> if not found.</returns>
    public async Task<int?> GetColorIdAsync(string colorName)
    {
        if (string.IsNullOrWhiteSpace(colorName))
            return null;

        return await ColorsAsNoTracking
            .Where(color => color.Name == NormalizeColorName(colorName))
            .Select(color => (int?)color.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously creates a new <see cref="Color" /> with the specified name.
    /// </summary>
    /// <param name="colorName">The name of the new color.</param>
    /// <returns>A <see cref="TransactionResult" /> describing the outcome.</returns>
    public async Task<TransactionResult> CreateColorAsync(string colorName)
    {
        if (string.IsNullOrWhiteSpace(colorName))
            return TransactionResult.NoAction;

        try
        {
            var normalizedName = NormalizeColorName(colorName);

            if (await ColorsAsNoTracking.AnyAsync(color => color.Name == normalizedName))
                return TransactionResult.NoAction;

            var color = new Color { Name = normalizedName };
            _context.Colors.Add(color);
            await _context.SaveChangesAsync();

            return TransactionResult.Succeeded;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _context.ChangeTracker.Clear();
            return TransactionResult.NoAction;
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return TransactionResult.Failed;
        }
    }

    /// <summary>
    ///     Asynchronously deletes a <see cref="Color" /> only if it is not referenced by any dependent entities.
    /// </summary>
    /// <param name="colorId">The identifier of the color to delete.</param>
    /// <returns>A tuple containing the result and any dependent materials.</returns>
    public async Task<(TransactionResult Result, List<Material> DependentMaterials)>
        DeleteColorRestrictedAsync(int colorId)
    {
        if (colorId <= 0)
            return (TransactionResult.NotFound, []);

        try
        {
            var color = await _context.Colors.FindAsync(colorId);

            if (color == null)
                return (TransactionResult.NotFound, []);

            var dependentMaterials = await _context.Materials
                .AsNoTracking()
                .Where(material => material.MaterialColorId == color.Id)
                .ToListAsync();

            if (dependentMaterials.Count > 0)
                return (TransactionResult.Abandoned, dependentMaterials);

            _context.Colors.Remove(color);
            await _context.SaveChangesAsync();

            return (TransactionResult.Succeeded, []);
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return (TransactionResult.Failed, []);
        }
    }

    /// <summary>
    ///     Asynchronously deletes a <see cref="Color" /> and all dependent entities (cascading delete).
    /// </summary>
    /// <param name="colorId">The identifier of the color to delete.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<TransactionResult> DeleteColorCascadingAsync(int colorId)
    {
        if (colorId <= 0)
            return TransactionResult.NotFound;

        try
        {
            var color = await _context.Colors.FindAsync(colorId);

            if (color == null)
                return TransactionResult.NotFound;

            _context.Colors.Remove(color);
            await _context.SaveChangesAsync();

            return TransactionResult.Succeeded;
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return TransactionResult.Failed;
        }
    }
}