// Repositories/AccountRepository.cs
using Microsoft.Data.SqlClient;
using BankSystem.Models;
using System;
using System.Collections.Generic;

namespace BankSystem.Repositories
{
    public class AccountRepository
    {
        // ── Insert new account; returns AccountID ─────────────────────
        public int Insert(int customerID, string accountNumber, string accountType,
                          SqlConnection conn, SqlTransaction tx)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO Accounts (CustomerID, AcountNumber, AccountType, Balance, [Status])
                OUTPUT INSERTED.AccountID
                VALUES (@C, @N, @T, 0, 'Active')", conn, tx);
            cmd.Parameters.AddWithValue("@C", customerID);
            cmd.Parameters.AddWithValue("@N", accountNumber);
            cmd.Parameters.AddWithValue("@T", accountType);
            return (int)cmd.ExecuteScalar()!;
        }

        public Account? GetByID(int accountID)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT a.AccountID, a.CustomerID, a.AcountNumber, a.AccountType,
                       a.Balance, a.[Status], a.InterestRate, a.LastInterest,
                       c.FirstName + ' ' + c.LastName AS OwnerName
                FROM   Accounts a
                JOIN   Customers c ON c.CustomerID = a.CustomerID
                WHERE  a.AccountID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", accountID);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return MapAccount(r);
        }

        public Account? GetByAccountNumber(string accNumber)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT a.AccountID, a.CustomerID, a.AcountNumber, a.AccountType,
                       a.Balance, a.[Status], a.InterestRate, a.LastInterest,
                       c.FirstName + ' ' + c.LastName AS OwnerName
                FROM   Accounts a
                JOIN   Customers c ON c.CustomerID = a.CustomerID
                WHERE  a.AcountNumber = @N", conn);
            cmd.Parameters.AddWithValue("@N", accNumber);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return MapAccount(r);
        }

        // Resolve active AccountID for a user (for login)
        public int? GetActiveAccountIDForUser(int userID, SqlConnection conn)
        {
            using var cmd = new SqlCommand(@"
                SELECT a.AccountID
                FROM   Accounts  a
                JOIN   Customers c ON c.CustomerID = a.CustomerID
                WHERE  c.UserID   = @UID AND a.[Status] = 'Active'", conn);
            cmd.Parameters.AddWithValue("@UID", userID);
            var r = cmd.ExecuteScalar();
            return r == null ? null : (int)r;
        }

        // All accounts for a customer (multiple accounts support)
        public List<Account> GetByCustomerID(int customerID)
        {
            var list = new List<Account>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT a.AccountID, a.CustomerID, a.AcountNumber, a.AccountType,
                       a.Balance, a.[Status], a.InterestRate, a.LastInterest,
                       c.FirstName + ' ' + c.LastName AS OwnerName
                FROM   Accounts a
                JOIN   Customers c ON c.CustomerID = a.CustomerID
                WHERE  a.CustomerID = @C ORDER BY a.AccountID", conn);
            cmd.Parameters.AddWithValue("@C", customerID);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapAccount(r));
            return list;
        }

        public List<Account> GetAll()
        {
            var list = new List<Account>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT a.AccountID, a.CustomerID, a.AcountNumber, a.AccountType,
                       a.Balance, a.[Status], a.InterestRate, a.LastInterest,
                       c.FirstName + ' ' + c.LastName AS OwnerName
                FROM   Accounts a
                JOIN   Customers c ON c.CustomerID = a.CustomerID
                ORDER  BY a.AccountID", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapAccount(r));
            return list;
        }

        public void SetStatus(int accountID, string status)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(
                "UPDATE Accounts SET [Status] = @S WHERE AccountID = @ID", conn);
            cmd.Parameters.AddWithValue("@S", status);
            cmd.Parameters.AddWithValue("@ID", accountID);
            cmd.ExecuteNonQuery();
        }

        public void UpdateBalance(int accountID, decimal delta,
                                  SqlConnection conn, SqlTransaction tx)
        {
            using var cmd = new SqlCommand(@"
                UPDATE Accounts SET Balance = Balance + @D WHERE AccountID = @ID", conn, tx);
            cmd.Parameters.AddWithValue("@D", delta);
            cmd.Parameters.AddWithValue("@ID", accountID);
            cmd.ExecuteNonQuery();
        }

        public decimal GetBalance(int accountID, SqlConnection conn, SqlTransaction tx)
        {
            using var cmd = new SqlCommand(
                "SELECT Balance FROM Accounts WHERE AccountID = @ID", conn, tx);
            cmd.Parameters.AddWithValue("@ID", accountID);
            return Convert.ToDecimal(cmd.ExecuteScalar());
        }

        // Apply monthly interest to all eligible Savings accounts
        public void ApplyMonthlyInterest()
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE Accounts
                SET    Balance      = Balance + (Balance * InterestRate),
                       LastInterest = GETDATE()
                WHERE  AccountType  = 'Savings'
                  AND  [Status]     = 'Active'
                  AND  (LastInterest IS NULL
                        OR DATEDIFF(DAY, LastInterest, GETDATE()) >= 30)", conn);
            cmd.ExecuteNonQuery();
        }

        // Top customers by balance (for teller dashboard)
        public List<Account> GetTopByBalance(int top = 10)
        {
            var list = new List<Account>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand($@"
                SELECT TOP {top} a.AccountID, a.CustomerID, a.AcountNumber, a.AccountType,
                       a.Balance, a.[Status], a.InterestRate, a.LastInterest,
                       c.FirstName + ' ' + c.LastName AS OwnerName
                FROM   Accounts a
                JOIN   Customers c ON c.CustomerID = a.CustomerID
                WHERE  a.[Status] = 'Active'
                ORDER  BY a.Balance DESC", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapAccount(r));
            return list;
        }

        private static Account MapAccount(SqlDataReader r) => new()
        {
            AccountID     = r.GetInt32(r.GetOrdinal("AccountID")),
            CustomerID    = r.GetInt32(r.GetOrdinal("CustomerID")),
            AccountNumber = r.GetString(r.GetOrdinal("AcountNumber")),
            AccountType   = r.GetString(r.GetOrdinal("AccountType")),
            Balance       = r.GetDecimal(r.GetOrdinal("Balance")),
            Status        = r.GetString(r.GetOrdinal("Status")),
            InterestRate  = r.IsDBNull(r.GetOrdinal("InterestRate")) ? 0.02m : r.GetDecimal(r.GetOrdinal("InterestRate")),
            LastInterest  = r.IsDBNull(r.GetOrdinal("LastInterest")) ? null : r.GetDateTime(r.GetOrdinal("LastInterest")),
            OwnerName     = r.GetString(r.GetOrdinal("OwnerName"))
        };
    }
}
