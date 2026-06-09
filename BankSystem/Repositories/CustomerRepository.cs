// Repositories/CustomerRepository.cs
using Microsoft.Data.SqlClient;
using BankSystem.Models;
using System;
using System.Collections.Generic;

namespace BankSystem.Repositories
{
    public class CustomerRepository
    {
        public int Insert(Customer c, int userID, SqlConnection conn, SqlTransaction tx)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO Customers (FirstName, LastName, [Address], Phone, Email, UserID)
                OUTPUT INSERTED.CustomerID
                VALUES (@F, @L, @A, @P, @E, @U)", conn, tx);
            cmd.Parameters.AddWithValue("@F", c.FirstName);
            cmd.Parameters.AddWithValue("@L", c.LastName);
            cmd.Parameters.AddWithValue("@A", c.Address);
            cmd.Parameters.AddWithValue("@P", c.Phone);
            cmd.Parameters.AddWithValue("@E", c.Email);
            cmd.Parameters.AddWithValue("@U", userID);
            return (int)cmd.ExecuteScalar()!;
        }

        public Customer? GetByUserID(int userID)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT CustomerID, UserID, FirstName, LastName,
                       [Address], Phone, Email, DateCreated
                FROM   Customers WHERE UserID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", userID);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return MapCustomer(r);
        }

        public List<Customer> GetAll()
        {
            var list = new List<Customer>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT CustomerID, UserID, FirstName, LastName,
                       [Address], Phone, Email, DateCreated
                FROM   Customers ORDER BY CustomerID", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapCustomer(r));
            return list;
        }

        // Search by name, email, or account number
        public List<Customer> Search(string term)
        {
            var list = new List<Customer>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT DISTINCT c.CustomerID, c.UserID, c.FirstName, c.LastName,
                       c.[Address], c.Phone, c.Email, c.DateCreated
                FROM   Customers c
                LEFT JOIN Accounts a ON a.CustomerID = c.CustomerID
                WHERE  c.FirstName  LIKE @T
                  OR   c.LastName   LIKE @T
                  OR   c.Email      LIKE @T
                  OR   a.AcountNumber LIKE @T
                ORDER  BY c.CustomerID", conn);
            cmd.Parameters.AddWithValue("@T", $"%{term}%");
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapCustomer(r));
            return list;
        }

        public void Update(Customer c)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE Customers
                SET FirstName=[F], LastName=[L], [Address]=[A], Phone=[P], Email=[E]
                WHERE CustomerID=@ID", conn);
            // use named params properly
            cmd.CommandText = @"
                UPDATE Customers
                SET FirstName=@F, LastName=@L, [Address]=@A, Phone=@P, Email=@E
                WHERE CustomerID=@ID";
            cmd.Parameters.AddWithValue("@F", c.FirstName);
            cmd.Parameters.AddWithValue("@L", c.LastName);
            cmd.Parameters.AddWithValue("@A", c.Address);
            cmd.Parameters.AddWithValue("@P", c.Phone);
            cmd.Parameters.AddWithValue("@E", c.Email);
            cmd.Parameters.AddWithValue("@ID", c.CustomerID);
            cmd.ExecuteNonQuery();
        }

        private static Customer MapCustomer(SqlDataReader r) => new()
        {
            CustomerID  = r.GetInt32(r.GetOrdinal("CustomerID")),
            UserID      = r.IsDBNull(r.GetOrdinal("UserID")) ? 0 : r.GetInt32(r.GetOrdinal("UserID")),
            FirstName   = r.GetString(r.GetOrdinal("FirstName")),
            LastName    = r.GetString(r.GetOrdinal("LastName")),
            Address     = r.IsDBNull(r.GetOrdinal("Address")) ? "" : r.GetString(r.GetOrdinal("Address")),
            Phone       = r.IsDBNull(r.GetOrdinal("Phone")) ? "" : r.GetString(r.GetOrdinal("Phone")),
            Email       = r.IsDBNull(r.GetOrdinal("Email")) ? "" : r.GetString(r.GetOrdinal("Email")),
            DateCreated = r.GetDateTime(r.GetOrdinal("DateCreated"))
        };
    }
}
