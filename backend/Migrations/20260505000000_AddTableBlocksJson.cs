using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcrApi.Migrations
{
    public partial class AddTableBlocksJson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TableBlocksJson",
                table: "analysis_results",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TableBlocksJson",
                table: "analysis_results");
        }
    }
}
