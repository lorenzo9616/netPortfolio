using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OcrApi.Migrations
{
    /// <inheritdoc />
    public partial class AddImageStorageAndTextBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ImageBytes",
                table: "analysis_results",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SignatureImage",
                table: "analysis_results",
                type: "bytea",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "saved_text_blocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnalysisResultId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Confidence = table.Column<float>(type: "real", nullable: false),
                    Page = table.Column<int>(type: "integer", nullable: false),
                    BboxX = table.Column<int>(type: "integer", nullable: false),
                    BboxY = table.Column<int>(type: "integer", nullable: false),
                    BboxWidth = table.Column<int>(type: "integer", nullable: false),
                    BboxHeight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_text_blocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_text_blocks_analysis_results_AnalysisResultId",
                        column: x => x.AnalysisResultId,
                        principalTable: "analysis_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_text_blocks_AnalysisResultId",
                table: "saved_text_blocks",
                column: "AnalysisResultId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_text_blocks");

            migrationBuilder.DropColumn(
                name: "ImageBytes",
                table: "analysis_results");

            migrationBuilder.DropColumn(
                name: "SignatureImage",
                table: "analysis_results");
        }
    }
}
