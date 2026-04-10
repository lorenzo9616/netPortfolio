using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OcrApi.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "analysis_results",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                RawText = table.Column<string>(type: "text", nullable: true),
                AnalyzedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                PageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_analysis_results", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ocr_properties",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                SearchHeuristic = table.Column<string>(type: "text", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ocr_properties", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "saved_fields",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AnalysisResultId = table.Column<int>(type: "integer", nullable: false),
                PropertyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ExtractedValue = table.Column<string>(type: "text", nullable: true),
                ManualOverride = table.Column<string>(type: "text", nullable: true),
                Confidence = table.Column<double>(type: "double precision", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_saved_fields", x => x.Id);
                table.ForeignKey(
                    name: "FK_saved_fields_analysis_results_AnalysisResultId",
                    column: x => x.AnalysisResultId,
                    principalTable: "analysis_results",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.InsertData(
            table: "ocr_properties",
            columns: new[] { "Id", "DataType", "IsActive", "Name", "SearchHeuristic" },
            values: new object[,]
            {
                { 1, "string", true, "Signature", null },
                { 2, "string", true, "FullName", null },
                { 3, "date", true, "DateOfBirth", @"\b\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}\b" },
                { 4, "decimal", true, "BillingTotal", @"\$?\s?\d{1,3}(?:,\d{3})*(?:\.\d{2})?" },
                { 5, "decimal", true, "ProcessingFee", @"\$?\s?\d{1,3}(?:,\d{3})*(?:\.\d{2})?" }
            });

        migrationBuilder.CreateIndex(
            name: "IX_saved_fields_AnalysisResultId",
            table: "saved_fields",
            column: "AnalysisResultId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "saved_fields");
        migrationBuilder.DropTable(name: "ocr_properties");
        migrationBuilder.DropTable(name: "analysis_results");
    }
}
