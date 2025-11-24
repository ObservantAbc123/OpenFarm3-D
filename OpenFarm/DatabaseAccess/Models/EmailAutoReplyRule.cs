using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("emailautoreplyrules")]
public partial class Emailautoreplyrule
{
    public enum EmailRuleType
    {
        OutOfOffice = 0,
        TimeWindow = 1
    }

    [Flags]
    public enum DayOfWeekFlags
    {
        None    = 0,
        Monday  = 1 << 0,
        Tuesday = 1 << 1,
        Wednesday = 1 << 2,
        Thursday  = 1 << 3,
        Friday    = 1 << 4,
        Saturday  = 1 << 5,
        Sunday    = 1 << 6,
        Weekdays  = Monday | Tuesday | Wednesday | Thursday | Friday,
        Weekends  = Saturday | Sunday,
        All       = Weekdays | Weekends
    }
    
    [Key]
    [Column("emailautoreplyruleid")]
    public int Emailautoreplyruleid { get; set; }

    [Column("label")]
    public string Label { get; set; } = null!;

    [Column("ruletype")]
    public int Ruletype { get; set; }

    [Column("startdate")]
    public DateOnly? Startdate { get; set; }

    [Column("enddate")]
    public DateOnly? Enddate { get; set; }

    [Column("starttime")]
    public TimeOnly? Starttime { get; set; }

    [Column("endtime")]
    public TimeOnly? Endtime { get; set; }

    [Column("daysofweek")]
    public int Daysofweek { get; set; }

    [Column("createdatutc")]
    public DateTime Createdatutc { get; set; }

    [Column("updatedatutc")]
    public DateTime Updatedatutc { get; set; }

    [Column("subject")]
    public string Subject { get; set; } = null!;

    [Column("body")]
    public string Body { get; set; } = null!;

    [Column("isenabled")]
    public bool Isenabled { get; set; }

    [Column("priority")]
    public int Priority { get; set; }
}
