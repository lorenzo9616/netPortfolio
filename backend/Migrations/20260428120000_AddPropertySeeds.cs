using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OcrApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertySeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ocr_properties",
                columns: new[] { "Id", "CreatedAt", "DataType", "IsActive", "IsRegex", "Name", "SearchHeuristic", "UpdatedAt" },
                values: new object[,]
                {
                    // Billing
                    { 6,  new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "InvoiceNumber", "Invoice\\s*(?:No\\.?|#|Number)[\\s:]*([A-Z0-9\\-]+)",                                    new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7,  new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "date",    true, true,  "InvoiceDate",   "Invoice\\s*Date[\\s:]*(\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4})",                        new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8,  new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "date",    true, true,  "DueDate",       "Due\\s*Date[\\s:]*(\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4})",                            new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9,  new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "AccountNumber", "Account\\s*(?:No\\.?|Number|#)[\\s:]*([A-Z0-9\\-]+)",                                     new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "TaxAmount",     "Tax[\\s:]*\\$?\\s*(\\d{1,3}(?:,\\d{3})*(?:\\.\\d{2})?)",                                  new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // Legal
                    { 11, new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "CaseNumber",    "Case\\s*(?:No\\.?|Number|#)[\\s:]*([A-Z0-9\\-\\/]+)",                                     new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "date",    true, true,  "ContractDate",  "(?:Contract|Agreement|Effective)\\s*Date[\\s:]*(\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4})", new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, false, "LegalParty",    "Party",                                                                                     new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 14, new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, false, "NotaryPublic",  "Notary Public",                                                                             new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "DocumentNumber","Doc(?:ument)?\\s*(?:No\\.?|#|Number)[\\s:]*([A-Z0-9\\-]+)",                                new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // Hospital
                    { 16, new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "PatientId",     "(?:MRN|Patient\\s*ID|Patient\\s*Number)[\\s:]*([A-Z0-9\\-]+)",                             new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "DiagnosisCode", "\\b[A-Z]\\d{2}(?:\\.\\d{1,4})?\\b",                                                       new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 18, new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "date",    true, true,  "AdmissionDate", "Admission\\s*Date[\\s:]*(\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4})",                      new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 19, new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "date",    true, true,  "DischargeDate", "Discharge\\s*Date[\\s:]*(\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4})",                      new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20, new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "PhysicianName", "(?:Attending\\s*)?Physician[\\s:]*([A-Za-z\\s\\.\\,]+)",                                   new DateTime(2026, 4, 28, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyValues: new object[] { 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 });
        }
    }
}
