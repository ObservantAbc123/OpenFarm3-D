using System.Linq.Expressions;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

/// <summary>
///     Provides helper methods for accessing and modifying MaterialType entities in the database.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="MaterialTypeHelper" /> class.
/// </remarks>
/// <param name="context">The database context to use for operations.</param>
public class MaterialTypeHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<MaterialType> MaterialTypes => _context.MaterialTypes.AsNoTracking();

    private IQueryable<Material> Materials => _context.Materials.AsNoTracking();

    /// <summary>
    ///     Asynchronously retrieves all <see cref="MaterialType" /> entities from the database.
    /// </summary>
    /// <returns>
    ///     A task that resolves to a list of all material types.
    /// </returns>
    public async Task<List<MaterialType>> GetMaterialTypesAsync()
    {
        return await MaterialTypes.ToListAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves a <see cref="MaterialType" /> entity with the given identifier.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to look up.</param>
    /// <returns>
    ///     A task that resolves to the material type, or <c>null</c> if not found.
    /// </returns>
    public async Task<MaterialType?> GetMaterialTypeAsync(int materialTypeId)
    {
        return await MaterialTypes.FirstOrDefaultAsync(type => type.Id == materialTypeId);
    }

    /// <summary>
    ///     Asynchronously retrieves a <see cref="MaterialType" /> entity with the given name.
    /// </summary>
    /// <param name="materialTypeName">The name of the material type to look up.</param>
    /// <returns>
    ///     A task that resolves to the material type, or <c>null</c> if not found.
    /// </returns>
    public async Task<MaterialType?> GetMaterialTypeAsync(string materialTypeName)
    {
        return await MaterialTypes.FirstOrDefaultAsync(type => type.Type == materialTypeName);
    }

    /// <summary>
    ///     Asynchronously retrieves the name of the <see cref="MaterialType" /> entity with the given identifier.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to look up.</param>
    /// <returns>
    ///     A task that resolves to the material type's name, or <c>null</c> if not found.
    /// </returns>
    public async Task<string?> GetMaterialTypeNameAsync(int materialTypeId)
    {
        return await MaterialTypes
            .Where(type => type.Id == materialTypeId)
            .Select(type => type.Type)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the identifier of the <see cref="MaterialType" /> entity with the given name.
    /// </summary>
    /// <param name="materialTypeName">The name of the material type to look up.</param>
    /// <returns>
    ///     A task that resolves to the material type's identifier, or <c>null</c> if not found.
    /// </returns>
    public async Task<int?> GetMaterialTypeIdAsync(string materialTypeName)
    {
        return await MaterialTypes
            .Where(type => type.Type == materialTypeName)
            .Select(type => type.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the minimum bed temperature for a <see cref="MaterialType" /> with the given identifier.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to look up.</param>
    /// <returns>
    ///     A task that resolves to the material type's minimum bed temperature, or <c>null</c> if not found or not set.
    /// </returns>
    public async Task<int?> GetMaterialTypeBedTempFloor(int materialTypeId)
    {
        return await MaterialTypes
            .Where(type => type.Id == materialTypeId)
            .Select(type => type.BedTempFloor)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the maximum bed temperature for a <see cref="MaterialType" /> with the given identifier.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to look up.</param>
    /// <returns>
    ///     A task that resolves to the material type's maximum bed temperature, or <c>null</c> if not found or not set.
    /// </returns>
    public async Task<int?> GetMaterialTypeBedTempCeiling(int materialTypeId)
    {
        return await MaterialTypes
            .Where(type => type.Id == materialTypeId)
            .Select(type => type.BedTempCeiling)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the minimum print temperature for a <see cref="MaterialType" /> with the given identifier.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to look up.</param>
    /// <returns>
    ///     A task that resolves to the material type's minimum print temperature, or <c>null</c> if not found or not set.
    /// </returns>
    public async Task<int?> GetMaterialTypePrintTempFloor(int materialTypeId)
    {
        return await MaterialTypes
            .Where(type => type.Id == materialTypeId)
            .Select(type => type.PrintTempFloor)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously retrieves the maximum print temperature for a <see cref="MaterialType" /> with the given identifier.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to look up.</param>
    /// <returns>
    ///     A task that resolves to the material type's maximum print temperature, or <c>null</c> if not found or not set.
    /// </returns>
    public async Task<int?> GetMaterialTypePrintTempCeiling(int materialTypeId)
    {
        return await MaterialTypes
            .Where(type => type.Id == materialTypeId)
            .Select(type => type.PrintTempCeiling)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Asynchronously sets the bed temperature floor for a material type with the specified identifier.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to update.</param>
    /// <param name="bedTempFloor">The new bed temperature floor value, or null to clear it.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the material type doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public Task<TransactionResult> SetMaterialTypeBedTempFloor(int materialTypeId, int? bedTempFloor)
    {
        return UpdateMaterialTypeAsync(
            materialTypeId,
            type => type.BedTempFloor = bedTempFloor,
            type => type.BedTempFloor);
    }

    /// <summary>
    ///     Asynchronously sets the bed temperature ceiling for a material type with the specified identifier.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to update.</param>
    /// <param name="bedTempCeiling">The new bed temperature ceiling value, or null to clear it.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the material type doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public Task<TransactionResult> SetMaterialTypeBedTempCeiling(int materialTypeId, int? bedTempCeiling)
    {
        return UpdateMaterialTypeAsync(
            materialTypeId,
            type => type.BedTempCeiling = bedTempCeiling,
            type => type.BedTempCeiling);
    }

    /// <summary>
    ///     Asynchronously sets the print temperature floor for a material type with the specified identifier.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to update.</param>
    /// <param name="printTempFloor">The new print temperature floor value, or null to clear it.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the material type doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public Task<TransactionResult> SetMaterialTypePrintTempFloor(int materialTypeId, int? printTempFloor)
    {
        return UpdateMaterialTypeAsync(
            materialTypeId,
            type => type.PrintTempFloor = printTempFloor,
            type => type.PrintTempFloor);
    }

    /// <summary>
    ///     Asynchronously sets the print temperature ceiling for a material type with the specified identifier.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to update.</param>
    /// <param name="printTempCeiling">The new print temperature ceiling value, or null to clear it.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the material type doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public Task<TransactionResult> SetMaterialTypePrintTempCeiling(int materialTypeId, int? printTempCeiling)
    {
        return UpdateMaterialTypeAsync(
            materialTypeId,
            type => type.PrintTempCeiling = printTempCeiling,
            type => type.PrintTempCeiling);
    }

    /// <summary>
    ///     Applies the supplied update action to the material type with the specified identifier.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to update.</param>
    /// <param name="applyUpdates">The action used to modify the tracked material type.</param>
    /// <param name="modifiedProperties">The set of properties that should be marked as modified.</param>
    /// <returns>A <see cref="TransactionResult" /> describing the outcome of the update.</returns>
    private async Task<TransactionResult> UpdateMaterialTypeAsync(
        int materialTypeId,
        Action<MaterialType> applyUpdates,
        params Expression<Func<MaterialType, object?>>[] modifiedProperties)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var exists = await MaterialTypes.AnyAsync(type => type.Id == materialTypeId);

            if (!exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists, or null
            var trackedEntity = _context.ChangeTracker.Entries<MaterialType>()
                .FirstOrDefault(e => e.Entity.Id == materialTypeId);

            if (trackedEntity != null)
                // Detach the existing tracked entity
                trackedEntity.State = EntityState.Detached;

            var materialType = new MaterialType { Id = materialTypeId };
            _context.MaterialTypes.Attach(materialType);

            applyUpdates(materialType);

            var entry = _context.Entry(materialType);
            foreach (var property in modifiedProperties)
                entry.Property(property).IsModified = true;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            await transaction.RollbackAsync();
            return TransactionResult.Failed;
        }
    }

    /// <summary>
    ///     Asynchronously creates a new <see cref="MaterialType" /> with the specified name and commits it in a transaction.
    /// </summary>
    /// <param name="materialTypeName">The name of the new material type.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NoAction" /> if a material type with the same name already exists,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    /// <exception cref="DbUpdateException">
    ///     Thrown if saving changes to the database fails.
    /// </exception>
    public async Task<TransactionResult> CreateMaterialTypeAsync(string materialTypeName)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var exists = await MaterialTypes
                .AnyAsync(type => type.Type == materialTypeName);

            if (exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            await _context.MaterialTypes.AddAsync(new MaterialType { Type = materialTypeName });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            await transaction.RollbackAsync();
            return TransactionResult.Failed;
        }
    }

    /// <summary>
    ///     Asynchronously creates a new <see cref="MaterialType" /> with the specified name and temperature settings.
    /// </summary>
    /// <param name="materialTypeName">The name of the new material type.</param>
    /// <param name="bedTempFloor">The minimum bed temperature for this material type, or null if not specified.</param>
    /// <param name="bedTempCeiling">The maximum bed temperature for this material type, or null if not specified.</param>
    /// <param name="printTempFloor">The minimum print temperature for this material type, or null if not specified.</param>
    /// <param name="printTempCeiling">The maximum print temperature for this material type, or null if not specified.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NoAction" /> if a material type with the same name already exists,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public async Task<TransactionResult> CreateMaterialTypeWithTempsAsync(string materialTypeName, int? bedTempFloor,
        int? bedTempCeiling, int? printTempFloor, int? printTempCeiling)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var exists = await MaterialTypes
                .AnyAsync(type => type.Type == materialTypeName);

            if (exists)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NoAction;
            }

            await _context.MaterialTypes.AddAsync(new MaterialType
            {
                Type = materialTypeName,
                BedTempFloor = bedTempFloor,
                BedTempCeiling = bedTempCeiling,
                PrintTempFloor = printTempFloor,
                PrintTempCeiling = printTempCeiling
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            await transaction.RollbackAsync();
            return TransactionResult.Failed;
        }
    }

    /// <summary>
    ///     Asynchronously deletes a material type with the specified identifier if no materials depend on it.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to delete.</param>
    /// <returns>
    ///     A task that resolves to a tuple containing a <see cref="TransactionResult" /> and a list of dependent materials.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the material type doesn't exist,
    ///     <see cref="TransactionResult.Abandoned" /> with dependent materials if materials rely on this type,
    ///     <see cref="TransactionResult.Succeeded" /> with an empty list if successful, or
    ///     <see cref="TransactionResult.Failed" /> with an empty list if an error occurs.
    /// </returns>
    public async Task<(TransactionResult, List<Material>)> DeleteMaterialTypeRestrictedAsync(int materialTypeId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var materialType = await MaterialTypes
                .FirstOrDefaultAsync(type => type.Id == materialTypeId);

            if (materialType == null)
            {
                await transaction.RollbackAsync();
                return (TransactionResult.NotFound, []);
            }

            var reliantMaterials = await Materials
                .Where(material => material.MaterialTypeId == materialTypeId)
                .ToListAsync();

            if (reliantMaterials.Count > 0)
            {
                await transaction.RollbackAsync();
                return (TransactionResult.Abandoned, reliantMaterials);
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<MaterialType>()
                .FirstOrDefault(e => e.Entity.Id == materialTypeId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.MaterialTypes.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.MaterialTypes.Attach(materialType);
                _context.MaterialTypes.Remove(materialType);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return (TransactionResult.Succeeded, []);
        }
        catch
        {
            await transaction.RollbackAsync();
            return (TransactionResult.Failed, []);
        }
    }

    /// <summary>
    ///     Asynchronously deletes a material type with the specified identifier, cascading to delete all dependent materials.
    /// </summary>
    /// <param name="materialTypeId">The identifier of the material type to delete.</param>
    /// <returns>
    ///     A task that resolves to a <see cref="TransactionResult" />.
    ///     Returns <see cref="TransactionResult.NotFound" /> if the material type doesn't exist,
    ///     <see cref="TransactionResult.Succeeded" /> if successful, or
    ///     <see cref="TransactionResult.Failed" /> if an error occurs.
    /// </returns>
    public async Task<TransactionResult> DeleteMaterialTypeCascadingAsync(int materialTypeId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var materialType = await MaterialTypes
                .FirstOrDefaultAsync(type => type.Id == materialTypeId);

            if (materialType == null)
            {
                await transaction.RollbackAsync();
                return TransactionResult.NotFound;
            }

            // Get the tracked entity if it exists
            var trackedEntity = _context.ChangeTracker.Entries<MaterialType>()
                .FirstOrDefault(e => e.Entity.Id == materialTypeId);

            if (trackedEntity != null)
            {
                // Entity is already tracked, remove it directly
                _context.MaterialTypes.Remove(trackedEntity.Entity);
            }
            else
            {
                // Entity is not tracked, attach and remove
                _context.MaterialTypes.Attach(materialType);
                _context.MaterialTypes.Remove(materialType);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return TransactionResult.Succeeded;
        }
        catch
        {
            await transaction.RollbackAsync();
            return TransactionResult.Failed;
        }
    }
}