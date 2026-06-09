# 🏦 BankSystem v2.0 — Full-Featured Console Banking App

## What's New vs. v1.0

| Category | Feature | Status |
|---|---|---|
| 🔐 Security | **Salted SHA-256** password hashing | ✅ |
| 🔐 Security | **Login lockout** after 5 failed attempts | ✅ |
| 🔐 Security | **OTP simulation** (console 2FA) | ✅ |
| 🔐 Security | **Session timeout** (15 min inactivity) | ✅ |
| 🔐 Security | Role separation enforced at service layer | ✅ |
| 💰 Banking | **Deposit / Withdraw / Instant Transfer** | ✅ |
| 💰 Banking | **Transfer fee** (₱10 per transfer) | ✅ |
| 💰 Banking | **Pending Transfers** (teller approval) | ✅ |
| 💰 Banking | **Multiple accounts** per customer | ✅ |
| 💰 Banking | **Switch active account** | ✅ |
| 💰 Banking | **Monthly interest** for Savings | ✅ |
| 💰 Banking | **Loan system** (apply, repay, status) | ✅ |
| 💰 Banking | **Bill payment** (10 billers) | ✅ |
| 📊 Reports | **Mini Statement** (last 5 transactions) | ✅ |
| 📊 Reports | **Monthly summary** (dep/wit/trf/net) | ✅ |
| 📊 Reports | **Spending breakdown** per account | ✅ |
| 📊 Reports | **Top 10 customers** by balance | ✅ |
| 📊 Reports | **PDF statement** (reportlab) | ✅ |
| 🧑‍💼 Teller | **Search** customer by name/email/acc# | ✅ |
| 🧑‍💼 Teller | **Edit** customer info | ✅ |
| 🧑‍💼 Teller | **Reset** user password | ✅ |
| 🧑‍💼 Teller | **Lock / Unlock** user | ✅ |
| 🧑‍💼 Teller | **Freeze / Unfreeze** account | ✅ |
| 🧑‍💼 Teller | **Approve / Reject** pending transfers | ✅ |
| 🧑‍💼 Teller | **Audit log** (all actions, 50 entries) | ✅ |
| 🎨 UX | Paginated transaction history | ✅ |
| 🎨 UX | `GetDecimalInput`, `GetIntInput` helpers | ✅ |
| 🎨 UX | Confirm prompts for destructive actions | ✅ |
| 🎨 UX | Color-coded output (green=credit, red=debit) | ✅ |
| 🏗️ Design | OOP: Services, Repositories, Models, UI | ✅ |
| 🏗️ Design | Repository pattern (DB logic separated) | ✅ |
| 🏗️ Design | DTO/Model classes for all entities | ✅ |

---

## 📂 Project Structure

```
BankSystem/
├── Program.cs                          ← Entry point, main loop
├── BankSystem.csproj                   ← Project file
├── DatabaseSetup.sql                   ← Run once on SQL Server
├── generate_transactions_pdf.py        ← PDF generation script
│
├── Models/
│   └── Models.cs                       ← User, Customer, Account, Session, Loan, etc.
│
├── Repositories/                       ← All SQL queries live here
│   ├── Database.cs                     ← Connection string
│   ├── UserRepository.cs
│   ├── CustomerRepository.cs
│   ├── AccountRepository.cs
│   └── OtherRepositories.cs            ← Transaction, PendingTransfer, Loan, Bill, Audit
│
├── Services/                           ← Business logic
│   ├── AuthService.cs                  ← Register, Login (OTP + lockout), timeout
│   ├── AccountService.cs               ← Deposit, Withdraw, Transfer, Interest
│   └── LoanService.cs                  ← Loan + BillPaymentService
│
├── Helpers/
│   ├── SecurityHelper.cs               ← Salt+hash, OTP generation
│   └── InputHelper.cs                  ← GetDecimalInput, ConsoleHelper, etc.
│
└── UI/
    ├── CustomerUI.cs                   ← Full customer menu (15 options)
    └── TellerUI.cs                     ← Full teller menu (15 options)
```

---

## ⚙️ Setup

### 1. Database
Run `DatabaseSetup.sql` once on your SQL Server:
```sql
-- In SSMS: open DatabaseSetup.sql, select BankSystem database, Execute
```

### 2. Build & Run
```bash
cd BankSystem
dotnet run
```

### 3. PDF Statements (optional)
```bash
pip install reportlab pyodbc
```
Make sure `generate_transactions_pdf.py` is in the same folder as the compiled `.exe`.

---

## 🔐 Security Architecture

### Password Hashing
```
Old (v1):  SHA256(password)               ← weak, no salt
New (v2):  SHA256(password + random_salt) ← per-user salt stored in DB
```
Legacy users (no salt) are still supported at login.

### Login Flow
```
Username + Password
    → Check DB → Wrong? Increment FailedLogins
    → 5 fails? Lock account → Teller must unlock
    → Correct? Generate 6-digit OTP → Display in console (simulate SMS)
    → User enters OTP → Validated → Session created
```

### Session Timeout
- Every menu interaction calls `session.Touch()` to reset the timer.
- After **15 minutes** of inactivity, user is automatically logged out.

---

## 💰 Transfer Fee
Every **instant transfer** deducts **₱10** from the sender in addition to the transfer amount.  
**Pending transfers** (routed through teller approval) do not charge a fee until approved.

## 💳 Loan Terms
- Interest: **5% flat per month**
- Default term: **12 months**
- Only **one active loan** per account at a time

## 🏦 Interest (Savings Accounts)
- Rate: **2% per month** (stored per-account in `Accounts.InterestRate`)
- Applied manually by Teller via menu option 11
- Only applies if ≥ 30 days since last application
