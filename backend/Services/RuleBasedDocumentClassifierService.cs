using OcrApi.Services;

namespace OcrApi.Services;

public sealed class RuleBasedDocumentClassifierService : IDocumentClassifierService
{
    public Task<ClassificationResult> ClassifyAsync(string rawText, CancellationToken ct = default)
        => Task.FromResult(Classify(rawText.ToLowerInvariant()));

    private static ClassificationResult Classify(string text)
    {
        var scores = new Dictionary<string, int>
        {
            ["Invoice"]         = Score(text, ["invoice", "bill to", "due date", "invoice number", "inv #", "payment terms", "subtotal", "amount due", "remit to"]),
            ["Receipt"]         = Score(text, ["receipt", "amount paid", "cashier", "thank you for your purchase", "change due", "transaction id", "store #"]),
            ["Purchase Order"]  = Score(text, ["purchase order", "p.o. number", "ship to", "vendor", "unit price", "qty", "ordered by"]),
            ["Contract"]        = Score(text, ["agreement", "contract", "party", "whereas", "hereinafter", "obligations", "breach", "indemnif", "governing law", "in witness whereof"]),
            ["Resume"]          = Score(text, ["resume", "curriculum vitae", "objective", "work experience", "employment history", "references", "skills", "education", "certifications"]),
            ["Bank Statement"]  = Score(text, ["statement", "account number", "opening balance", "closing balance", "deposit", "withdrawal", "available balance"]),
            ["Tax Form"]        = Score(text, ["w-2", "1099", "tax return", "gross income", "withholding", "adjusted gross", "irs", "taxable income", "filing status"]),
            ["Medical Record"]  = Score(text, ["patient", "diagnosis", "prescription", "physician", "clinic", "dosage", "medication", "date of birth", "chief complaint"]),
            ["Meeting Minutes"] = Score(text, ["minutes", "attendees", "agenda", "motion", "seconded", "adjourned", "action items", "quorum", "in attendance"]),
            ["Legal Filing"]    = Score(text, ["court", "plaintiff", "defendant", "affidavit", "subpoena", "deposition", "docket", "jurisdiction", "hereby orders"]),
        };

        var best = scores.MaxBy(kv => kv.Value);
        if (best.Value < 2)
            return new ClassificationResult();

        return new ClassificationResult
        {
            DocumentType = best.Key,
            SuggestedProperties = GetProperties(best.Key),
        };
    }

    private static int Score(string text, string[] keywords)
        => keywords.Count(text.Contains);

    private static List<SuggestedProperty> GetProperties(string docType) => docType switch
    {
        "Invoice" =>
        [
            new() { Name = "Invoice Number",  DataType = "text",     SearchHeuristic = @"inv(?:oice)?[\s#:.]*([A-Z0-9\-]+)",             IsRegex = true  },
            new() { Name = "Invoice Date",    DataType = "date",     SearchHeuristic = @"(?:invoice\s+date|date)[:\s]+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})", IsRegex = true  },
            new() { Name = "Due Date",        DataType = "date",     SearchHeuristic = @"due\s+(?:date)?[:\s]+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",         IsRegex = true  },
            new() { Name = "Vendor Name",     DataType = "text",     SearchHeuristic = "from",                                           IsRegex = false },
            new() { Name = "Customer Name",   DataType = "text",     SearchHeuristic = "bill to",                                        IsRegex = false },
            new() { Name = "Subtotal",        DataType = "currency", SearchHeuristic = @"subtotal[\s:$]*(\d[\d,]*\.?\d{0,2})",            IsRegex = true  },
            new() { Name = "Tax Amount",      DataType = "currency", SearchHeuristic = @"(?:tax|vat)[\s:$]*(\d[\d,]*\.?\d{0,2})",        IsRegex = true  },
            new() { Name = "Total Amount",    DataType = "currency", SearchHeuristic = @"(?:total|amount\s+due)[\s:$]*(\d[\d,]*\.?\d{0,2})", IsRegex = true  },
        ],
        "Receipt" =>
        [
            new() { Name = "Store Name",       DataType = "text",     SearchHeuristic = "store",                                          IsRegex = false },
            new() { Name = "Date",             DataType = "date",     SearchHeuristic = @"(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",          IsRegex = true  },
            new() { Name = "Total Amount",     DataType = "currency", SearchHeuristic = @"total[\s:$]*(\d[\d,]*\.?\d{0,2})",              IsRegex = true  },
            new() { Name = "Payment Method",   DataType = "text",     SearchHeuristic = @"(?:paid\s+by|payment\s+method)[:\s]+(\w+)",     IsRegex = true  },
            new() { Name = "Transaction ID",   DataType = "text",     SearchHeuristic = @"(?:transaction|trans)[\s#:]*([A-Z0-9\-]+)",     IsRegex = true  },
        ],
        "Purchase Order" =>
        [
            new() { Name = "PO Number",        DataType = "text",     SearchHeuristic = @"p\.?o\.?\s*(?:number|#)?[\s:]*([A-Z0-9\-]+)",  IsRegex = true  },
            new() { Name = "Order Date",       DataType = "date",     SearchHeuristic = @"(?:order\s+)?date[:\s]+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})", IsRegex = true  },
            new() { Name = "Vendor Name",      DataType = "text",     SearchHeuristic = "vendor",                                         IsRegex = false },
            new() { Name = "Ship To",          DataType = "text",     SearchHeuristic = "ship to",                                        IsRegex = false },
            new() { Name = "Total Amount",     DataType = "currency", SearchHeuristic = @"total[\s:$]*(\d[\d,]*\.?\d{0,2})",              IsRegex = true  },
        ],
        "Contract" =>
        [
            new() { Name = "Effective Date",   DataType = "date",     SearchHeuristic = @"effective[\s]+(?:date)?[:\s]*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}|\w+ \d{1,2},?\s*\d{4})", IsRegex = true  },
            new() { Name = "Expiration Date",  DataType = "date",     SearchHeuristic = @"(?:expir|terminat\w+)[\s\w]*date[:\s]*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})", IsRegex = true  },
            new() { Name = "Party A",          DataType = "text",     SearchHeuristic = "party a",                                        IsRegex = false },
            new() { Name = "Party B",          DataType = "text",     SearchHeuristic = "party b",                                        IsRegex = false },
            new() { Name = "Governing Law",    DataType = "text",     SearchHeuristic = @"governed\s+by\s+the\s+laws\s+of\s+([\w\s]+)",   IsRegex = true  },
        ],
        "Resume" =>
        [
            new() { Name = "Candidate Name",   DataType = "text",     SearchHeuristic = "name",                                           IsRegex = false },
            new() { Name = "Email",            DataType = "text",     SearchHeuristic = @"([a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,})", IsRegex = true  },
            new() { Name = "Phone Number",     DataType = "text",     SearchHeuristic = @"(\(?\d{3}\)?[\s.\-]\d{3}[\s.\-]\d{4})",         IsRegex = true  },
            new() { Name = "LinkedIn",         DataType = "text",     SearchHeuristic = @"linkedin\.com/in/([\w\-]+)",                    IsRegex = true  },
            new() { Name = "Current Title",    DataType = "text",     SearchHeuristic = "title",                                          IsRegex = false },
        ],
        "Bank Statement" =>
        [
            new() { Name = "Account Number",   DataType = "text",     SearchHeuristic = @"account[\s#:]*(\d{4,})",                        IsRegex = true  },
            new() { Name = "Statement Period", DataType = "text",     SearchHeuristic = "statement period",                               IsRegex = false },
            new() { Name = "Opening Balance",  DataType = "currency", SearchHeuristic = @"opening\s+balance[\s:$]*(\d[\d,]*\.?\d{0,2})",  IsRegex = true  },
            new() { Name = "Closing Balance",  DataType = "currency", SearchHeuristic = @"closing\s+balance[\s:$]*(\d[\d,]*\.?\d{0,2})",  IsRegex = true  },
        ],
        "Tax Form" =>
        [
            new() { Name = "Tax Year",         DataType = "text",     SearchHeuristic = @"tax\s+year[\s:]*(\d{4})",                       IsRegex = true  },
            new() { Name = "Taxpayer Name",    DataType = "text",     SearchHeuristic = "taxpayer",                                       IsRegex = false },
            new() { Name = "SSN",              DataType = "text",     SearchHeuristic = @"(\d{3}-\d{2}-\d{4})",                           IsRegex = true  },
            new() { Name = "Gross Income",     DataType = "currency", SearchHeuristic = @"gross\s+income[\s:$]*(\d[\d,]*\.?\d{0,2})",     IsRegex = true  },
            new() { Name = "Total Tax",        DataType = "currency", SearchHeuristic = @"total\s+tax[\s:$]*(\d[\d,]*\.?\d{0,2})",        IsRegex = true  },
        ],
        "Medical Record" =>
        [
            new() { Name = "Patient Name",     DataType = "text",     SearchHeuristic = "patient name",                                   IsRegex = false },
            new() { Name = "Date of Birth",    DataType = "date",     SearchHeuristic = @"(?:dob|date of birth)[:\s]+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})", IsRegex = true  },
            new() { Name = "Physician",        DataType = "text",     SearchHeuristic = @"(?:physician|doctor|dr\.)[:\s]+([\w\s]+)",      IsRegex = true  },
            new() { Name = "Diagnosis",        DataType = "text",     SearchHeuristic = "diagnosis",                                      IsRegex = false },
            new() { Name = "Medication",       DataType = "text",     SearchHeuristic = "medication",                                     IsRegex = false },
        ],
        "Meeting Minutes" =>
        [
            new() { Name = "Meeting Date",     DataType = "date",     SearchHeuristic = @"(?:meeting\s+)?date[:\s]+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}|\w+ \d{1,2},?\s*\d{4})", IsRegex = true  },
            new() { Name = "Location",         DataType = "text",     SearchHeuristic = "location",                                       IsRegex = false },
            new() { Name = "Attendees",        DataType = "text",     SearchHeuristic = "attendees",                                      IsRegex = false },
            new() { Name = "Facilitator",      DataType = "text",     SearchHeuristic = "facilitator",                                    IsRegex = false },
        ],
        "Legal Filing" =>
        [
            new() { Name = "Case Number",      DataType = "text",     SearchHeuristic = @"(?:case|docket)\s*(?:no\.?|number)?[\s:]*([A-Z0-9\-:]+)", IsRegex = true  },
            new() { Name = "Court",            DataType = "text",     SearchHeuristic = "court",                                          IsRegex = false },
            new() { Name = "Plaintiff",        DataType = "text",     SearchHeuristic = "plaintiff",                                      IsRegex = false },
            new() { Name = "Defendant",        DataType = "text",     SearchHeuristic = "defendant",                                      IsRegex = false },
            new() { Name = "Filing Date",      DataType = "date",     SearchHeuristic = @"filed[\s:]+(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})", IsRegex = true  },
        ],
        _ => []
    };
}
