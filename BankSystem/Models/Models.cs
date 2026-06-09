// Models/Models.cs
using System;

namespace BankSystem.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string UserName { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string PasswordSalt { get; set; } = "";
        public string Role { get; set; } = "";
        public int FailedLogins { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LastActivity { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Customer
    {
        public int CustomerID { get; set; }
        public int UserID { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Address { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime DateCreated { get; set; }
        public string FullName => $"{FirstName} {LastName}";
    }

    public class Account
    {
        public int AccountID { get; set; }
        public int CustomerID { get; set; }
        public string AccountNumber { get; set; } = "";
        public string AccountType { get; set; } = "";
        public decimal Balance { get; set; }
        public string Status { get; set; } = "";
        public decimal InterestRate { get; set; }
        public DateTime? LastInterest { get; set; }
        public string OwnerName { get; set; } = "";   // joined from Customers
    }

    public class Transaction
    {
        public int TransactionID { get; set; }
        public int AccountID { get; set; }
        public string TransactionType { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }

    public class PendingTransfer
    {
        public int PendingID { get; set; }
        public int FromAccountID { get; set; }
        public string FromAccountNumber { get; set; } = "";
        public string FromOwner { get; set; } = "";
        public int ToAccountID { get; set; }
        public string ToAccountNumber { get; set; } = "";
        public string ToOwner { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime RequestedAt { get; set; }
        public string Status { get; set; } = "";
    }

    public class Loan
    {
        public int LoanID { get; set; }
        public int AccountID { get; set; }
        public decimal Principal { get; set; }
        public decimal InterestRate { get; set; }
        public decimal MonthlyPayment { get; set; }
        public decimal TotalDue { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal RemainingBalance => TotalDue - AmountPaid;
        public DateTime StartDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "";
    }

    public class AuditEntry
    {
        public int LogID { get; set; }
        public int? UserID { get; set; }
        public string UserName { get; set; } = "";
        public string Action { get; set; } = "";
        public string? Details { get; set; }
        public DateTime LoggedAt { get; set; }
    }

    public class Session
    {
        public int UserID { get; set; }
        public string UserName { get; set; } = "";
        public string Role { get; set; } = "";
        public int AccountID { get; set; } = -1;   // Customer only
        public DateTime LoginTime { get; set; } = DateTime.Now;
        public DateTime LastActivity { get; set; } = DateTime.Now;

        public static readonly int TimeoutMinutes = 15;

        public bool IsTimedOut =>
            (DateTime.Now - LastActivity).TotalMinutes >= TimeoutMinutes;

        public void Touch() => LastActivity = DateTime.Now;
        public bool IsCustomer => Role == "Customer";
        public bool IsTeller => Role == "Teller";
    }
}
