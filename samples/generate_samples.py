"""Generate three sample PDFs for OCR system testing."""

from fpdf import FPDF
from fpdf.enums import XPos, YPos
import os

OUT = os.path.dirname(__file__)

CURSIVE_FONT_PATH = r"C:\Windows\Fonts\KUNSTLER.TTF"

_HONORIFICS = {"MR", "MRS", "MS", "MISS", "DR", "ATTY", "PROF", "HON", "ENG", "REV", "SIR"}
_SUFFIXES   = {"MD", "RN", "CPA", "FPCP", "JD", "PHD", "MBA", "LLB", "II", "III", "IV", "JR", "SR", "ESQ"}


# ── helpers ────────────────────────────────────────────────────────────────────

def get_initials(name: str) -> str:
    """Return dotted initials from a full name, stripping honorifics and suffixes."""
    tokens = name.upper().replace(".", " ").split()
    keep = [t for t in tokens if t and t not in _HONORIFICS and t not in _SUFFIXES]
    return ".".join(t[0] for t in keep) + "."


def add_cursive(pdf: FPDF):
    """Register Kunstler Script as 'Cursive' on this PDF instance."""
    pdf.add_font("Cursive", "", CURSIVE_FONT_PATH)


def hr(pdf: FPDF, y_offset: int = 2):
    pdf.ln(y_offset)
    pdf.set_draw_color(180, 180, 180)
    pdf.line(pdf.l_margin, pdf.get_y(), pdf.w - pdf.r_margin, pdf.get_y())
    pdf.ln(y_offset)


def section_title(pdf: FPDF, text: str):
    pdf.set_font("Helvetica", "B", 11)
    pdf.set_fill_color(230, 237, 250)
    pdf.cell(0, 8, f"  {text}", new_x=XPos.LMARGIN, new_y=YPos.NEXT, fill=True)
    pdf.set_font("Helvetica", "", 10)
    pdf.ln(2)


def two_col(pdf: FPDF, label: str, value: str, label_w: int = 55):
    pdf.set_font("Helvetica", "B", 10)
    pdf.cell(label_w, 6, label)
    pdf.set_font("Helvetica", "", 10)
    pdf.cell(0, 6, value, new_x=XPos.LMARGIN, new_y=YPos.NEXT)


def sig_block(pdf: FPDF, name: str, title: str, date: str,
              x: float, y: float, w: float = 85):
    """
    Draw a cursive-initial signature block.
    Initials are rendered in Kunstler Script above a signing line,
    followed by printed name / title / date.
    """
    initials = get_initials(name)

    # -- Cursive initials in ink-blue --
    pdf.set_font("Cursive", "", 32)
    pdf.set_text_color(22, 48, 130)
    pdf.set_xy(x, y)
    pdf.cell(w, 20, initials, align="L")

    # -- Signing line --
    line_y = y + 21
    pdf.set_draw_color(70, 70, 70)
    pdf.set_line_width(0.35)
    pdf.line(x, line_y, x + w, line_y)

    # -- Printed name / title / date --
    pdf.set_text_color(0, 0, 0)
    pdf.set_font("Helvetica", "B", 9)
    pdf.set_xy(x, line_y + 2)
    pdf.cell(w, 5, name, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.set_xy(x, pdf.get_y())
    pdf.set_font("Helvetica", "", 8)
    pdf.cell(w, 4, title, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.set_xy(x, pdf.get_y())
    pdf.cell(w, 4, f"Date: {date}", new_x=XPos.LMARGIN, new_y=YPos.NEXT)


# ── 1. Hospital Bill ───────────────────────────────────────────────────────────

def hospital_bill():
    pdf = FPDF()
    pdf.add_page()
    pdf.set_margins(20, 20, 20)
    add_cursive(pdf)

    # Header
    pdf.set_font("Helvetica", "B", 18)
    pdf.set_text_color(20, 60, 120)
    pdf.cell(0, 10, "METRO GENERAL HOSPITAL", new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_font("Helvetica", "", 9)
    pdf.set_text_color(80, 80, 80)
    pdf.cell(0, 5,
        "1428 Rizal Avenue, Makati City, Metro Manila 1200  |  Tel: (02) 8-555-0100  |  www.metrogeneralhospital.ph",
        new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.cell(0, 4, "TIN: 000-123-456-000  |  PhilHealth Accreditation No.: PH-2019-00421",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    hr(pdf, 4)

    pdf.set_font("Helvetica", "B", 13)
    pdf.set_text_color(20, 60, 120)
    pdf.cell(0, 8, "STATEMENT OF ACCOUNT / HOSPITAL BILL",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_text_color(0, 0, 0)
    hr(pdf, 2)
    pdf.ln(2)

    # Patient info
    section_title(pdf, "Patient Information")
    two_col(pdf, "Patient Name:",       "Santos, Maria Luisa C.")
    two_col(pdf, "Date of Birth:",      "March 14, 1985")
    two_col(pdf, "Age / Sex:",          "41 years old  /  Female")
    two_col(pdf, "Patient ID:",         "MGH-2024-00783")
    two_col(pdf, "PhilHealth No.:",     "11-0000034821-1")
    two_col(pdf, "Address:",            "Unit 4B, Sunrise Condo, Pasig City, NCR")
    two_col(pdf, "Admitting Physician:","Dr. Ramon B. Villanueva, MD, FPCP")
    two_col(pdf, "Diagnosis:",          "Community-Acquired Pneumonia (CAP), Moderate Risk")
    two_col(pdf, "Date Admitted:",      "January 8, 2025  --  10:42 AM")
    two_col(pdf, "Date Discharged:",    "January 13, 2025  --  9:00 AM")
    two_col(pdf, "Length of Stay:",     "5 days")
    two_col(pdf, "Room / Ward:",        "Room 308 -- Semi-Private")
    pdf.ln(4)

    # Itemized charges
    section_title(pdf, "Itemized Charges")
    col_w = [80, 35, 30, 35]
    headers = ["Description", "Qty / Days", "Unit Cost", "Amount (PHP)"]
    pdf.set_font("Helvetica", "B", 9)
    pdf.set_fill_color(50, 90, 160)
    pdf.set_text_color(255, 255, 255)
    for w, h in zip(col_w, headers):
        pdf.cell(w, 7, h, border=1, fill=True, align="C")
    pdf.ln()
    pdf.set_text_color(0, 0, 0)

    rows = [
        ("Room and Board (Semi-Private)",        "5",  "3,500.00", "17,500.00"),
        ("Physician's Professional Fee",          "5",  "2,000.00", "10,000.00"),
        ("Chest X-Ray (PA view)",                 "2",    "850.00",  "1,700.00"),
        ("Complete Blood Count (CBC)",            "3",    "450.00",  "1,350.00"),
        ("Blood Culture & Sensitivity",           "1",  "1,800.00",  "1,800.00"),
        ("Urinalysis",                            "2",    "250.00",    "500.00"),
        ("Nebulization Treatment",                "8",    "350.00",  "2,800.00"),
        ("IV Antibiotics -- Ceftriaxone 1g",     "10",    "420.00",  "4,200.00"),
        ("IV Fluid -- PNSS 1L",                   "6",    "180.00",  "1,080.00"),
        ("Oxygen Therapy (per hour)",            "36",     "95.00",  "3,420.00"),
        ("Nursing Care / Professional Fee",       "5",    "600.00",  "3,000.00"),
        ("Operating Room Fee (minor procedure)", "1",  "5,500.00",  "5,500.00"),
        ("Anesthesiologist Fee",                  "1",  "3,500.00",  "3,500.00"),
        ("Medicines & Supplies",                 "--",       "--",   "6,230.00"),
        ("ECG",                                   "1",    "750.00",    "750.00"),
        ("2D Echo",                               "1",  "3,200.00",  "3,200.00"),
        ("Emergency Room Fee",                    "1",  "1,200.00",  "1,200.00"),
        ("Administrative / Processing Fee",       "1",    "500.00",    "500.00"),
    ]
    for i, (desc, qty, unit, amt) in enumerate(rows):
        fill = i % 2 == 0
        pdf.set_fill_color(245, 248, 255)
        pdf.set_font("Helvetica", "", 9)
        pdf.cell(col_w[0], 6, f"  {desc}", border="LR", fill=fill)
        pdf.cell(col_w[1], 6, qty,  border="LR", fill=fill, align="C")
        pdf.cell(col_w[2], 6, unit, border="LR", fill=fill, align="R")
        pdf.cell(col_w[3], 6, amt,  border="LR", fill=fill, align="R")
        pdf.ln()

    pdf.set_font("Helvetica", "", 9)
    pdf.cell(sum(col_w[:3]), 6, "", border="T")
    pdf.ln()

    def total_row(label, amount, bold=False):
        pdf.set_font("Helvetica", "B" if bold else "", 10)
        pdf.cell(sum(col_w[:3]), 6, label, align="R")
        pdf.cell(col_w[3], 6, amount, align="R", new_x=XPos.LMARGIN, new_y=YPos.NEXT)

    pdf.ln(2)
    total_row("Gross Total:", "PHP  68,030.00")
    total_row("PhilHealth Deduction:", "(PHP  15,000.00)")
    total_row("HMO Coverage (Medicard):", "(PHP  20,000.00)")
    hr(pdf, 1)
    total_row("BALANCE DUE:", "PHP  33,030.00", bold=True)
    pdf.ln(4)

    # Payment info
    section_title(pdf, "Payment Information")
    two_col(pdf, "Mode of Payment:",       "Split: Cash + Credit Card (BDO Visa)")
    two_col(pdf, "Cash Paid:",             "PHP  13,030.00")
    two_col(pdf, "Credit Card Charged:",   "PHP  20,000.00  (Ref: 2025011300084)")
    two_col(pdf, "OR Number:",             "MGH-OR-2025-041892")
    two_col(pdf, "Cashier:",               "De Leon, Ana R.")
    two_col(pdf, "Transaction Date:",      "January 13, 2025  --  8:47 AM")
    pdf.ln(4)

    # Footer note
    pdf.set_font("Helvetica", "I", 8)
    pdf.set_text_color(100, 100, 100)
    pdf.multi_cell(0, 4,
        "This statement is computer-generated. For inquiries contact our Billing Department "
        "at (02) 8-555-0110 or billing@metrogeneralhospital.ph. Present this document with "
        "a valid ID for any adjustments or reimbursement claims.")
    pdf.set_text_color(0, 0, 0)
    pdf.ln(6)

    # ── Authorized Signatures ──────────────────────────────────────────────────
    section_title(pdf, "Authorized Signatures")
    pdf.ln(2)
    sig_y = pdf.get_y()
    lm = pdf.l_margin
    sig_block(pdf, "DR. RAMON B. VILLANUEVA",
              "Attending Physician, MD, FPCP", "January 13, 2025",
              lm, sig_y)
    sig_block(pdf, "ANA R. DE LEON",
              "Billing Officer / Cashier", "January 13, 2025",
              lm + 100, sig_y)
    pdf.set_y(sig_y + 36)

    pdf.output(os.path.join(OUT, "hospital_bill.pdf"))
    print("  OK  hospital_bill.pdf")


# ── 2. Legal Document -- Restaurant Monthly Sales ──────────────────────────────

def legal_restaurant():
    pdf = FPDF()
    pdf.add_page()
    pdf.set_margins(25, 25, 25)
    add_cursive(pdf)

    pdf.set_font("Helvetica", "B", 13)
    pdf.set_text_color(20, 20, 20)
    pdf.cell(0, 8, "REPUBLIC OF THE PHILIPPINES",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_font("Helvetica", "", 10)
    pdf.cell(0, 5, "DEPARTMENT OF TRADE AND INDUSTRY -- NCR REGIONAL OFFICE",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.cell(0, 5, "BUREAU OF DOMESTIC TRADE AND CONSUMER PROTECTION",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    hr(pdf, 4)

    pdf.set_font("Helvetica", "B", 12)
    pdf.set_text_color(20, 60, 120)
    pdf.cell(0, 8, "SWORN DECLARATION OF MONTHLY GROSS SALES",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_font("Helvetica", "I", 10)
    pdf.set_text_color(80, 80, 80)
    pdf.cell(0, 5,
        "Pursuant to Section 7 of RA 7394 (Consumer Act of the Philippines) and DTI AO No. 18-01",
        new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_text_color(0, 0, 0)
    hr(pdf, 4)
    pdf.ln(2)

    # Business info
    section_title(pdf, "Business Information")
    two_col(pdf, "Business Name:",        "Casa Filipina Restaurant & Events Hall")
    two_col(pdf, "Trade Name / Brand:",   "Casa Filipina")
    two_col(pdf, "Business Address:",     "G/F Ermita Place Bldg., T.M. Kalaw Ave., Manila 1000")
    two_col(pdf, "Business Registration:","DTI-NCR-2018-00934  /  BIR TIN: 225-816-701-000")
    two_col(pdf, "SEC Registration:",     "CS201800341219")
    two_col(pdf, "Type of Business:",     "Food Service -- Full-Service Restaurant & Event Venue")
    two_col(pdf, "Date of Operations:",   "February 12, 2018 to present")
    two_col(pdf, "Proprietor / Owner:",   "Anastacia R. Fontanilla")
    two_col(pdf, "Contact Number:",       "+63-917-555-8841")
    two_col(pdf, "Email Address:",        "admin@casafilipina.com.ph")
    pdf.ln(4)

    # Monthly sales table
    section_title(pdf, "Monthly Gross Sales Report -- Fiscal Year 2024")
    col_w = [50, 38, 38, 44]
    hdrs = ["Month", "Dine-in Sales (PHP)", "Events/Catering (PHP)", "Total Gross (PHP)"]
    pdf.set_font("Helvetica", "B", 9)
    pdf.set_fill_color(50, 90, 160)
    pdf.set_text_color(255, 255, 255)
    for w, h in zip(col_w, hdrs):
        pdf.cell(w, 7, h, border=1, fill=True, align="C")
    pdf.ln()
    pdf.set_text_color(0, 0, 0)

    monthly = [
        ("January 2024",   "548,200.00",   "180,000.00",   "728,200.00"),
        ("February 2024",  "612,450.00",   "245,000.00",   "857,450.00"),
        ("March 2024",     "590,100.00",   "210,500.00",   "800,600.00"),
        ("April 2024",     "503,800.00",   "155,000.00",   "658,800.00"),
        ("May 2024",       "634,700.00",   "320,000.00",   "954,700.00"),
        ("June 2024",      "588,300.00",   "265,000.00",   "853,300.00"),
        ("July 2024",      "521,900.00",   "190,000.00",   "711,900.00"),
        ("August 2024",    "605,200.00",   "230,000.00",   "835,200.00"),
        ("September 2024", "647,500.00",   "275,000.00",   "922,500.00"),
        ("October 2024",   "701,800.00",   "340,000.00", "1,041,800.00"),
        ("November 2024",  "823,400.00",   "415,000.00", "1,238,400.00"),
        ("December 2024",  "980,600.00",   "580,000.00", "1,560,600.00"),
    ]
    for i, row in enumerate(monthly):
        fill = i % 2 == 0
        pdf.set_fill_color(245, 248, 255)
        pdf.set_font("Helvetica", "", 9)
        pdf.cell(col_w[0], 6, f"  {row[0]}", border="LR", fill=fill)
        for j in range(1, 4):
            pdf.cell(col_w[j], 6, row[j], border="LR", fill=fill, align="R")
        pdf.ln()

    pdf.set_font("Helvetica", "B", 9)
    pdf.set_fill_color(220, 230, 245)
    pdf.cell(col_w[0], 7,  "  TOTAL FY 2024",   border=1, fill=True)
    pdf.cell(col_w[1], 7, "7,757,950.00",       border=1, fill=True, align="R")
    pdf.cell(col_w[2], 7, "3,405,500.00",       border=1, fill=True, align="R")
    pdf.cell(col_w[3], 7, "11,163,450.00",      border=1, fill=True, align="R")
    pdf.ln(6)

    # Attestation
    section_title(pdf, "Attestation")
    pdf.set_font("Helvetica", "", 10)
    pdf.multi_cell(0, 6,
        "I, ANASTACIA R. FONTANILLA, of legal age, Filipino citizen, and proprietor of "
        "CASA FILIPINA RESTAURANT & EVENTS HALL, with registered address at G/F Ermita Place Bldg., "
        "T.M. Kalaw Ave., Manila, after having been sworn in accordance with law, do hereby depose and state:\n\n"
        "1.  That I am the duly registered owner/operator of the above-named business establishment;\n"
        "2.  That the monthly gross sales figures declared above are true, correct, and complete to the best "
        "of my knowledge and belief, and are based on official receipts and books of accounts maintained in "
        "accordance with BIR regulations;\n"
        "3.  That said business has complied with all applicable tax obligations, including VAT remittances under "
        "Sections 106-108 of the National Internal Revenue Code, as amended;\n"
        "4.  That I execute this sworn declaration voluntarily and without duress, fully aware of the legal "
        "consequences of any false statement herein.")
    pdf.ln(5)

    pdf.set_font("Helvetica", "", 10)
    pdf.cell(0, 5, "Executed in Manila, Philippines, this 15th day of January 2025.",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.ln(4)

    # ── Proprietor signature ──────────────────────────────────────────────────
    lm = pdf.l_margin
    sig_y = pdf.get_y()
    sig_block(pdf, "ANASTACIA R. FONTANILLA",
              "Proprietor / Authorized Signatory", "January 15, 2025",
              lm, sig_y)
    pdf.set_y(sig_y + 36)
    pdf.set_font("Helvetica", "", 9)
    pdf.cell(0, 5, "TIN: 225-816-701-000", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.ln(8)

    # ── Notarial acknowledgment ──────────────────────────────────────────────
    pdf.set_font("Helvetica", "B", 10)
    pdf.cell(0, 6,
        "SUBSCRIBED AND SWORN TO before me this 15th day of January 2025 in Manila, Philippines.",
        new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.ln(2)
    pdf.set_font("Helvetica", "", 9)
    pdf.cell(0, 5,
        "Affiant exhibited to me competent evidence of identity: Passport No. P3918274 (issued Feb 3, 2022).",
        new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.ln(4)

    # Notary signature
    sig_y2 = pdf.get_y()
    sig_block(pdf, "ATTY. JOSE MIGUEL D. REYES",
              "Notary Public for Manila", "January 15, 2025",
              lm, sig_y2)
    pdf.set_y(sig_y2 + 36)
    pdf.set_font("Helvetica", "", 8)
    pdf.cell(0, 4, "PTR No. 8803421 -- January 2, 2025 -- Manila",  new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.cell(0, 4, "Roll of Attorneys No. 45821",                    new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.cell(0, 4, "IBP No. 1093411 -- Lifetime",                    new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.cell(0, 4, "MCLE Compliance No. VI-0042315",                 new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.cell(0, 4, "Notarial Commission valid until December 31, 2026",new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.ln(3)
    pdf.set_text_color(100, 100, 100)
    pdf.cell(0, 4, "Doc. No. 88;  Page No. 18;  Book No. IV;  Series of 2025.",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT)

    pdf.output(os.path.join(OUT, "restaurant_legal_sales.pdf"))
    print("  OK  restaurant_legal_sales.pdf")


# ── 3. Board Meeting Minutes -- Company Expansion to Philippines ────────────────

def meeting_minutes():
    pdf = FPDF()
    pdf.add_page()
    pdf.set_margins(22, 22, 22)
    add_cursive(pdf)

    pdf.set_font("Helvetica", "B", 16)
    pdf.set_text_color(15, 50, 110)
    pdf.cell(0, 10, "NEXBRIDGE HOLDINGS CORPORATION",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_font("Helvetica", "", 9)
    pdf.set_text_color(80, 80, 80)
    pdf.cell(0, 5,
        "Registered Office: 38/F Zuellig Building, Makati Avenue cor. Paseo de Roxas, Makati City 1225",
        new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.cell(0, 4, "SEC Registration No. CS201600128341  |  www.nexbridgeholdings.com",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    hr(pdf, 4)

    pdf.set_font("Helvetica", "B", 13)
    pdf.set_text_color(15, 50, 110)
    pdf.cell(0, 8, "MINUTES OF THE SPECIAL BOARD MEETING",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_font("Helvetica", "I", 10)
    pdf.set_text_color(60, 60, 60)
    pdf.cell(0, 5,
        "Re: Proposed Business Expansion -- Republic of the Philippines Operations",
        new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_text_color(0, 0, 0)
    hr(pdf, 3)
    pdf.ln(2)

    # Meeting details
    section_title(pdf, "Meeting Details")
    two_col(pdf, "Date:",             "April 14, 2025")
    two_col(pdf, "Time:",             "2:00 PM -- 5:30 PM (GMT+8, Philippine Standard Time)")
    two_col(pdf, "Venue:",            "Boardroom A, 38/F Zuellig Building, Makati City")
    two_col(pdf, "Meeting Type:",     "Special Board Meeting -- Extraordinary Session")
    two_col(pdf, "Minutes Taken by:", "Ms. Carla Dizon, Corporate Secretary")
    two_col(pdf, "Reference No.:",    "NBH-SBM-2025-003")
    pdf.ln(4)

    # Directors present
    section_title(pdf, "Directors Present")
    attendees = [
        ("1.", "Mr. Richard A. Alcantara",  "Chairman of the Board",              "Present"),
        ("2.", "Ms. Elena B. Montoya",       "President & CEO",                    "Present"),
        ("3.", "Mr. David C. Lim",           "Chief Financial Officer / Director", "Present"),
        ("4.", "Atty. Grace D. Reyes",       "Corporate Secretary / Director",     "Present"),
        ("5.", "Mr. Franklin E. Santos",     "Independent Director",               "Present (via video conference)"),
        ("6.", "Ms. Patricia F. Ocampo",     "Independent Director",               "Present"),
    ]
    pdf.set_font("Helvetica", "B", 9)
    pdf.cell(8,  6, "#")
    pdf.cell(60, 6, "Name")
    pdf.cell(80, 6, "Position")
    pdf.cell(0,  6, "Attendance")
    pdf.ln()
    pdf.set_font("Helvetica", "", 9)
    for row in attendees:
        pdf.cell(8,  6, row[0])
        pdf.cell(60, 6, row[1])
        pdf.cell(80, 6, row[2])
        pdf.cell(0,  6, row[3], new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.ln(2)
    pdf.set_font("Helvetica", "I", 9)
    pdf.cell(0, 5, "Quorum: 6 of 6 directors present (100%). Meeting declared properly convened.",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.ln(4)

    # Agenda
    section_title(pdf, "Agenda")
    agenda = [
        ("1.", "Call to order and determination of quorum"),
        ("2.", "Approval of agenda"),
        ("3.", "Presentation: Philippine Market Feasibility Study (CFO Report)"),
        ("4.", "Discussion: Legal and regulatory requirements for foreign business registration in the Philippines"),
        ("5.", "Resolution: Approval of the Philippine Expansion Plan and capital allocation"),
        ("6.", "Resolution: Authority to negotiate and execute joint venture / partnership agreements"),
        ("7.", "Other matters"),
        ("8.", "Adjournment"),
    ]
    pdf.set_font("Helvetica", "", 10)
    for num, item in agenda:
        pdf.cell(10, 6, num)
        pdf.cell(0, 6, item, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.ln(4)

    # Proceedings
    section_title(pdf, "Proceedings")

    pdf.set_font("Helvetica", "B", 10)
    pdf.cell(0, 6, "1.  Call to Order", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.set_font("Helvetica", "", 10)
    pdf.multi_cell(0, 6,
        "The Chairman, Mr. Richard A. Alcantara, called the meeting to order at 2:05 PM and confirmed that a "
        "quorum was present with all six (6) members of the Board of Directors in attendance, satisfying the "
        "requirement under Section 52 of the Revised Corporation Code of the Philippines (RA 11232).")
    pdf.ln(3)

    pdf.set_font("Helvetica", "B", 10)
    pdf.cell(0, 6, "2.  Approval of Agenda", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.set_font("Helvetica", "", 10)
    pdf.multi_cell(0, 6,
        "The agenda as circulated on April 10, 2025 was presented. Upon motion duly made by Ms. Elena Montoya "
        "and seconded by Mr. David Lim, the agenda was unanimously approved without amendment.")
    pdf.ln(3)

    pdf.set_font("Helvetica", "B", 10)
    pdf.cell(0, 6, "3.  CFO Presentation -- Philippine Market Feasibility Study",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.set_font("Helvetica", "", 10)
    pdf.multi_cell(0, 6,
        "Mr. David C. Lim presented the results of the six-month Philippine Market Feasibility Study commissioned "
        "in October 2024. Key findings presented:\n\n"
        "    a.  Market Opportunity: The Philippine digital infrastructure market is projected to grow at a CAGR of "
        "18.4% through 2028, reaching USD 4.2 billion. Demand is driven by IATF digitalization mandates, DICT "
        "national broadband expansion, and rapid adoption of cloud services in BFSI and BPO sectors.\n\n"
        "    b.  Competitive Landscape: Three incumbent players (Converge ICT, Globe Telecom Infra, and PLDT "
        "Enterprise) hold approximately 71% market share. A regulatory opening under the PSA's Open Access "
        "Data Transmission Act presents a credible entry point.\n\n"
        "    c.  Financial Projections: Initial investment of USD 12.5 million for Year 1 (entity setup, DICT "
        "registration, leased data center co-location in Clark Freeport Zone). Projected break-even at Month 22, "
        "with cumulative 5-year NPV of USD 28.7 million at a 10% hurdle rate.\n\n"
        "    d.  Risk Factors: Foreign equity restrictions (40% cap under the Foreign Investment Negative List) "
        "require a Philippine joint-venture partner or nominee structure. Exchange-rate volatility (PHP/USD) and "
        "political risk index rated Medium by the Economist Intelligence Unit.")
    pdf.ln(3)

    pdf.set_font("Helvetica", "B", 10)
    pdf.cell(0, 6, "4.  Legal & Regulatory Discussion", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.set_font("Helvetica", "", 10)
    pdf.multi_cell(0, 6,
        "Atty. Grace D. Reyes briefed the Board on the legal framework governing the proposed expansion:\n\n"
        "    a.  Foreign Investment Act (RA 7042 as amended by RA 8179 and RA 11647): Foreign equity in certain "
        "sectors remains restricted. A Domestic Market Enterprise with 60% Filipino equity is the recommended "
        "structure.\n\n"
        "    b.  SEC Registration: The Philippine subsidiary, to be incorporated as NEXBRIDGE DIGITAL SOLUTIONS, "
        "INC., must file Articles of Incorporation with the SEC and secure a Certificate of Registration within "
        "60 days of Board approval.\n\n"
        "    c.  PEZA / BOI Incentives: Registration with either PEZA (if operating within an ecozoned facility) "
        "or BOI under the Strategic Investment Priority Plan (SIPP) may entitle the company to 4--6 year income "
        "tax holiday and duty-free importation of capital equipment.\n\n"
        "    d.  Proposed JV Partner: After due diligence, management recommends Meridian Infrastructure Group, "
        "Inc. (MIGI) as the Philippine partner (60% equity), a Makati-based firm with existing DICT and NTC "
        "accreditations.")
    pdf.ln(3)

    pdf.set_font("Helvetica", "B", 10)
    pdf.cell(0, 6, "5.  Board Resolutions", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.set_font("Helvetica", "", 10)

    resolutions = [
        ("Resolution No. 2025-003-01", "APPROVAL OF PHILIPPINE EXPANSION PLAN",
         "RESOLVED, that the Board of Directors hereby approves in principle the expansion of Nexbridge Holdings "
         "Corporation's operations into the Republic of the Philippines through the establishment of a domestic "
         "subsidiary, NEXBRIDGE DIGITAL SOLUTIONS, INC., with an initial authorized capital stock of PHP 50,000,000 "
         "(USD ~870,000 at prevailing rate), subject to SEC registration and regulatory approvals."),
        ("Resolution No. 2025-003-02", "CAPITAL ALLOCATION",
         "FURTHER RESOLVED, that the Board authorizes an initial capital outlay not exceeding USD 12,500,000 for "
         "Year 1 operations, to be sourced from retained earnings and a USD 5,000,000 revolving credit facility "
         "to be arranged with a local Philippine banking institution, subject to CFO approval of final drawdown terms."),
        ("Resolution No. 2025-003-03", "JOINT VENTURE AUTHORITY",
         "FURTHER RESOLVED, that the President & CEO, Ms. Elena B. Montoya, and the CFO, Mr. David C. Lim, are "
         "jointly authorized to negotiate, finalize, and execute on behalf of the Corporation a Joint Venture "
         "Agreement with MERIDIAN INFRASTRUCTURE GROUP, INC. (MIGI) and/or any other qualified Philippine partner, "
         "on such terms as the said officers shall deem reasonable and in the best interest of the Corporation, "
         "provided that any agreement must be ratified by the Board within 30 days of execution."),
        ("Resolution No. 2025-003-04", "APPOINTMENT OF EXTERNAL LEGAL COUNSEL",
         "FURTHER RESOLVED, that the firm SyCip Salazar Hernandez & Gatmaitan (SyCipLaw) is hereby engaged as "
         "external legal counsel to advise on all Philippine regulatory, corporate, and transactional matters "
         "relating to the expansion, at a retainer not exceeding PHP 500,000 per month."),
    ]
    for res_no, res_title, res_body in resolutions:
        pdf.set_font("Helvetica", "B", 9)
        pdf.cell(0, 6, f"    {res_no} -- {res_title}", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        pdf.set_font("Helvetica", "", 9)
        pdf.multi_cell(0, 5, f"    {res_body}")
        pdf.set_font("Helvetica", "I", 9)
        pdf.cell(0, 5, "    VOTE:  6 in favor, 0 against, 0 abstention -- CARRIED UNANIMOUSLY.",
                 new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        pdf.ln(3)

    pdf.set_font("Helvetica", "B", 10)
    pdf.cell(0, 6, "6.  Adjournment", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.set_font("Helvetica", "", 10)
    pdf.multi_cell(0, 6,
        "There being no further business to transact, the Chairman declared the meeting adjourned at 5:28 PM. "
        "The next meeting of the Board is tentatively scheduled for May 19, 2025.")
    pdf.ln(6)

    # Certification
    hr(pdf, 2)
    pdf.set_font("Helvetica", "I", 9)
    pdf.multi_cell(0, 5,
        "I hereby certify that the foregoing is a true and accurate record of the proceedings of the Special "
        "Board Meeting of Nexbridge Holdings Corporation held on April 14, 2025.")
    pdf.ln(6)

    # ── Four signatures in 2x2 grid ────────────────────────────────────────────
    section_title(pdf, "Certified and Attested -- Board of Directors")
    pdf.ln(2)

    lm = pdf.l_margin
    sig_y_top = pdf.get_y()
    sig_y_bot = sig_y_top + 48   # row spacing accommodates 32pt initials + metadata

    signers = [
        ("MR. RICHARD A. ALCANTARA",  "Chairman of the Board",              "April 14, 2025"),
        ("MS. ELENA B. MONTOYA",       "President & Chief Executive Officer", "April 14, 2025"),
        ("MR. DAVID C. LIM",           "Chief Financial Officer / Director",  "April 14, 2025"),
        ("ATTY. GRACE D. REYES",       "Corporate Secretary / Director",      "April 14, 2025"),
    ]

    col_x  = [lm, lm + 93]
    rows_y = [sig_y_top, sig_y_bot]

    for i, (name, title, date) in enumerate(signers):
        sig_block(pdf, name, title, date, col_x[i % 2], rows_y[i // 2])

    pdf.set_y(sig_y_bot + 38)
    pdf.set_font("Helvetica", "", 8)
    pdf.set_text_color(100, 100, 100)
    pdf.cell(0, 4,
        "NBH-SBM-2025-003  |  Page 1 of 1  |  Nexbridge Holdings Corporation -- Confidential",
        new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")

    pdf.output(os.path.join(OUT, "meeting_minutes_ph_expansion.pdf"))
    print("  OK  meeting_minutes_ph_expansion.pdf")


# ── 4. Philippine Payslip ─────────────────────────────────────────────────────

def payslip():
    pdf = FPDF()
    pdf.add_page()
    pdf.set_margins(18, 18, 18)
    add_cursive(pdf)

    # ── Header ────────────────────────────────────────────────────────────────
    pdf.set_font("Helvetica", "B", 15)
    pdf.set_text_color(15, 50, 110)
    pdf.cell(0, 9, "NEXBRIDGE HOLDINGS CORPORATION",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_font("Helvetica", "", 8)
    pdf.set_text_color(80, 80, 80)
    pdf.cell(0, 4,
        "38/F Zuellig Building, Makati Avenue cor. Paseo de Roxas, Makati City 1225",
        new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.cell(0, 4, "TIN: 004-801-445-000  |  HR Hotline: (02) 8-888-0200  |  hr@nexbridgeholdings.com",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    hr(pdf, 3)

    pdf.set_font("Helvetica", "B", 12)
    pdf.set_text_color(15, 50, 110)
    pdf.cell(0, 7, "PAYSLIP / EARNINGS STATEMENT",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_text_color(0, 0, 0)
    hr(pdf, 2)
    pdf.ln(2)

    # ── Employee info (two panels side by side) ───────────────────────────────
    section_title(pdf, "Employee Information")

    lm = pdf.l_margin
    col = 88  # left-panel width
    start_y = pdf.get_y()

    def epair(label, value, x, y, lw=42):
        pdf.set_xy(x, y)
        pdf.set_font("Helvetica", "B", 9)
        pdf.cell(lw, 5.5, label)
        pdf.set_font("Helvetica", "", 9)
        pdf.cell(0, 5.5, value, new_x=XPos.LMARGIN, new_y=YPos.NEXT)

    left = [
        ("Employee Name:",    "Reyes, Marco L."),
        ("Employee ID:",      "NBH-EMP-00418"),
        ("Department:",       "Information Technology"),
        ("Position / Grade:", "Senior Systems Engineer / Grade 7"),
        ("Employment Type:",  "Regular"),
        ("Date Hired:",       "June 15, 2020"),
    ]
    right = [
        ("Pay Period:",       "March 1 -- 31, 2025"),
        ("Payment Date:",     "April 5, 2025"),
        ("Bank:",             "BDO Unibank"),
        ("Account No.:",      "****-****-1842"),
        ("Cost Center:",      "CC-IT-001"),
        ("Manager:",          "Ms. Elena B. Montoya"),
    ]

    for i, ((ll, lv), (rl, rv)) in enumerate(zip(left, right)):
        y = start_y + i * 5.5
        epair(ll, lv, lm,        y)
        epair(rl, rv, lm + col,  y, lw=36)

    pdf.set_y(start_y + len(left) * 5.5 + 4)
    pdf.ln(2)

    # ── Government IDs (regex targets) ────────────────────────────────────────
    section_title(pdf, "Government-Mandated Contributions -- Reference Numbers")
    pdf.ln(1)

    gov_ids = [
        ("SSS No.:",        "04-1234567-8"),
        ("PhilHealth No.:", "11-0000418321-5"),
        ("Pag-IBIG No.:",   "1234-5678-9012"),
        ("TIN:",            "318-442-091-000"),
    ]
    for label, val in gov_ids:
        two_col(pdf, label, val, 50)
    pdf.ln(4)

    # ── Earnings & Deductions ─────────────────────────────────────────────────
    section_title(pdf, "Earnings and Deductions -- Pay Period: March 1-31, 2025")

    ecol = [80, 40]   # description / amount
    dcol = [80, 40]

    def earn_header(label, x):
        pdf.set_font("Helvetica", "B", 9)
        pdf.set_fill_color(50, 90, 160)
        pdf.set_text_color(255, 255, 255)
        pdf.set_xy(x, pdf.get_y())
        pdf.cell(ecol[0], 6, f"  {label}", border=1, fill=True)
        pdf.cell(ecol[1], 6, "Amount (PHP)", border=1, fill=True, align="R")

    earn_x = lm
    ded_x  = lm + sum(ecol) + 4

    table_y = pdf.get_y()
    earn_header("EARNINGS", earn_x)
    pdf.set_xy(ded_x, table_y)
    earn_header("DEDUCTIONS", ded_x)
    pdf.ln()

    earnings = [
        ("Basic Salary",              "45,000.00"),
        ("Transportation Allowance",   "3,000.00"),
        ("Meal Allowance",             "2,000.00"),
        ("Clothing Allowance",           "500.00"),
        ("Project Bonus",              "5,000.00"),
        ("Night Differential",           "750.00"),
        ("Overtime Pay (8.5 hrs)",       "875.63"),
    ]
    deductions = [
        ("SSS Contribution",           "1,125.00"),
        ("PhilHealth Contribution",      "562.50"),
        ("Pag-IBIG Contribution",        "100.00"),
        ("Withholding Tax",            "4,218.75"),
        ("Company Loan Deduction",     "2,000.00"),
        ("Late / Absence Deduction",     "375.00"),
        ("",                                   ""),
    ]

    pdf.set_text_color(0, 0, 0)
    for i, ((el, ea), (dl, da)) in enumerate(zip(earnings, deductions)):
        row_y = pdf.get_y()
        fill = i % 2 == 0
        pdf.set_fill_color(245, 248, 255)
        pdf.set_font("Helvetica", "", 9)

        pdf.set_xy(earn_x, row_y)
        pdf.cell(ecol[0], 5.5, f"  {el}", border="LR", fill=fill)
        pdf.cell(ecol[1], 5.5, ea, border="LR", fill=fill, align="R")

        pdf.set_xy(ded_x, row_y)
        pdf.cell(dcol[0], 5.5, f"  {dl}", border="LR", fill=fill)
        pdf.cell(dcol[1], 5.5, da, border="LR", fill=fill, align="R")
        pdf.ln()

    # Totals row
    row_y = pdf.get_y()
    pdf.set_font("Helvetica", "B", 9)
    pdf.set_fill_color(220, 230, 245)

    pdf.set_xy(earn_x, row_y)
    pdf.cell(ecol[0], 6, "  GROSS EARNINGS", border=1, fill=True)
    pdf.cell(ecol[1], 6, "57,125.63",        border=1, fill=True, align="R")

    pdf.set_xy(ded_x, row_y)
    pdf.cell(dcol[0], 6, "  TOTAL DEDUCTIONS", border=1, fill=True)
    pdf.cell(dcol[1], 6, "8,381.25",           border=1, fill=True, align="R")
    pdf.ln(8)

    # Net Pay callout
    pdf.set_font("Helvetica", "B", 12)
    pdf.set_fill_color(15, 50, 110)
    pdf.set_text_color(255, 255, 255)
    pdf.cell(0, 10, "  NET PAY:   PHP  48,744.38", fill=True,
             new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.set_text_color(0, 0, 0)
    pdf.ln(4)

    # ── Signature ─────────────────────────────────────────────────────────────
    section_title(pdf, "Acknowledgment")
    pdf.set_font("Helvetica", "", 9)
    pdf.multi_cell(0, 5,
        "I hereby acknowledge receipt of the above-stated net pay for the pay period "
        "March 1--31, 2025, and confirm that the earnings and deductions listed are correct.")
    pdf.ln(5)

    sig_y = pdf.get_y()
    sig_block(pdf, "MARCO L. REYES",
              "Employee -- Senior Systems Engineer", "April 5, 2025",
              lm, sig_y)
    sig_block(pdf, "MS. ELENA B. MONTOYA",
              "President & CEO / Authorized Signatory", "April 5, 2025",
              lm + 100, sig_y)
    pdf.set_y(sig_y + 36)
    pdf.ln(2)
    pdf.set_font("Helvetica", "I", 7)
    pdf.set_text_color(130, 130, 130)
    pdf.cell(0, 4,
        "This payslip is system-generated. For discrepancies contact payroll@nexbridgeholdings.com.",
        new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")

    pdf.output(os.path.join(OUT, "payslip.pdf"))
    print("  OK  payslip.pdf")


# ── 5. Bank Fund-Transfer Receipt ─────────────────────────────────────────────

def bank_receipt():
    pdf = FPDF()
    pdf.add_page()
    pdf.set_margins(30, 30, 30)
    add_cursive(pdf)

    # ── Bank header ───────────────────────────────────────────────────────────
    pdf.set_font("Helvetica", "B", 16)
    pdf.set_text_color(0, 70, 140)
    pdf.cell(0, 10, "BDO UNIBANK, INC.",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_font("Helvetica", "", 9)
    pdf.set_text_color(80, 80, 80)
    pdf.cell(0, 4, "BDO Online Banking  |  Customer Care: (02) 8-631-8000  |  www.bdo.com.ph",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    hr(pdf, 4)

    pdf.set_font("Helvetica", "B", 13)
    pdf.set_text_color(0, 70, 140)
    pdf.cell(0, 8, "FUND TRANSFER TRANSACTION RECEIPT",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_font("Helvetica", "I", 9)
    pdf.set_text_color(80, 80, 80)
    pdf.cell(0, 5, "Interbank Funds Transfer via PESONet",
             new_x=XPos.LMARGIN, new_y=YPos.NEXT, align="C")
    pdf.set_text_color(0, 0, 0)
    hr(pdf, 4)
    pdf.ln(2)

    # ── Transaction details ───────────────────────────────────────────────────
    section_title(pdf, "Transaction Details")

    details = [
        ("Transaction Ref. No.:", "BD202503280041827364"),
        ("Transaction Type:",     "PESONet Interbank Fund Transfer"),
        ("Transaction Status:",   "SUCCESSFUL"),
        ("Transaction Date:",     "03/28/2025 14:22:45"),
        ("Value Date:",           "03/28/2025"),
        ("Channel:",              "BDO Online Banking (Web)"),
        ("Device ID:",            "WEB-PH-8841-4422"),
    ]
    for label, value in details:
        pdf.set_font("Helvetica", "B", 10)
        pdf.set_text_color(50, 50, 50)
        pdf.cell(65, 7, label)
        pdf.set_font("Helvetica", "", 10)
        if label == "Transaction Status:":
            pdf.set_text_color(0, 140, 60)
        else:
            pdf.set_text_color(0, 0, 0)
        pdf.cell(0, 7, value, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.set_text_color(0, 0, 0)
    pdf.ln(3)

    # ── Sender ────────────────────────────────────────────────────────────────
    section_title(pdf, "Sender Information")
    sender = [
        ("Account Name:",   "NEXBRIDGE HOLDINGS CORPORATION"),
        ("Account No.:",    "********1842"),
        ("Account Type:",   "BDO Current Account"),
        ("Branch:",         "Makati Zuellig Building Branch"),
        ("Bank:",           "BDO Unibank, Inc."),
    ]
    for label, value in sender:
        two_col(pdf, label, value, 55)
    pdf.ln(3)

    # ── Receiver ──────────────────────────────────────────────────────────────
    section_title(pdf, "Recipient Information")
    receiver = [
        ("Account Name:",   "MERIDIAN INFRASTRUCTURE GROUP, INC."),
        ("Account No.:",    "********7731"),
        ("Account Type:",   "Savings Account"),
        ("Bank:",           "Metropolitan Bank and Trust Company (Metrobank)"),
        ("Branch:",         "Ortigas Center Branch"),
        ("Bank Code:",      "010269995"),
    ]
    for label, value in receiver:
        two_col(pdf, label, value, 55)
    pdf.ln(3)

    # ── Amount breakdown ──────────────────────────────────────────────────────
    section_title(pdf, "Amount Details")

    amt_col = [100, 50]
    pdf.set_font("Helvetica", "B", 9)
    pdf.set_fill_color(50, 90, 160)
    pdf.set_text_color(255, 255, 255)
    pdf.cell(amt_col[0], 6, "  Description",    border=1, fill=True)
    pdf.cell(amt_col[1], 6, "Amount (PHP)",     border=1, fill=True, align="R")
    pdf.ln()
    pdf.set_text_color(0, 0, 0)

    amts = [
        ("Transfer Amount",              "500,000.00"),
        ("PESONet Processing Fee",             "15.00"),
        ("VAT on Processing Fee (12%)",         "1.80"),
    ]
    for i, (desc, amt) in enumerate(amts):
        fill = i % 2 == 0
        pdf.set_fill_color(245, 248, 255)
        pdf.set_font("Helvetica", "", 9)
        pdf.cell(amt_col[0], 6, f"  {desc}", border="LR", fill=fill)
        pdf.cell(amt_col[1], 6, amt,          border="LR", fill=fill, align="R")
        pdf.ln()
    pdf.set_font("Helvetica", "B", 10)
    pdf.set_fill_color(15, 50, 110)
    pdf.set_text_color(255, 255, 255)
    pdf.cell(amt_col[0], 7, "  TOTAL AMOUNT DEBITED", border=1, fill=True)
    pdf.cell(amt_col[1], 7, "PHP 500,016.80",          border=1, fill=True, align="R")
    pdf.ln(6)
    pdf.set_text_color(0, 0, 0)

    # ── Running balance ───────────────────────────────────────────────────────
    pdf.set_font("Helvetica", "", 10)
    pdf.set_fill_color(240, 245, 255)
    pdf.cell(0, 7,
        "  Available Balance After Transfer:   PHP  2,184,337.92",
        fill=True, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
    pdf.ln(5)

    # ── Authorization signature ───────────────────────────────────────────────
    pdf.set_draw_color(180, 180, 180)
    pdf.set_line_width(0.2)
    pdf.set_font("Helvetica", "I", 9)
    pdf.set_text_color(80, 80, 80)
    pdf.multi_cell(0, 5,
        "This is an official transaction receipt generated by BDO Online Banking. "
        "For disputes or inquiries call (02) 8-631-8000 or visit any BDO branch within 15 days.")
    pdf.set_text_color(0, 0, 0)
    pdf.ln(5)

    sig_y = pdf.get_y()
    sig_block(pdf, "DAVID C. LIM",
              "CFO / Authorized Signatory -- Nexbridge Holdings", "March 28, 2025",
              pdf.l_margin, sig_y)
    pdf.set_y(sig_y + 36)

    pdf.output(os.path.join(OUT, "bank_receipt.pdf"))
    print("  OK  bank_receipt.pdf")


# ── main ───────────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    print("Generating sample PDFs...")
    hospital_bill()
    legal_restaurant()
    meeting_minutes()
    payslip()
    bank_receipt()
    print("Done. Files written to:", OUT)
