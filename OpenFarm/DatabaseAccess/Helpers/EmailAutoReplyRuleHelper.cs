using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

public class EmailAutoReplyRuleHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<Emailautoreplyrule> RulesAsNoTracking =>
        _context.Emailautoreplyrules.AsNoTracking();

    private IQueryable<Emailautoreplyrule> EnabledRules =>
        RulesAsNoTracking.Where(r => r.Isenabled);

    /// <summary>
    /// Get all rules ordered by priority.
    /// </summary>
    public async Task<List<Emailautoreplyrule>> GetAllAsync()
    {
        return await RulesAsNoTracking
            .OrderBy(r => r.Priority)
            .ToListAsync();
    }

    /// <summary>
    /// Get a single rule by id.
    /// </summary>
    public async Task<Emailautoreplyrule?> GetByIdAsync(int id)
    {
        return await RulesAsNoTracking
            .SingleOrDefaultAsync(r => r.Emailautoreplyruleid == id);
    }

    /// <summary>
    /// Create a new auto-reply rule.
    /// </summary>
    public async Task<Emailautoreplyrule> AddAsync(
        string label,
        Emailautoreplyrule.EmailRuleType ruleType,
        DateOnly? startDate,
        DateOnly? endDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        Emailautoreplyrule.DayOfWeekFlags daysOfWeek,
        string subject,
        string body,
        bool isEnabled = true,
        int priority = 100)
    {
        var now = DateTime.UtcNow;

        var rule = new Emailautoreplyrule
        {
            Label        = label,
            Ruletype     = (int)ruleType,
            Startdate    = startDate,
            Enddate      = endDate,
            Starttime    = startTime,
            Endtime      = endTime,
            Daysofweek   = (int)daysOfWeek,
            Createdatutc = now,                // ✅ DateTime, not DateOnly
            Updatedatutc = now,                // ✅ DateTime, not DateOnly
            Subject      = subject,
            Body         = body,
            Isenabled    = isEnabled,
            Priority     = priority
        };

        _context.Emailautoreplyrules.Add(rule);
        await _context.SaveChangesAsync();

        return rule;
    }

    public async Task UpdateAsync(Emailautoreplyrule rule)
    {
        rule.Updatedatutc = DateTime.UtcNow;   // ✅ DateTime, not DateOnly

        _context.Emailautoreplyrules.Update(rule);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete a rule by id.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var rule = await _context.Emailautoreplyrules.FindAsync(id);
        if (rule is null)
            return;

        _context.Emailautoreplyrules.Remove(rule);
        await _context.SaveChangesAsync();
    }
}