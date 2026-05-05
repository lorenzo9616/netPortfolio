using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcrApi.Migrations
{
    public partial class AddInvoiceProperties : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ocr_properties",
                columns: new[] { "Id", "CreatedAt", "DataType", "IsActive", "IsRegex", "Name", "SearchHeuristic", "UpdatedAt" },
                columnTypes: new[] { "integer", "timestamp with time zone", "character varying(50)", "boolean", "boolean", "character varying(100)", "text", "timestamp with time zone" },
                values: new object[,]
                {
                    // 21 — DocumentDate: standalone DATE: label (e.g. "DATE: 3/16/2026")
                    { 21, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "date",    true, true,  "DocumentDate",      "DATE[\\s:]+(\\d{1,2}[\\/\\-]\\d{1,2}[\\/\\-]\\d{2,4})",           new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 22 — BillFromCompany: first line below "Bill From:"
                    { 22, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "BillFromCompany",   "Bill\\s*From:\\s+([^\\n\\r]+)",                                    new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 23 — BillToCompany: first line below "Bill To:"
                    { 23, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "BillToCompany",     "Bill\\s*To:\\s+([^\\n\\r]+)",                                      new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 24 — BillToContact: "Attn: Thomas Huber"
                    { 24, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "BillToContact",     "Attn:\\s*([^\\n\\r]+)",                                            new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 25 — ServiceDateRange: "6/18/25 - 11/19/25"
                    { 25, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "ServiceDateRange",  "(\\d{1,2}\\/\\d{1,2}\\/\\d{2,4}\\s*-\\s*\\d{1,2}\\/\\d{1,2}\\/\\d{2,4})", new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 26 — HourlyRate: "$25.00" after "Services"
                    { 26, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "HourlyRate",        "Services\\s+\\$(\\d+(?:\\.\\d{2})?)",                              new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 27 — TotalHours: 3+-digit integer after rate amount (e.g. "355")
                    { 27, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "number",  true, true,  "TotalHours",        "\\$\\d+(?:\\.\\d{2})?\\s+(\\d{3,})",                              new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 28 — InvoiceTotal: "Total: $8,875"
                    { 28, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "InvoiceTotal",      "Total:\\s*\\$?([\\d,]+(?:\\.\\d{2})?)",                            new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 29 — PaymentDate: "3/3/2026 Payment"
                    { 29, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "date",    true, true,  "PaymentDate",       "(\\d{1,2}\\/\\d{1,2}\\/\\d{4})\\s+Payment",                       new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 30 — PaymentAmount: amount after "Payment"
                    { 30, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "PaymentAmount",     "Payment\\s+\\$?([\\d,]+(?:\\.\\d{2})?)",                          new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 31 — OutstandingBalance: "Balance $8,375.00"
                    { 31, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "OutstandingBalance","Balance\\s+\\$?([\\d,]+(?:\\.\\d{2})?)",                          new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 32 — BankName: "Bank: Philippine Savings Bank"
                    { 32, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "BankName",          "Bank:\\s+([^\\n\\r]+)",                                            new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 33 — SwiftCode: "Swift code: PHSBPHM"
                    { 33, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "SwiftCode",         "Swift\\s*(?:code|Code|CODE):\\s*([A-Z0-9]+)",                      new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 34 — BankAccountName: "Account Name: MCLARN Operations Company"
                    { 34, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "BankAccountName",   "Account\\s*Name:\\s*([^\\n\\r]+)",                                 new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 35 — SignatoryName: line immediately above a title keyword
                    { 35, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "SignatoryName",     "([^\\n]+)\\n(?:President|Director|CEO|CFO|COO|Manager|Officer|Secretary|Chairman|VP|Treasurer|Principal|Administrator|Partner)", new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 36 — SignatoryTitle: "President", "Director", etc.
                    { 36, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "SignatoryTitle",    "(President|Director|CEO|CFO|COO|Manager|Officer|Secretary|Chairman|Vice\\s+President|VP|Treasurer|Principal|Administrator)", new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 37 — SignatoryCompany: line immediately below title keyword
                    { 37, new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "SignatoryCompany",  "(?:President|Director|CEO|CFO|COO|Manager|Officer|Secretary|Chairman|VP|Treasurer|Principal|Administrator)\\n([^\\n]+)", new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyColumnType: "integer",
                keyValues: new object[] { 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 });
        }
    }
}
