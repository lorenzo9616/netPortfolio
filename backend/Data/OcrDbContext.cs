using Microsoft.EntityFrameworkCore;
using OcrApi.Models;

namespace OcrApi.Data;

public class OcrDbContext : DbContext
{
    public OcrDbContext(DbContextOptions<OcrDbContext> options) : base(options)
    {
    }

    public DbSet<OcrProperty>    OcrProperties   => Set<OcrProperty>();
    public DbSet<AnalysisResult> AnalysisResults => Set<AnalysisResult>();
    public DbSet<SavedField>     SavedFields      => Set<SavedField>();
    public DbSet<SavedTextBlock> SavedTextBlocks  => Set<SavedTextBlock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OcrProperty>(entity =>
        {
            entity.ToTable("ocr_properties");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.DataType)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(e => e.SearchHeuristic)
                  .IsRequired(false);

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("NOW()");
        });

        // ── AnalysisResult ────────────────────────────────────────────────────
        modelBuilder.Entity<AnalysisResult>(entity =>
        {
            entity.ToTable("analysis_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.RawText).IsRequired(false);
            entity.Property(e => e.AnalyzedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.ImageBytes).IsRequired(false);
            entity.Property(e => e.SignatureImage).IsRequired(false);

            entity.HasMany(e => e.Fields)
                  .WithOne(f => f.AnalysisResult)
                  .HasForeignKey(f => f.AnalysisResultId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.TextBlocks)
                  .WithOne(b => b.AnalysisResult)
                  .HasForeignKey(b => b.AnalysisResultId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── SavedField ────────────────────────────────────────────────────────
        modelBuilder.Entity<SavedField>(entity =>
        {
            entity.ToTable("saved_fields");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PropertyName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ExtractedValue).IsRequired(false);
            entity.Property(e => e.ManualOverride).IsRequired(false);
        });

        // ── SavedTextBlock ────────────────────────────────────────────────────
        modelBuilder.Entity<SavedTextBlock>(entity =>
        {
            entity.ToTable("saved_text_blocks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired().HasMaxLength(500);
        });

        // ── Seed data ──────────────────────────────────────────────────────────
        modelBuilder.Entity<OcrProperty>().HasData(
            new OcrProperty
            {
                Id              = 1,
                Name            = "Signature",
                DataType        = "string",
                SearchHeuristic = null,
                IsActive        = true,
                CreatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 2,
                Name            = "FullName",
                DataType        = "string",
                SearchHeuristic = null,
                IsActive        = true,
                CreatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 3,
                Name            = "DateOfBirth",
                DataType        = "date",
                SearchHeuristic = @"Date\s*of\s*Birth[\s:]*(\w+\s+\d{1,2},?\s+\d{4}|\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 4,
                Name            = "BillingTotal",
                DataType        = "decimal",
                SearchHeuristic = @"\$?\s?\d{1,3}(?:,\d{3})*(?:\.\d{2})?",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 5,
                Name            = "ProcessingFee",
                DataType        = "decimal",
                SearchHeuristic = @"\$?\s?\d{1,3}(?:,\d{3})*(?:\.\d{2})?",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },

            // ── Billing ───────────────────────────────────────────────────────────
            new OcrProperty
            {
                Id              = 6,
                Name            = "InvoiceNumber",
                DataType        = "string",
                SearchHeuristic = @"Invoice\s*(?:No\.?|#|Number)[\s:]*([A-Z0-9\-]+)",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 7,
                Name            = "InvoiceDate",
                DataType        = "date",
                SearchHeuristic = @"Invoice\s*Date[\s:]*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 8,
                Name            = "DueDate",
                DataType        = "date",
                SearchHeuristic = @"Due\s*Date[\s:]*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 9,
                Name            = "AccountNumber",
                DataType        = "string",
                SearchHeuristic = @"Account\s*(?:No\.?|Number|#)[\s:]*([*A-Z0-9\-]+)",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 10,
                Name            = "TaxAmount",
                DataType        = "decimal",
                SearchHeuristic = @"Tax[\s:]*\$?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc)
            },

            // ── Legal ─────────────────────────────────────────────────────────────
            new OcrProperty
            {
                Id              = 11,
                Name            = "CaseNumber",
                DataType        = "string",
                SearchHeuristic = @"Case\s*(?:No\.?|Number|#)[\s:]*([A-Z0-9\-\/]+)",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 12,
                Name            = "ContractDate",
                DataType        = "date",
                SearchHeuristic = @"(?:Contract|Agreement|Effective)\s*Date[\s:]*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 13,
                Name            = "LegalParty",
                DataType        = "string",
                SearchHeuristic = "Party",
                IsRegex         = false,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 14,
                Name            = "NotaryPublic",
                DataType        = "string",
                SearchHeuristic = "Notary Public",
                IsRegex         = false,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 15,
                Name            = "DocumentNumber",
                DataType        = "string",
                SearchHeuristic = @"Doc(?:ument)?\.?\s*(?:No\.?|#|Number)[\s:]*([A-Z0-9]+)",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc)
            },

            // ── Hospital ──────────────────────────────────────────────────────────
            new OcrProperty
            {
                Id              = 16,
                Name            = "PatientId",
                DataType        = "string",
                SearchHeuristic = @"(?:MRN|Patient\s*ID|Patient\s*Number)[\s:]*([A-Z0-9\-]+)",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 17,
                Name            = "DiagnosisCode",
                DataType        = "string",
                SearchHeuristic = "Diagnosis",
                IsRegex         = false,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 18,
                Name            = "AdmissionDate",
                DataType        = "date",
                SearchHeuristic = @"(?:Admission\s*Date|Date\s*Admitt?ed)[\s:]*(\w+\s+\d{1,2},?\s+\d{4}|\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 19,
                Name            = "DischargeDate",
                DataType        = "date",
                SearchHeuristic = @"(?:Discharge[d]?\s*Date|Date\s*Discharge[d]?)[\s:]*(\w+\s+\d{1,2},?\s+\d{4}|\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 20,
                Name            = "PhysicianName",
                DataType        = "string",
                SearchHeuristic = @"(?:(?:Attending|Admitting)\s*)?Physician[\s:]*([^\n]+)",
                IsRegex         = true,
                IsActive        = true,
                CreatedAt       = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
