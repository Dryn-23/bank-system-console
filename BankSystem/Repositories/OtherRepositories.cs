// Repositories/TransactionRepository.cs
using Microsoft.Data.SqlClient;
using BankSystem.Models;
using System;
using System.Collections.Generic;

namespace BankSystem.Repositories
{
    public class TransactionRepository
    {
        public void Log(int accountID, string type, decimal amount,
                        SqlConnection conn, SqlTransaction? tx = null)
        {
            const string sql = @"
                INSERT INTO Transactions (AccountID, TransactionType, Amount)
                VALUES (@A, @T, @M)";
            using var cmd = tx != null ? new SqlCommand(sql, conn, tx) : new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@A", accountID);
            cmd.Parameters.AddWithValue("@T", type);
            cmd.Parameters.AddWithValue("@M", amount);
            cmd.ExecuteNonQuery();
        }

        public List<Transaction> GetByAccount(int accountID, int limit = 0)
        {
            var list = new List<Transaction>();
            using var conn = Database.GetConnection();
            string top = limit > 0 ? $"TOP {limit}" : "";
            using var cmd = new SqlCommand($@"
                SELECT {top} TransactionID, AccountID, TransactionType, Amount, TracsactionDate
                FROM   Transactions
                WHERE  AccountID = @A
                ORDER  BY TracsactionDate DESC", conn);
            cmd.Parameters.AddWithValue("@A", accountID);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new Transaction
                {
                    TransactionID   = r.GetInt32(0),
                    AccountID       = r.GetInt32(1),
                    TransactionType = r.GetString(2),
                    Amount          = r.GetDecimal(3),
                    Date            = r.GetDateTime(4)
                });
            return list;
        }

        // Monthly summary for a given account
        public List<(string Month, decimal Deposits, decimal Withdrawals, decimal Transfers)>
            GetMonthlySummary(int accountID)
        {
            var list = new List<(string, decimal, decimal, decimal)>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT FORMAT(TracsactionDate,'yyyy-MM') AS Mo,
                       SUM(CASE WHEN TransactionType='Deposit'      THEN Amount ELSE 0 END) AS Dep,
                       SUM(CASE WHEN TransactionType='Withdraw'     THEN Amount ELSE 0 END) AS Wit,
                       SUM(CASE WHEN TransactionType='Transfer Out' THEN Amount ELSE 0 END) AS Trf
                FROM   Transactions
                WHERE  AccountID = @A
                GROUP  BY FORMAT(TracsactionDate,'yyyy-MM')
                ORDER  BY Mo DESC", conn);
            cmd.Parameters.AddWithValue("@A", accountID);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetString(0), r.GetDecimal(1), r.GetDecimal(2), r.GetDecimal(3)));
            return list;
        }
    }

    public class PendingTransferRepository
    {
        public void Insert(int fromID, int toID, decimal amount)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO PendingTransfers (FromAccountID, ToAccountID, Amount)
                VALUES (@F, @T, @A)", conn);
            cmd.Parameters.AddWithValue("@F", fromID);
            cmd.Parameters.AddWithValue("@T", toID);
            cmd.Parameters.AddWithValue("@A", amount);
            cmd.ExecuteNonQuery();
        }

        public List<PendingTransfer> GetPending()
        {
            var list = new List<PendingTransfer>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT pt.PendingID, pt.FromAccountID, af.AcountNumber AS FNum,
                       cf.FirstName+' '+cf.LastName AS FOwner,
                       pt.ToAccountID, at2.AcountNumber AS TNum,
                       ct.FirstName+' '+ct.LastName AS TOwner,
                       pt.Amount, pt.RequestedAt, pt.[Status]
                FROM   PendingTransfers pt
                JOIN   Accounts  af ON af.AccountID  = pt.FromAccountID
                JOIN   Customers cf ON cf.CustomerID = af.CustomerID
                JOIN   Accounts  at2 ON at2.AccountID = pt.ToAccountID
                JOIN   Customers ct ON ct.CustomerID = at2.CustomerID
                WHERE  pt.[Status] = 'Pending'
                ORDER  BY pt.RequestedAt", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new PendingTransfer
                {
                    PendingID         = r.GetInt32(0),
                    FromAccountID     = r.GetInt32(1),
                    FromAccountNumber = r.GetString(2),
                    FromOwner         = r.GetString(3),
                    ToAccountID       = r.GetInt32(4),
                    ToAccountNumber   = r.GetString(5),
                    ToOwner           = r.GetString(6),
                    Amount            = r.GetDecimal(7),
                    RequestedAt       = r.GetDateTime(8),
                    Status            = r.GetString(9)
                });
            return list;
        }

        public PendingTransfer? GetByID(int pendingID)
        {
            var list = new List<PendingTransfer>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT pt.PendingID, pt.FromAccountID, af.AcountNumber,
                       cf.FirstName+' '+cf.LastName,
                       pt.ToAccountID, at2.AcountNumber,
                       ct.FirstName+' '+ct.LastName,
                       pt.Amount, pt.RequestedAt, pt.[Status]
                FROM   PendingTransfers pt
                JOIN   Accounts  af ON af.AccountID  = pt.FromAccountID
                JOIN   Customers cf ON cf.CustomerID = af.CustomerID
                JOIN   Accounts  at2 ON at2.AccountID = pt.ToAccountID
                JOIN   Customers ct ON ct.CustomerID = at2.CustomerID
                WHERE  pt.PendingID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", pendingID);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new PendingTransfer
            {
                PendingID         = r.GetInt32(0),
                FromAccountID     = r.GetInt32(1),
                FromAccountNumber = r.GetString(2),
                FromOwner         = r.GetString(3),
                ToAccountID       = r.GetInt32(4),
                ToAccountNumber   = r.GetString(5),
                ToOwner           = r.GetString(6),
                Amount            = r.GetDecimal(7),
                RequestedAt       = r.GetDateTime(8),
                Status            = r.GetString(9)
            };
        }

        public void SetStatus(int pendingID, string status, int approverID)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE PendingTransfers
                SET [Status] = @S, ApprovedAt = GETDATE(), ApprovedByUserID = @U
                WHERE PendingID = @ID", conn);
            cmd.Parameters.AddWithValue("@S", status);
            cmd.Parameters.AddWithValue("@U", approverID);
            cmd.Parameters.AddWithValue("@ID", pendingID);
            cmd.ExecuteNonQuery();
        }
    }

    public class LoanRepository
    {
        public void Insert(int accountID, decimal principal, decimal rate,
                           decimal monthly, decimal total, DateTime due)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO Loans (AccountID, Principal, InterestRate,
                                   MonthlyPayment, TotalDue, DueDate)
                VALUES (@A, @P, @R, @M, @T, @D)", conn);
            cmd.Parameters.AddWithValue("@A", accountID);
            cmd.Parameters.AddWithValue("@P", principal);
            cmd.Parameters.AddWithValue("@R", rate);
            cmd.Parameters.AddWithValue("@M", monthly);
            cmd.Parameters.AddWithValue("@T", total);
            cmd.Parameters.AddWithValue("@D", due);
            cmd.ExecuteNonQuery();
        }

        public List<Loan> GetByAccount(int accountID)
        {
            var list = new List<Loan>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT LoanID, AccountID, Principal, InterestRate, MonthlyPayment,
                       TotalDue, AmountPaid, StartDate, DueDate, [Status]
                FROM   Loans WHERE AccountID = @A ORDER BY StartDate DESC", conn);
            cmd.Parameters.AddWithValue("@A", accountID);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapLoan(r));
            return list;
        }

        public Loan? GetActiveLoan(int accountID)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT TOP 1 LoanID, AccountID, Principal, InterestRate, MonthlyPayment,
                       TotalDue, AmountPaid, StartDate, DueDate, [Status]
                FROM   Loans WHERE AccountID = @A AND [Status] = 'Active'
                ORDER  BY StartDate DESC", conn);
            cmd.Parameters.AddWithValue("@A", accountID);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return MapLoan(r);
        }

        public void MakePayment(int loanID, decimal amount)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE Loans
                SET    AmountPaid = AmountPaid + @A,
                       [Status]   = CASE WHEN AmountPaid + @A >= TotalDue THEN 'Paid' ELSE [Status] END
                WHERE  LoanID = @ID", conn);
            cmd.Parameters.AddWithValue("@A", amount);
            cmd.Parameters.AddWithValue("@ID", loanID);
            cmd.ExecuteNonQuery();
        }

        private static Loan MapLoan(SqlDataReader r) => new()
        {
            LoanID         = r.GetInt32(0),
            AccountID      = r.GetInt32(1),
            Principal      = r.GetDecimal(2),
            InterestRate   = r.GetDecimal(3),
            MonthlyPayment = r.GetDecimal(4),
            TotalDue       = r.GetDecimal(5),
            AmountPaid     = r.GetDecimal(6),
            StartDate      = r.GetDateTime(7),
            DueDate        = r.GetDateTime(8),
            Status         = r.GetString(9)
        };
    }

    public class BillPaymentRepository
    {
        public void Insert(int accountID, string biller, string refNo, decimal amount)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO BillPayments (AccountID, Biller, ReferenceNo, Amount)
                VALUES (@A, @B, @R, @M)", conn);
            cmd.Parameters.AddWithValue("@A", accountID);
            cmd.Parameters.AddWithValue("@B", biller);
            cmd.Parameters.AddWithValue("@R", refNo);
            cmd.Parameters.AddWithValue("@M", amount);
            cmd.ExecuteNonQuery();
        }

        public List<(string Biller, string RefNo, decimal Amount, DateTime PaidAt)>
            GetByAccount(int accountID)
        {
            var list = new List<(string, string, decimal, DateTime)>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT Biller, ReferenceNo, Amount, PaidAt
                FROM   BillPayments WHERE AccountID = @A ORDER BY PaidAt DESC", conn);
            cmd.Parameters.AddWithValue("@A", accountID);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetString(0), r.GetString(1), r.GetDecimal(2), r.GetDateTime(3)));
            return list;
        }
    }

    public class AuditRepository
    {
        public void Log(int? userID, string action, string? details = null)
        {
            try
            {
                using var conn = Database.GetConnection();
                using var cmd = new SqlCommand(@"
                    INSERT INTO AuditLog (UserID, Action, Details)
                    VALUES (@U, @A, @D)", conn);
                cmd.Parameters.AddWithValue("@U", userID.HasValue ? (object)userID.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@A", action);
                cmd.Parameters.AddWithValue("@D", details != null ? (object)details : DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            catch { /* Audit must never crash the main flow */ }
        }

        public List<AuditEntry> GetRecent(int limit = 50)
        {
            var list = new List<AuditEntry>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand($@"
                SELECT TOP {limit} al.LogID, al.UserID, u.UserName, al.Action, al.Details, al.LoggedAt
                FROM   AuditLog al
                LEFT JOIN Users u ON u.UserID = al.UserID
                ORDER  BY al.LoggedAt DESC", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new AuditEntry
                {
                    LogID    = r.GetInt32(0),
                    UserID   = r.IsDBNull(1) ? null : r.GetInt32(1),
                    UserName = r.IsDBNull(2) ? "System" : r.GetString(2),
                    Action   = r.GetString(3),
                    Details  = r.IsDBNull(4) ? null : r.GetString(4),
                    LoggedAt = r.GetDateTime(5)
                });
            return list;
        }
    }
}
