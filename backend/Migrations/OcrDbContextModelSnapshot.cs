using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using OcrApi.Data;

#nullable disable

namespace OcrApi.Migrations;

[DbContext(typeof(OcrDbContext))]
partial class OcrDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.5")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("OcrApi.Models.AnalysisResult", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer");

            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

            b.Property<DateTime>("AnalyzedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()");

            b.Property<string>("FileName")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("character varying(255)");

            b.Property<int>("PageCount")
                .HasColumnType("integer");

            b.Property<string>("RawText")
                .HasColumnType("text");

            b.HasKey("Id");

            b.ToTable("analysis_results");
        });

        modelBuilder.Entity("OcrApi.Models.OcrProperty", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer");

            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()");

            b.Property<string>("DataType")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)");

            b.Property<bool>("IsActive")
                .ValueGeneratedOnAdd()
                .HasColumnType("boolean")
                .HasDefaultValue(true);

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<string>("SearchHeuristic")
                .HasColumnType("text");

            b.Property<DateTime>("UpdatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()");

            b.HasKey("Id");

            b.ToTable("ocr_properties");

            b.HasData(
                new { Id = 1, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), DataType = "string", IsActive = true, Name = "Signature", UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                new { Id = 2, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), DataType = "string", IsActive = true, Name = "FullName", UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                new { Id = 3, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), DataType = "date", IsActive = true, Name = "DateOfBirth", SearchHeuristic = "\\b\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4}\\b", UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                new { Id = 4, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), DataType = "decimal", IsActive = true, Name = "BillingTotal", SearchHeuristic = "\\$?\\s?\\d{1,3}(?:,\\d{3})*(?:\\.\\d{2})?", UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                new { Id = 5, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), DataType = "decimal", IsActive = true, Name = "ProcessingFee", SearchHeuristic = "\\$?\\s?\\d{1,3}(?:,\\d{3})*(?:\\.\\d{2})?", UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        });

        modelBuilder.Entity("OcrApi.Models.SavedField", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer");

            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

            b.Property<int>("AnalysisResultId")
                .HasColumnType("integer");

            b.Property<double>("Confidence")
                .HasColumnType("double precision");

            b.Property<string>("ExtractedValue")
                .HasColumnType("text");

            b.Property<string>("ManualOverride")
                .HasColumnType("text");

            b.Property<string>("PropertyName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.HasKey("Id");

            b.HasIndex("AnalysisResultId");

            b.ToTable("saved_fields");
        });

        modelBuilder.Entity("OcrApi.Models.SavedField", b =>
        {
            b.HasOne("OcrApi.Models.AnalysisResult", "AnalysisResult")
                .WithMany("Fields")
                .HasForeignKey("AnalysisResultId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("AnalysisResult");
        });

        modelBuilder.Entity("OcrApi.Models.AnalysisResult", b =>
        {
            b.Navigation("Fields");
        });
#pragma warning restore 612, 618
    }
}
