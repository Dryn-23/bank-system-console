# generate_transactions_pdf.py
# Usage: python generate_transactions_pdf.py <AccountID> <OutputPath>
#
# Requirements: pip install reportlab pyodbc

import sys
import pyodbc
from datetime import datetime
from reportlab.lib.pagesizes import A4
from reportlab.lib import colors
from reportlab.lib.units import mm
from reportlab.platypus import (SimpleDocTemplate, Table, TableStyle,
                                 Paragraph, Spacer, HRFlowable)
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_CENTER, TA_RIGHT

# ── DB Connection ────────────────────────────────────────────────────────────
CONN_STR = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=DESKTOP-51HVDT7;"
    "DATABASE=BankSystem;"
    "Trusted_Connection=yes;"
    "TrustServerCertificate=yes;"
)

def fetch_data(account_id: int):
    conn = pyodbc.connect(CONN_STR)
    cur  = conn.cursor()

    # Account + owner info
    cur.execute("""
        SELECT a.AcountNumber, a.AccountType, a.Balance, a.[Status],
               c.FirstName + ' ' + c.LastName AS FullName,
               c.Email, c.Phone
        FROM   Accounts  a
        JOIN   Customers c ON c.CustomerID = a.CustomerID
        WHERE  a.AccountID = ?
    """, account_id)
    acc = cur.fetchone()

    # Transactions grouped by month
    cur.execute("""
        SELECT TransactionType, Amount, TracsactionDate
        FROM   Transactions
        WHERE  AccountID = ?
        ORDER  BY TracsactionDate DESC
    """, account_id)
    txs = cur.fetchall()

    # Monthly summary
    cur.execute("""
        SELECT FORMAT(TracsactionDate,'yyyy-MM') AS Mo,
               SUM(CASE WHEN TransactionType='Deposit'      THEN Amount ELSE 0 END),
               SUM(CASE WHEN TransactionType='Withdraw'     THEN Amount ELSE 0 END),
               SUM(CASE WHEN TransactionType='Transfer Out' THEN Amount ELSE 0 END)
        FROM   Transactions
        WHERE  AccountID = ?
        GROUP  BY FORMAT(TracsactionDate,'yyyy-MM')
        ORDER  BY Mo DESC
    """, account_id)
    summary = cur.fetchall()

    conn.close()
    return acc, txs, summary

def build_pdf(account_id: int, output_path: str):
    acc, txs, summary = fetch_data(account_id)
    if not acc:
        print(f"No account found for ID {account_id}", file=sys.stderr)
        sys.exit(1)

    acc_number, acc_type, balance, status, full_name, email, phone = acc

    doc    = SimpleDocTemplate(output_path, pagesize=A4,
                               leftMargin=15*mm, rightMargin=15*mm,
                               topMargin=15*mm, bottomMargin=15*mm)
    styles = getSampleStyleSheet()
    story  = []

    # ── Bank Header ──────────────────────────────────────────────────────────
    bank_style = ParagraphStyle('BankTitle', fontSize=20, textColor=colors.HexColor('#1a3c6e'),
                                 alignment=TA_CENTER, fontName='Helvetica-Bold')
    sub_style  = ParagraphStyle('BankSub',   fontSize=10, textColor=colors.grey,
                                 alignment=TA_CENTER)
    story.append(Paragraph("🏦  BankSystem", bank_style))
    story.append(Paragraph("Official Account Statement", sub_style))
    story.append(HRFlowable(width="100%", thickness=2, color=colors.HexColor('#1a3c6e')))
    story.append(Spacer(1, 6*mm))

    # ── Account Details ──────────────────────────────────────────────────────
    generated = datetime.now().strftime("%B %d, %Y  %I:%M %p")
    detail_data = [
        ["Account Holder", full_name,     "Generated",     generated],
        ["Account Number", acc_number,    "Email",         email or "—"],
        ["Account Type",   acc_type,      "Phone",         phone or "—"],
        ["Status",         status,        "Balance",       f"₱{balance:,.2f}"],
    ]
    dt = Table(detail_data, colWidths=[38*mm, 55*mm, 30*mm, 57*mm])
    dt.setStyle(TableStyle([
        ('BACKGROUND',  (0, 0), (-1, -1), colors.HexColor('#f0f4f8')),
        ('FONTNAME',    (0, 0), (0, -1),  'Helvetica-Bold'),
        ('FONTNAME',    (2, 0), (2, -1),  'Helvetica-Bold'),
        ('FONTSIZE',    (0, 0), (-1, -1), 9),
        ('ROWBACKGROUNDS', (0, 0), (-1, -1), [colors.HexColor('#f0f4f8'), colors.white]),
        ('BOX',         (0, 0), (-1, -1), 0.5, colors.HexColor('#cccccc')),
        ('INNERGRID',   (0, 0), (-1, -1), 0.5, colors.HexColor('#cccccc')),
        ('PADDING',     (0, 0), (-1, -1), 5),
        ('TEXTCOLOR',   (3, 3), (3, 3),   colors.HexColor('#1a6e3c')),  # balance green
        ('FONTNAME',    (3, 3), (3, 3),   'Helvetica-Bold'),
    ]))
    story.append(dt)
    story.append(Spacer(1, 6*mm))

    # ── Monthly Summary ──────────────────────────────────────────────────────
    story.append(Paragraph("Monthly Summary", styles['Heading2']))
    ms_header = [["Month", "Deposits", "Withdrawals", "Transfers Out", "Net"]]
    ms_rows   = []
    for mo, dep, wit, trf in summary:
        net = dep - wit - trf
        ms_rows.append([
            mo,
            f"₱{dep:,.2f}",
            f"₱{wit:,.2f}",
            f"₱{trf:,.2f}",
            f"₱{net:,.2f}"
        ])
    ms_data = ms_header + ms_rows if ms_rows else ms_header + [["No data", "", "", "", ""]]
    ms_table = Table(ms_data, colWidths=[30*mm, 35*mm, 35*mm, 40*mm, 40*mm])
    ms_table.setStyle(TableStyle([
        ('BACKGROUND',  (0, 0), (-1, 0),  colors.HexColor('#1a3c6e')),
        ('TEXTCOLOR',   (0, 0), (-1, 0),  colors.white),
        ('FONTNAME',    (0, 0), (-1, 0),  'Helvetica-Bold'),
        ('FONTSIZE',    (0, 0), (-1, -1), 9),
        ('ROWBACKGROUNDS', (0, 1), (-1, -1), [colors.white, colors.HexColor('#f0f4f8')]),
        ('BOX',         (0, 0), (-1, -1), 0.5, colors.HexColor('#cccccc')),
        ('INNERGRID',   (0, 0), (-1, -1), 0.5, colors.HexColor('#cccccc')),
        ('ALIGN',       (1, 0), (-1, -1), 'RIGHT'),
        ('PADDING',     (0, 0), (-1, -1), 5),
    ]))
    story.append(ms_table)
    story.append(Spacer(1, 6*mm))

    # ── Transaction History ──────────────────────────────────────────────────
    story.append(Paragraph("Transaction History", styles['Heading2']))
    tx_header = [["Date & Time", "Type", "Amount"]]
    tx_rows   = []
    for t_type, t_amt, t_date in txs:
        tx_rows.append([
            t_date.strftime("%Y-%m-%d  %H:%M") if t_date else "—",
            t_type,
            f"₱{t_amt:,.2f}"
        ])
    tx_data  = tx_header + tx_rows if tx_rows else tx_header + [["No transactions", "", ""]]
    tx_table = Table(tx_data, colWidths=[55*mm, 70*mm, 55*mm])
    tx_table.setStyle(TableStyle([
        ('BACKGROUND',  (0, 0), (-1, 0),  colors.HexColor('#1a3c6e')),
        ('TEXTCOLOR',   (0, 0), (-1, 0),  colors.white),
        ('FONTNAME',    (0, 0), (-1, 0),  'Helvetica-Bold'),
        ('FONTSIZE',    (0, 0), (-1, -1), 9),
        ('ROWBACKGROUNDS', (0, 1), (-1, -1), [colors.white, colors.HexColor('#f0f4f8')]),
        ('BOX',         (0, 0), (-1, -1), 0.5, colors.HexColor('#cccccc')),
        ('INNERGRID',   (0, 0), (-1, -1), 0.5, colors.HexColor('#cccccc')),
        ('ALIGN',       (2, 0), (2, -1),  'RIGHT'),
        ('PADDING',     (0, 0), (-1, -1), 5),
    ]))
    story.append(tx_table)
    story.append(Spacer(1, 8*mm))

    # ── Footer ───────────────────────────────────────────────────────────────
    story.append(HRFlowable(width="100%", thickness=1, color=colors.grey))
    footer_style = ParagraphStyle('Footer', fontSize=8, textColor=colors.grey,
                                   alignment=TA_CENTER)
    story.append(Paragraph(
        "This is a system-generated statement. For inquiries, contact your nearest branch.",
        footer_style))

    doc.build(story)
    print(f"PDF saved: {output_path}")

# ── Entry ────────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python generate_transactions_pdf.py <AccountID> <OutputPath>",
              file=sys.stderr)
        sys.exit(1)
    build_pdf(int(sys.argv[1]), sys.argv[2])
