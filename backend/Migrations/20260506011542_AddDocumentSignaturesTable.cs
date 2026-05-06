using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OcrApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSignaturesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignatureImage",
                table: "analysis_results");

            migrationBuilder.CreateTable(
                name: "document_signatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnalysisResultId = table.Column<int>(type: "integer", nullable: false),
                    ImageData = table.Column<byte[]>(type: "bytea", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_signatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_signatures_analysis_results_AnalysisResultId",
                        column: x => x.AnalysisResultId,
                        principalTable: "analysis_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_signatures_AnalysisResultId",
                table: "document_signatures",
                column: "AnalysisResultId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_signatures");

            migrationBuilder.AddColumn<byte[]>(
                name: "SignatureImage",
                table: "analysis_results",
                type: "bytea",
                nullable: true);
        }
    }
}
