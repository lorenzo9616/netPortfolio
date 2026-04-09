using Microsoft.EntityFrameworkCore;
using OcrApi.Models;

namespace OcrApi.Data;

public class OcrDbContext : DbContext
{
    public OcrDbContext(DbContextOptions<OcrDbContext> options) : base(options)
    {
    }

    public DbSet<OcrProperty>    OcrProperties    => Set<OcrProperty>();
    public DbSet<AnalysisResult> AnalysisResults  => Set<AnalysisResult>();
    public DbSet<SavedField>     SavedFields       => Set<SavedField>();

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

            // One AnalysisResult has many SavedFields
            entity.HasMany(e => e.Fields)
                  .WithOne(f => f.AnalysisResult)
                  .HasForeignKey(f => f.AnalysisResultId)
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
                SearchHeuristic = @"\b\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}\b",
                IsActive        = true,
                CreatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id              = 4,
                Name            = "BillingTotal",
                DataType        = "decimal",
                SearchHeuristic = @"\$?\s?\d{1,3}(?:,\d{3})*(?:\.\d{2})?",
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
                IsActive        = true,
                CreatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt       = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
