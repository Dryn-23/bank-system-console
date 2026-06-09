// Services/AccountService.cs
using BankSystem.Helpers;
using BankSystem.Models;
using BankSystem.Repositories;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace BankSystem.Services
{
    public class AccountService
    {
        private readonly AccountRepository _accounts = new();
        private readonly TransactionRepository _txRepo = new();
        private readonly AuditRepository _audit = new();

        public const decimal TransferFee = 10m;   // ₱10 per transfer

        // ── Deposit ──────────────────────────────────────────────────
        public (bool ok, string msg) Deposit(int accountID, decimal amount, int userID)
        {
            try
            {
                using var conn = Database.GetConnection();
                using var tx = conn.BeginTransaction();

                var acc = _accounts.GetByID(accountID);
                if (acc == null || acc.Status != "Active")
                    return (false, "Account not found or inactive.");

                _accounts.UpdateBalance(accountID, amount, conn, tx);
                _txRepo.Log(accountID, "Deposit", amount, conn, tx);
                tx.Commit();
                _audit.Log(userID, "DEPOSIT", $"AccID={accountID} Amount=₱{amount:N2}");
                return (true, $"Deposited ₱{amount:N2} successfully. New balance: ₱{acc.Balance + amount:N2}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ── Withdraw ─────────────────────────────────────────────────
        public (bool ok, string msg) Withdraw(int accountID, decimal amount, int userID)
        {
            try
            {
                using var conn = Database.GetConnection();
                using var tx = conn.BeginTransaction();

                decimal balance = _accounts.GetBalance(accountID, conn, tx);
                if (balance < amount)
                    return (false, $"Insufficient funds. Available: ₱{balance:N2}");

                _accounts.UpdateBalance(accountID, -amount, conn, tx);
                _txRepo.Log(accountID, "Withdraw", amount, conn, tx);
                tx.Commit();
                _audit.Log(userID, "WITHDRAW", $"AccID={accountID} Amount=₱{amount:N2}");
                return (true, $"Withdrew ₱{amount:N2} successfully. New balance: ₱{balance - amount:N2}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ── Immediate Transfer (with ₱10 fee) ───────────────────────
        public (bool ok, string msg) Transfer(int fromAccountID, string destAccNumber,
                                               decimal amount, int userID)
        {
            try
            {
                var dest = _accounts.GetByAccountNumber(destAccNumber);
                if (dest == null || dest.Status != "Active")
                    return (false, "Destination account not found or inactive.");
                if (dest.AccountID == fromAccountID)
                    return (false, "Cannot transfer to your own account.");

                decimal total = amount + TransferFee;

                using var conn = Database.GetConnection();
                using var tx = conn.BeginTransaction();

                decimal balance = _accounts.GetBalance(fromAccountID, conn, tx);
                if (balance < total)
                    return (false, $"Insufficient funds. Need ₱{total:N2} (includes ₱{TransferFee:N2} fee). Available: ₱{balance:N2}");

                _accounts.UpdateBalance(fromAccountID, -total, conn, tx);
                _accounts.UpdateBalance(dest.AccountID, amount, conn, tx);

                // Log Trasfer table
                using var tCmd = new SqlCommand(@"
                    INSERT INTO Trasfer (FromAccountID, ToAccountID, Amount)
                    VALUES (@F, @T, @A)", conn, tx);
                tCmd.Parameters.AddWithValue("@F", fromAccountID);
                tCmd.Parameters.AddWithValue("@T", dest.AccountID);
                tCmd.Parameters.AddWithValue("@A", amount);
                tCmd.ExecuteNonQuery();

                _txRepo.Log(fromAccountID, "Transfer Out", amount, conn, tx);
                _txRepo.Log(dest.AccountID, "Transfer In", amount, conn, tx);
                tx.Commit();
                _audit.Log(userID, "TRANSFER", $"From={fromAccountID} To={dest.AccountID} Amount=₱{amount:N2} Fee=₱{TransferFee:N2}");
                return (true, $"Transferred ₱{amount:N2} to {destAccNumber}. Fee: ₱{TransferFee:N2}. New balance: ₱{balance - total:N2}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ── Pending Transfer (requires teller approval) ──────────────
        public (bool ok, string msg) RequestPendingTransfer(int fromAccountID,
                                                             string destAccNumber,
                                                             decimal amount, int userID)
        {
            try
            {
                var dest = _accounts.GetByAccountNumber(destAccNumber);
                if (dest == null || dest.Status != "Active")
                    return (false, "Destination account not found or inactive.");
                if (dest.AccountID == fromAccountID)
                    return (false, "Cannot transfer to your own account.");

                var pending = new PendingTransferRepository();
                pending.Insert(fromAccountID, dest.AccountID, amount);
                _audit.Log(userID, "PENDING_TRANSFER_REQUEST",
                    $"From={fromAccountID} To={dest.AccountID} Amount=₱{amount:N2}");
                return (true, $"Transfer request of ₱{amount:N2} submitted for teller approval.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ── Apply interest ────────────────────────────────────────────
        public void ApplyInterest()
        {
            _accounts.ApplyMonthlyInterest();
            _audit.Log(null, "INTEREST_APPLIED", "Monthly interest batch run");
        }

        public Account? GetAccount(int accountID) => _accounts.GetByID(accountID);

        public List<Account> GetAllAccounts() => _accounts.GetAll();

        public List<Account> GetTopAccounts(int n = 10) => _accounts.GetTopByBalance(n);

        public List<Account> GetByCustomer(int customerID) => _accounts.GetByCustomerID(customerID);

        public void SetStatus(int accountID, string status, int callerUserID)
        {
            _accounts.SetStatus(accountID, status);
            _audit.Log(callerUserID, $"ACCOUNT_{status.ToUpper()}", $"AccountID={accountID}");
        }

        // ── Switch active account for multi-account customers ─────────
        public List<Account> GetAccountsForUser(int userID)
        {
            var cust = new CustomerRepository().GetByUserID(userID);
            if (cust == null) return new();
            return _accounts.GetByCustomerID(cust.CustomerID);
        }
    }
}
