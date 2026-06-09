-- ================================================================
--  BankSystem — Full Database Setup Script
--  Run this ONCE against your BankSystem database.
--  Safe to re-run (uses IF NOT EXISTS / IF COL_LENGTH checks).
-- ================================================================

USE BankSystem;
GO

-- ────────────────────────────────────────────────────────────────
--  1. USERS — add salt, failed-login tracking, lock, audit
-- ────────────────────────────────────────────────────────────────
IF COL_LENGTH('Users','PasswordSalt')  IS NULL ALTER TABLE Users ADD PasswordSalt   NVARCHAR(64)  NULL;
IF COL_LENGTH('Users','FailedLogins')  IS NULL ALTER TABLE Users ADD FailedLogins   INT           NOT NULL DEFAULT 0;
IF COL_LENGTH('Users','IsLocked')      IS NULL ALTER TABLE Users ADD IsLocked       BIT           NOT NULL DEFAULT 0;
IF COL_LENGTH('Users','LastActivity')  IS NULL ALTER TABLE Users ADD LastActivity   DATETIME      NULL;
IF COL_LENGTH('Users','CreatedAt')     IS NULL ALTER TABLE Users ADD CreatedAt      DATETIME      NOT NULL DEFAULT GETDATE();
GO

-- ────────────────────────────────────────────────────────────────
--  2. CUSTOMERS — UserID link (already in original ALTER TABLE)
-- ────────────────────────────────────────────────────────────────
IF COL_LENGTH('Customers','UserID') IS NULL
    ALTER TABLE Customers ADD UserID INT NULL REFERENCES Users(UserID);
GO

-- ────────────────────────────────────────────────────────────────
--  3. ACCOUNTS — freeze support, interest, pending flag
-- ────────────────────────────────────────────────────────────────
-- Status already exists; 'Frozen' is a new allowed value (no constraint change needed)
IF COL_LENGTH('Accounts','InterestRate')  IS NULL ALTER TABLE Accounts ADD InterestRate  DECIMAL(5,4) NOT NULL DEFAULT 0.0200;
IF COL_LENGTH('Accounts','LastInterest')  IS NULL ALTER TABLE Accounts ADD LastInterest  DATETIME     NULL;
GO

-- ────────────────────────────────────────────────────────────────
--  4. TRANSACTIONS — no schema change needed; TracsactionDate
--     already defaults to GETDATE()
-- ────────────────────────────────────────────────────────────────

-- ────────────────────────────────────────────────────────────────
--  5. PENDING TRANSFERS — teller-approval queue
-- ────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PendingTransfers')
CREATE TABLE PendingTransfers (
    PendingID       INT          IDENTITY(1,1) PRIMARY KEY,
    FromAccountID   INT          NOT NULL REFERENCES Accounts(AccountID),
    ToAccountID     INT          NOT NULL REFERENCES Accounts(AccountID),
    Amount          DECIMAL(18,2) NOT NULL,
    RequestedAt     DATETIME     NOT NULL DEFAULT GETDATE(),
    ApprovedAt      DATETIME     NULL,
    ApprovedByUserID INT         NULL REFERENCES Users(UserID),
    [Status]        NVARCHAR(20) NOT NULL DEFAULT 'Pending'  -- Pending | Approved | Rejected
);
GO

-- ────────────────────────────────────────────────────────────────
--  6. AUDIT LOG
-- ────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AuditLog')
CREATE TABLE AuditLog (
    LogID       INT           IDENTITY(1,1) PRIMARY KEY,
    UserID      INT           NULL REFERENCES Users(UserID),
    Action      NVARCHAR(100) NOT NULL,
    Details     NVARCHAR(500) NULL,
    LoggedAt    DATETIME      NOT NULL DEFAULT GETDATE(),
    IPAddress   NVARCHAR(50)  NULL
);
GO

-- ────────────────────────────────────────────────────────────────
--  7. LOANS
-- ────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Loans')
CREATE TABLE Loans (
    LoanID          INT           IDENTITY(1,1) PRIMARY KEY,
    AccountID       INT           NOT NULL REFERENCES Accounts(AccountID),
    Principal       DECIMAL(18,2) NOT NULL,
    InterestRate    DECIMAL(5,4)  NOT NULL DEFAULT 0.0500,   -- 5% monthly
    MonthlyPayment  DECIMAL(18,2) NOT NULL,
    TotalDue        DECIMAL(18,2) NOT NULL,
    AmountPaid      DECIMAL(18,2) NOT NULL DEFAULT 0,
    StartDate       DATETIME      NOT NULL DEFAULT GETDATE(),
    DueDate         DATETIME      NOT NULL,
    [Status]        NVARCHAR(20)  NOT NULL DEFAULT 'Active'  -- Active | Paid | Defaulted
);
GO

-- ────────────────────────────────────────────────────────────────
--  8. BILL PAYMENTS
-- ────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BillPayments')
CREATE TABLE BillPayments (
    BillPaymentID   INT           IDENTITY(1,1) PRIMARY KEY,
    AccountID       INT           NOT NULL REFERENCES Accounts(AccountID),
    Biller          NVARCHAR(50)  NOT NULL,    -- e.g. Meralco, PLDT, Globe
    ReferenceNo     NVARCHAR(50)  NOT NULL,
    Amount          DECIMAL(18,2) NOT NULL,
    PaidAt          DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

-- ────────────────────────────────────────────────────────────────
--  9. OTP TABLE (login simulation)
-- ────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OTPCodes')
CREATE TABLE OTPCodes (
    OTPID       INT          IDENTITY(1,1) PRIMARY KEY,
    UserID      INT          NOT NULL REFERENCES Users(UserID),
    Code        NVARCHAR(6)  NOT NULL,
    ExpiresAt   DATETIME     NOT NULL,
    IsUsed      BIT          NOT NULL DEFAULT 0
);
GO

PRINT 'Database setup complete.';
