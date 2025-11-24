using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DatabaseAccess;
using DatabaseAccess.Models;
using Microsoft.Extensions.Logging;

namespace EmailService.Services;

/// <summary>
/// Evaluates saved auto-reply rules and sends an email when one matches
/// the current date/time.
/// </summary>
public sealed class EmailAutoReplyService
{
    private readonly DatabaseAccessHelper _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailAutoReplyService> _logger;
    private readonly TimeZoneInfo _timeZone;

    public EmailAutoReplyService(
        DatabaseAccessHelper db,
        IEmailSender emailSender,
        ILogger<EmailAutoReplyService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;

        // Use the server's local time zone. You can swap this for a specific zone if needed.
        _timeZone = TimeZoneInfo.Local;
    }

    /// <summary>
    /// Checks the current time against your configured rules and sends
    /// an auto-reply to <paramref name="toAddress"/> if any rule matches.
    /// </summary>
    public async Task SendAutoReplyIfNeededAsync(
        string toAddress,
        CancellationToken ct = default)
    {
        var nowUtc = DateTimeOffset.UtcNow;

        // Pull all rules from the DB (same helper your Config screen uses).
        var rules = await _db.EmailAutoReplyRules.GetAllAsync();

        var matchingRule = rules
            .Where(r => r.Isenabled)
            .Where(r => RuleMatches(r, nowUtc, _timeZone))
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Emailautoreplyruleid)
            .FirstOrDefault();

        if (matchingRule is null)
        {
            _logger.LogDebug(
                "No auto-reply rule matched at {Now} for {Recipient}",
                nowUtc, toAddress);
            return;
        }

        // If the operator accidentally saved an empty template, skip.
        if (string.IsNullOrWhiteSpace(matchingRule.Subject) &&
            string.IsNullOrWhiteSpace(matchingRule.Body))
        {
            _logger.LogDebug(
                "Matched rule {RuleId} but subject/body were empty; skipping auto-reply",
                matchingRule.Emailautoreplyruleid);
            return;
        }

        await _emailSender.SendAsync(
            to: toAddress,
            subject: matchingRule.Subject,
            htmlBody: matchingRule.Body,
            ct: ct);

        _logger.LogInformation(
            "Sent auto-reply using rule {RuleId} to {Recipient}",
            matchingRule.Emailautoreplyruleid, toAddress);
    }

    // ----------------- Matching logic -----------------

    /// <summary>
    /// Returns true if the given rule should fire at the specified UTC time.
    /// </summary>
    private static bool RuleMatches(
        Emailautoreplyrule rule,
        DateTimeOffset nowUtc,
        TimeZoneInfo tz)
    {
        // Convert "now" into the configured local time zone.
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, tz);

        var today = DateOnly.FromDateTime(localNow.Date);
        var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);

        // 1) Optional date window: if set, today must be between Startdate and Enddate.
        if (rule.Startdate.HasValue && today < rule.Startdate.Value)
            return false;

        if (rule.Enddate.HasValue && today > rule.Enddate.Value)
            return false;

        // 2) Determine rule type (int -> enum).
        var ruleType = (Emailautoreplyrule.EmailRuleType)rule.Ruletype;

        // Out-of-office rule: if we are within the date window, it always matches.
        if (ruleType == Emailautoreplyrule.EmailRuleType.OutOfOffice)
            return true;

        // TimeWindow rule: also cares about day-of-week and time of day.
        var todayFlag = DayOfWeekToFlag(localNow.DayOfWeek);
        var ruleDays = (Emailautoreplyrule.DayOfWeekFlags)rule.Daysofweek;

        // If today's flag isn't present in the rule's DayOfWeek flags, no match.
        if ((ruleDays & todayFlag) == Emailautoreplyrule.DayOfWeekFlags.None)
            return false;

        // If no times are set, treat as "all day" on those days.
        if (rule.Starttime is null || rule.Endtime is null)
            return true;

        var start = rule.Starttime.Value;
        var end   = rule.Endtime.Value;

        if (start <= end)
        {
            // Normal window: e.g., 08:00–17:00 (same day).
            return localTime >= start && localTime <= end;
        }

        // Window that crosses midnight: e.g., 18:00–02:00.
        return localTime >= start || localTime <= end;
    }

    private static Emailautoreplyrule.DayOfWeekFlags DayOfWeekToFlag(DayOfWeek day) =>
        day switch
        {
            DayOfWeek.Sunday    => Emailautoreplyrule.DayOfWeekFlags.Sunday,
            DayOfWeek.Monday    => Emailautoreplyrule.DayOfWeekFlags.Monday,
            DayOfWeek.Tuesday   => Emailautoreplyrule.DayOfWeekFlags.Tuesday,
            DayOfWeek.Wednesday => Emailautoreplyrule.DayOfWeekFlags.Wednesday,
            DayOfWeek.Thursday  => Emailautoreplyrule.DayOfWeekFlags.Thursday,
            DayOfWeek.Friday    => Emailautoreplyrule.DayOfWeekFlags.Friday,
            DayOfWeek.Saturday  => Emailautoreplyrule.DayOfWeekFlags.Saturday,
            _                   => Emailautoreplyrule.DayOfWeekFlags.None
        };
}