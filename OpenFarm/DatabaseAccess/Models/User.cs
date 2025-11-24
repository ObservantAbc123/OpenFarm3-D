using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("users")]
[Index("OrgId", Name = "users_org_id_key", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string Name { get; set; } = null!;

    [Column("verified")]
    public bool? Verified { get; set; }

    [Column("suspended")]
    public bool Suspended { get; set; }

    [Column("org_id")]
    [StringLength(255)]
    public string? OrgId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }



    [InverseProperty("User")]
    public virtual ICollection<Email> Emails { get; set; } = new List<Email>();

    [InverseProperty("User")]
    public virtual ICollection<PrintJob> PrintJobs { get; set; } = new List<PrintJob>();

    [InverseProperty("User")]
    public virtual ICollection<Thread> Threads { get; set; } = new List<Thread>();
}
