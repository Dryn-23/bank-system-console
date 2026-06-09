// Repositories/Database.cs
using Microsoft.Data.SqlClient;

namespace BankSystem.Repositories
{
    public static class Database
    {
        public static readonly string ConnectionString =
            @"Server=DESKTOP-51HVDT7;Database=BankSystem;Trusted_Connection=True;TrustServerCertificate=True;";

        public static SqlConnection GetConnection()
        {
            var conn = new SqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }
    }
}
