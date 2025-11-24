using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

public partial class OpenFarmContext : DbContext
{
    public OpenFarmContext(DbContextOptions<OpenFarmContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Color> Colors { get; set; }



    public virtual DbSet<Email> Emails { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Maintenance> Maintenances { get; set; }
    public virtual DbSet<Emailautoreplyrule> Emailautoreplyrules { get; set; }

    public virtual DbSet<Material> Materials { get; set; }

    public virtual DbSet<MaterialPricePeriod> MaterialPricePeriods { get; set; }

    public virtual DbSet<MaterialType> MaterialTypes { get; set; }

    public virtual DbSet<Print> Prints { get; set; }

    public virtual DbSet<PrintJob> PrintJobs { get; set; }

    public virtual DbSet<Printer> Printers { get; set; }

    public virtual DbSet<PrinterModel> PrinterModels { get; set; }

    public virtual DbSet<PrinterModelPricePeriod> PrinterModelPricePeriods { get; set; }

    public virtual DbSet<PrintersLoadedMaterial> PrintersLoadedMaterials { get; set; }

    public virtual DbSet<Thumbnail> Thumbnails { get; set; }

    public virtual DbSet<Thread> Threads { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<AiGeneratedResponse> AiGeneratedResponses { get; set; }

    public virtual DbSet<RagDocument> RagDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<AiGeneratedResponse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_generated_responses_pkey");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasDefaultValueSql("'Pending'::character varying");

            entity.HasOne(d => d.Thread).WithMany(p => p.AiGeneratedResponses)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ai_generated_responses_thread_id_fkey");

            entity.HasOne(d => d.Message).WithMany(p => p.AiGeneratedResponses)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ai_generated_responses_message_id_fkey");
        });

        modelBuilder.Entity<RagDocument>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("rag_documents_pkey");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Color>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("colors_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
        });



        modelBuilder.Entity<Email>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.EmailAddress }).HasName("emails_pkey");

            entity.HasOne(d => d.User).WithMany(p => p.Emails).HasConstraintName("emails_user_id_fkey");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messages_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.MessageType).HasDefaultValueSql("'email'::character varying");
            entity.Property(e => e.SenderType).HasDefaultValueSql("'user'::character varying");
            entity.Property(e => e.MessageStatus).HasDefaultValueSql("'unseen'::character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Thread).WithMany(p => p.Messages)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("messages_thread_id_fkey");
        });

        modelBuilder.Entity<Maintenance>(entity =>
        {
            entity.HasKey(e => e.MaintenanceReportId).HasName("maintenance_pkey");

            entity.Property(e => e.MaintenanceReportId).ValueGeneratedNever();
            entity.Property(e => e.SessionErrorCount).HasDefaultValue(0);
            entity.Property(e => e.SessionPrintsCompleted).HasDefaultValue(0);
            entity.Property(e => e.SessionPrintsFailed).HasDefaultValue(0);
            entity.Property(e => e.SessionUptime).HasDefaultValue(0);

            entity.HasOne(d => d.MaintenanceReport).WithOne(p => p.Maintenance).HasConstraintName("maintenance_maintenance_report_id_fkey");
        });


        modelBuilder.Entity<Emailautoreplyrule>(entity =>
        {
            entity.HasKey(e => e.Emailautoreplyruleid).HasName("emailautoreplyrules_pkey");

            entity.Property(e => e.Isenabled);
            entity.Property(e => e.Priority);
        });

        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("materials_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();

            entity.HasOne(d => d.MaterialColor).WithMany(p => p.Materials).HasConstraintName("materials_material_color_id_fkey");

            entity.HasOne(d => d.MaterialType).WithMany(p => p.Materials).HasConstraintName("materials_material_type_id_fkey");
        });

        modelBuilder.Entity<MaterialPricePeriod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("material_price_periods_pkey");

            entity.HasIndex(e => e.MaterialId, "uq_material_one_active_price")
                .IsUnique()
                .HasFilter("(ended_at IS NULL)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Material).WithOne(p => p.MaterialPricePeriod).HasConstraintName("material_price_periods_material_id_fkey");
        });

        modelBuilder.Entity<MaterialType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("material_types_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
        });

        modelBuilder.Entity<Print>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("prints_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.PrintStatus).HasDefaultValueSql("'pending'::character varying");

            entity.HasOne(d => d.PrintJob).WithMany(p => p.Prints)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("prints_print_job_id_fkey");

            entity.HasOne(d => d.Printer).WithMany(p => p.Prints)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("prints_printer_id_fkey");
        });

        modelBuilder.Entity<PrintJob>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("print_jobs_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.JobStatus).HasDefaultValueSql("'received'::character varying");
            entity.Property(e => e.NumCopies).HasDefaultValue(1);
            entity.Property(e => e.Paid).HasDefaultValue(false);

            entity.HasOne(d => d.Material).WithMany(p => p.PrintJobs)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("print_jobs_material_id_fkey");

            entity.HasOne(d => d.PrinterModel).WithMany(p => p.PrintJobs)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("print_jobs_printer_model_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.PrintJobs)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("print_jobs_user_id_fkey");
        });

        modelBuilder.Entity<Printer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("printers_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CurrentlyPrinting).HasDefaultValue(false);

            entity.HasOne(d => d.PrinterModel).WithMany(p => p.Printers).HasConstraintName("printers_printer_model_id_fkey");
        });

        modelBuilder.Entity<PrinterModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("printer_models_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
        });

        modelBuilder.Entity<PrinterModelPricePeriod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("printer_model_price_periods_pkey");

            entity.HasIndex(e => e.PrinterModelId, "uq_printer_one_active_price")
                .IsUnique()
                .HasFilter("(ended_at IS NULL)");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.PrinterModel).WithOne(p => p.PrinterModelPricePeriod)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("printer_model_price_periods_printer_model_id_fkey");
        });

        modelBuilder.Entity<PrintersLoadedMaterial>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("printers_loaded_materials_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();

            entity.HasOne(d => d.Material).WithMany(p => p.PrintersLoadedMaterials).HasConstraintName("printers_loaded_materials_material_id_fkey");

            entity.HasOne(d => d.Printer).WithMany(p => p.PrintersLoadedMaterials).HasConstraintName("printers_loaded_materials_printer_id_fkey");
        });

        modelBuilder.Entity<Thumbnail>(entity =>
        {
            entity.HasKey(e => e.PrintJobId).HasName("thumbnails_pkey");

            entity.Property(e => e.PrintJobId).ValueGeneratedNever();

            entity.HasOne(d => d.PrintJob).WithOne(p => p.Thumbnail).HasConstraintName("thumbnails_print_job_id_fkey");
        });

        modelBuilder.Entity<Thread>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("threads_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.ThreadStatus).HasDefaultValueSql("'active'::character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Job).WithMany(p => p.Threads)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("threads_job_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Threads)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("threads_user_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Suspended).HasDefaultValue(false);
            entity.Property(e => e.Verified).HasDefaultValue(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
