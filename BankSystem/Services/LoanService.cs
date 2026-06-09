// Services/LoanService.cs
using BankSystem.Helpers;
using BankSystem.Models;
using BankSystem.Repositories;
using System;
using System.Collections.Generic;

namespace BankSystem.Services
{
    public class LoanService
    {
        private readonly LoanRepository _loans = new();
        private readonly AccountRepository _accounts = new();
        private readonly TransactionRepository _txRepo = new();
        private readonly AuditRepository _audit = new();

        public const decimal LoanInterestRate = 0.05m;   // 5% monthly flat
        public const int DefaultTermMonths = 12;

        // ── Apply for a loan ─────────────────────────────────────────
        public (bool ok, string msg) Apply(int accountID, decimal principal, int userID)
        {
            try
            {
                // Only one active loan at a time
                var existing = _loans.GetActiveLoan(accountID);
                if (existing != null)
                    return (false, $"You already have an active loan of ₱{existing.Principal:N2}. Settle it first.");

                decimal total = principal + (principal * LoanInterestRate * DefaultTermMonths);
                decimal monthly = total / DefaultTermMonths;
                DateTime due = DateTime.Now.AddMonths(DefaultTermMonths);

                // Credit the loan amount to the account
                using var conn = Database.GetConnection();
                using var tx = conn.BeginTransaction();
                _accounts.UpdateBalance(accountID, principal, conn, tx);
                _txRepo.Log(accountID, "Loan Disbursed", principal, conn, tx);
                tx.Commit();

                _loans.Insert(accountID, principal, LoanInterestRate, monthly, total, due);
                _audit.Log(userID, "LOAN_APPLY", $"AccID={accountID} Principal=₱{principal:N2}");

                return (true, $"Loan of ₱{principal:N2} approved!\n" +
                              $"  Total Due    : ₱{total:N2}\n" +
                              $"  Monthly Pmt  : ₱{monthly:N2}\n" +
                              $"  Due Date     : {due:yyyy-MM-dd}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ── Make a loan payment ───────────────────────────────────────
        public (bool ok, string msg) Pay(int accountID, decimal amount, int userID)
        {
            try
            {
                var loan = _loans.GetActiveLoan(accountID);
                if (loan == null) return (false, "No active loan found.");

                using var conn = Database.GetConnection();
                using var tx = conn.BeginTransaction();

                decimal balance = _accounts.GetBalance(accountID, conn, tx);
                if (balance < amount)
                    return (false, $"Insufficient balance. Available: ₱{balance:N2}");

                _accounts.UpdateBalance(accountID, -amount, conn, tx);
                _txRepo.Log(accountID, "Loan Payment", amount, conn, tx);
                tx.Commit();

                _loans.MakePayment(loan.LoanID, amount);
                _audit.Log(userID, "LOAN_PAYMENT", $"LoanID={loan.LoanID} Amount=₱{amount:N2}");

                decimal remaining = loan.RemainingBalance - amount;
                return (true, remaining <= 0
                    ? "Loan fully paid off! Congratulations!"
                    : $"Payment of ₱{amount:N2} recorded. Remaining: ₱{remaining:N2}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public List<Loan> GetLoans(int accountID) => _loans.GetByAccount(accountID);
        public Loan? GetActiveLoan(int accountID) => _loans.GetActiveLoan(accountID);
    }

    public class BillPaymentService
    {
        private readonly BillPaymentRepository _bills = new();
        private readonly AccountRepository _accounts = new();
        private readonly TransactionRepository _txRepo = new();
        private readonly AuditRepository _audit = new();

        public static readonly string[] Billers =
        {
            "Meralco", "Manila Water", "Maynilad", "PLDT", "Globe",
            "Smart", "Sky Cable", "SSS", "PhilHealth", "Pag-IBIG"
        };

        public (bool ok, string msg) Pay(int accountID, string biller, string refNo,
                                         decimal amount, int userID)
        {
            try
            {
                using var conn = Database.GetConnection();
                using var tx = conn.BeginTransaction();

                decimal balance = _accounts.GetBalance(accountID, conn, tx);
                if (balance < amount)
                    return (false, $"Insufficient balance. Available: ₱{balance:N2}");

                _accounts.UpdateBalance(accountID, -amount, conn, tx);
                _txRepo.Log(accountID, $"Bill-{biller}", amount, conn, tx);
                tx.Commit();

                _bills.Insert(accountID, biller, refNo, amount);
                _audit.Log(userID, "BILL_PAYMENT",
                    $"AccID={accountID} Biller={biller} Ref={refNo} Amount=₱{amount:N2}");

                return (true, $"Bill payment of ₱{amount:N2} to {biller} successful!\n  Reference: {refNo}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public List<(string Biller, string RefNo, decimal Amount, DateTime PaidAt)>
            GetHistory(int accountID) => _bills.GetByAccount(accountID);
    }
}
