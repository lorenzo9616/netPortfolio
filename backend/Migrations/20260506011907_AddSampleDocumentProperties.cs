using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcrApi.Migrations
{
    public partial class AddSampleDocumentProperties : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ocr_properties",
                columns: new[] { "Id", "CreatedAt", "DataType", "IsActive", "IsRegex", "Name", "SearchHeuristic", "UpdatedAt" },
                columnTypes: new[] { "integer", "timestamp with time zone", "character varying(50)", "boolean", "boolean", "character varying(100)", "text", "timestamp with time zone" },
                values: new object[,]
                {
                    // ── Hospital Bill ─────────────────────────────────────────────────────────
                    // 38 — PhilHealthNumber: "PhilHealth No.: 11-0000034821-1"
                    { 38, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "PhilHealthNumber",             @"PhilHealth\s*No\.?\s*[:\s]+([\d\-]+)",                                                          new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 39 — GrossTotal: "Gross Total: PHP 68,030.00"
                    { 39, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "GrossTotal",                   @"Gross\s*Total[:\s]+(?:PHP\s+)?([\d,]+(?:\.\d{2})?)",                                           new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 40 — PhilHealthDeduction: "PhilHealth Deduction: (PHP 15,000.00)"
                    { 40, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "PhilHealthDeduction",          @"PhilHealth\s*Deduction[:\s]+\(?(?:PHP\s+)?([\d,]+(?:\.\d{2})?)\)?",                           new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 41 — HMOCoverage: "HMO Coverage (Medicard): (PHP 20,000.00)"
                    { 41, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "HMOCoverage",                  @"HMO\s*Coverage[^:]*[:\s]+\(?(?:PHP\s+)?([\d,]+(?:\.\d{2})?)\)?",                             new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 42 — BalanceDue: "BALANCE DUE: PHP 33,030.00"
                    { 42, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "BalanceDue",                   @"BALANCE\s*DUE[:\s]+(?:PHP\s+)?([\d,]+(?:\.\d{2})?)",                                          new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 43 — ModeOfPayment: "Mode of Payment: Split: Cash + Credit Card..."
                    { 43, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "ModeOfPayment",                @"Mode\s*of\s*Payment[:\s]+([^\n\r]+)",                                                          new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 44 — ORNumber: "OR Number: MGH-OR-2025-041892"
                    { 44, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "ORNumber",                     @"OR\s*(?:No\.|Number|#)[:\s]+([A-Z0-9\-]+)",                                                    new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 45 — Cashier: "Cashier: De Leon, Ana R."
                    { 45, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "Cashier",                      @"Cashier[:\s]+([^\n\r]+)",                                                                      new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },

                    // ── Legal / Restaurant ────────────────────────────────────────────────────
                    // 46 — BusinessName: "Business Name: Casa Filipina Restaurant & Events Hall"
                    { 46, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "BusinessName",                 @"Business\s*Name[:\s]+([^\n\r]+)",                                                              new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 47 — BusinessAddress: "Business Address: G/F Ermita Place Bldg., ..."
                    { 47, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "BusinessAddress",              @"Business\s*Address[:\s]+([^\n\r]+)",                                                           new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 48 — TIN: "TIN: 225-816-701-000"
                    { 48, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "TIN",                          @"(?:^|\s)TIN[:\s]+(\d{3}-\d{3}-\d{3}-\d{3})",                                                 new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 49 — ProprietorName: "Proprietor / Owner: Anastacia R. Fontanilla"
                    { 49, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "ProprietorName",               @"Proprietor\s*(?:/\s*(?:Owner|Authorized\s*Signatory))?[:\s]+([^\n\r]+)",                      new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 50 — ContactNumber: "Contact Number: +63-917-555-8841"
                    { 50, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "ContactNumber",                @"Contact\s*(?:Number|No\.?)[:\s]+([\+\d\-\(\)\s]+)",                                           new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 51 — EmailAddress: "Email Address: admin@casafilipina.com.ph"
                    { 51, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "EmailAddress",                 @"Email\s*(?:Address)?[:\s]+([\w.\-]+@[\w.\-]+(?:\.[\w]+)+)",                                   new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 52 — TotalGrossSales: last amount on "TOTAL FY YYYY" row
                    { 52, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "TotalGrossSales",              @"TOTAL\s+FY\s+\d{4}[^\n]*?([\d]{1,3}(?:,\d{3})+\.\d{2})",                                     new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },

                    // ── Meeting Minutes ───────────────────────────────────────────────────────
                    // 53 — CompanyName: "NEXBRIDGE HOLDINGS CORPORATION" in header
                    { 53, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "CompanyName",                  @"([A-Z][A-Z\s&,\.]+(?:CORPORATION|HOLDINGS|INC\b|LLC|LTD|COMPANY|CORP\b))",                   new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 54 — MeetingDate: "Date: April 14, 2025"
                    { 54, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "date",    true, true,  "MeetingDate",                  @"Date[:\s]+([A-Za-z]+\s+\d{1,2},\s+\d{4})",                                                   new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 55 — MeetingType: "Meeting Type: Special Board Meeting -- Extraordinary Session"
                    { 55, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "MeetingType",                  @"Meeting\s*Type[:\s]+([^\n\r]+)",                                                               new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 56 — Venue: "Venue: Boardroom A, 38/F Zuellig Building, Makati City"
                    { 56, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "Venue",                        @"Venue[:\s]+([^\n\r]+)",                                                                        new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 57 — ReferenceNumber: "Reference No.: NBH-SBM-2025-003"
                    { 57, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "ReferenceNumber",              @"Reference\s*(?:No\.|Number)[:\s]+([A-Z0-9\-]+)",                                              new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },

                    // ── Payslip ───────────────────────────────────────────────────────────────
                    // 58 — EmployeeName: "Employee Name: Reyes, Marco L."
                    { 58, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "EmployeeName",                 @"Employee\s*Name[:\s]+([^\n\r]+)",                                                              new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 59 — EmployeeId: "Employee ID: NBH-EMP-00418"
                    { 59, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "EmployeeId",                   @"Employee\s*ID[:\s]+([A-Z0-9\-]+)",                                                            new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 60 — Department: "Department: Information Technology"
                    { 60, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "Department",                   @"Department[:\s]+([^\n\r]+)",                                                                   new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 61 — Position: "Position / Grade: Senior Systems Engineer / Grade 7"
                    { 61, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "Position",                     @"Position\s*(?:/\s*Grade)?[:\s]+([^\n\r]+)",                                                   new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 62 — EmploymentType: "Employment Type: Regular"
                    { 62, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "EmploymentType",               @"Employment\s*Type[:\s]+([^\n\r]+)",                                                            new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 63 — PayPeriod: "Pay Period: March 1 -- 31, 2025"
                    { 63, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "PayPeriod",                    @"Pay\s*Period[:\s]+([^\n\r]+)",                                                                 new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 64 — SSSNumber: "SSS No.: 04-1234567-8"
                    { 64, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "SSSNumber",                    @"SSS\s*(?:No\.|Number)[:\s]+([\d\-]+)",                                                        new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 65 — PagIBIGNumber: "Pag-IBIG No.: 1234-5678-9012"
                    { 65, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "PagIBIGNumber",                @"Pag-IBIG\s*(?:No\.|Number)[:\s]+([\d\-]+)",                                                   new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 66 — NetPay: "NET PAY: PHP 48,744.38"
                    { 66, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "NetPay",                       @"NET\s*PAY[:\s]+(?:PHP\s+)?([\d,]+(?:\.\d{2})?)",                                             new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 67 — GrossEarnings: "GROSS EARNINGS 57,125.63"
                    { 67, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "GrossEarnings",                @"GROSS\s*EARNINGS[:\s]*([\d,]+(?:\.\d{2})?)",                                                  new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 68 — TotalDeductions: "TOTAL DEDUCTIONS 8,381.25"
                    { 68, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "TotalDeductions",              @"TOTAL\s*DEDUCTIONS[:\s]*([\d,]+(?:\.\d{2})?)",                                                new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },

                    // ── Bank Receipt ──────────────────────────────────────────────────────────
                    // 69 — TransactionReferenceNumber: "Transaction Ref. No.: BD202503280041827364"
                    { 69, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "TransactionReferenceNumber",   @"Transaction\s*Ref\.?\s*No\.?[:\s]+([A-Z0-9]+)",                                               new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 70 — TransactionType: "Transaction Type: PESONet Interbank Fund Transfer"
                    { 70, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "string",  true, true,  "TransactionType",              @"Transaction\s*Type[:\s]+([^\n\r]+)",                                                           new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 71 — TransactionDate: "Transaction Date: 03/28/2025 14:22:45"
                    { 71, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "date",    true, true,  "TransactionDate",              @"Transaction\s*Date[:\s]+(\d{2}/\d{2}/\d{4}(?:\s+\d{2}:\d{2}:\d{2})?)",                       new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 72 — TransferAmount: "Transfer Amount 500,000.00"
                    { 72, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "TransferAmount",               @"Transfer\s*Amount[:\s]*([\d,]+(?:\.\d{2})?)",                                                 new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    // 73 — AvailableBalance: "Available Balance After Transfer: PHP 2,184,337.92"
                    { 73, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "decimal", true, true,  "AvailableBalance",             @"Available\s*Balance[^:]*[:\s]+(?:PHP\s+)?([\d,]+(?:\.\d{2})?)",                              new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ocr_properties",
                keyColumn: "Id",
                keyColumnType: "integer",
                keyValues: new object[] { 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73 });
        }
    }
}
