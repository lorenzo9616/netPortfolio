using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcrApi.Migrations
{
    /// <inheritdoc />
    public partial class FixHeuristics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ID 3 — DateOfBirth: add label anchor + support written dates ("March 14, 1985")
            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "Date\\s*of\\s*Birth[\\s:]*(\\w+\\s+\\d{1,2},?\\s+\\d{4}|\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4})",
                    new DateTime(2026, 4, 29, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            // ID 9 — AccountNumber: allow '*' for masked numbers ("****-****-1842")
            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "Account\\s*(?:No\\.?|Number|#)[\\s:]*([*A-Z0-9\\-]+)",
                    new DateTime(2026, 4, 29, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            // ID 15 — DocumentNumber: handle period after "Doc" ("Doc. No. 88")
            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "Doc(?:ument)?\\.?\\s*(?:No\\.?|#|Number)[\\s:]*([A-Z0-9]+)",
                    new DateTime(2026, 4, 29, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            // ID 17 — DiagnosisCode: switch to keyword search for text diagnosis
            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "IsRegex", "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    false,
                    "Diagnosis",
                    new DateTime(2026, 4, 29, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            // ID 18 — AdmissionDate: match "Date Admitted:" + written date format
            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "(?:Admission\\s*Date|Date\\s*Admitt?ed)[\\s:]*(\\w+\\s+\\d{1,2},?\\s+\\d{4}|\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4})",
                    new DateTime(2026, 4, 29, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            // ID 19 — DischargeDate: match "Date Discharged:" + written date format
            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "(?:Discharge[d]?\\s*Date|Date\\s*Discharge[d]?)[\\s:]*(\\w+\\s+\\d{1,2},?\\s+\\d{4}|\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4})",
                    new DateTime(2026, 4, 29, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            // ID 20 — PhysicianName: accept "Admitting" prefix + stop at newline
            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "(?:(?:Attending|Admitting)\\s*)?Physician[\\s:]*([^\\n]+)",
                    new DateTime(2026, 4, 29, 0, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "\\b\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4}\\b",
                    new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "Account\\s*(?:No\\.?|Number|#)[\\s:]*([A-Z0-9\\-]+)",
                    new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "Doc(?:ument)?\\s*(?:No\\.?|#|Number)[\\s:]*([A-Z0-9\\-]+)",
                    new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "IsRegex", "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    true,
                    "\\b[A-Z]\\d{2}(?:\\.\\d{1,4})?\\b",
                    new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "Admission\\s*Date[\\s:]*(\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4})",
                    new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "Discharge\\s*Date[\\s:]*(\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4})",
                    new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.UpdateData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "SearchHeuristic", "UpdatedAt" },
                values: new object[]
                {
                    "(?:Attending\\s*)?Physician[\\s:]*([A-Za-z\\s\\.\\,]+)",
                    new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc)
                });
        }
    }
}
