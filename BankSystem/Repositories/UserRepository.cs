// Repositories/UserRepository.cs
using Microsoft.Data.SqlClient;
using BankSystem.Models;
using System;
using System.Collections.Generic;

namespace BankSystem.Repositories
{
    public class UserRepository
    {
        // ── Fetch by username (for login) ──────────────────────────────
        public User? GetByUsername(string username)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT UserID, UserName, PasswordHash, PasswordSalt,
                       Role, FailedLogins, IsLocked, LastActivity, CreatedAt
                FROM   Users
                WHERE  UserName = @UserName", conn);
            cmd.Parameters.AddWithValue("@UserName", username);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return MapUser(r);
        }

        public User? GetByID(int userID)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT UserID, UserName, PasswordHash, PasswordSalt,
                       Role, FailedLogins, IsLocked, LastActivity, CreatedAt
                FROM   Users WHERE UserID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", userID);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return MapUser(r);
        }

        public List<User> GetAll()
        {
            var list = new List<User>();
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(
                "SELECT UserID, UserName, PasswordHash, PasswordSalt, Role, FailedLogins, IsLocked, LastActivity, CreatedAt FROM Users ORDER BY UserID", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapUser(r));
            return list;
        }

        // ── Insert new user; returns new UserID ───────────────────────
        public int Insert(string username, string passwordHash, string salt, string role,
                          SqlConnection conn, SqlTransaction tx)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO Users (UserName, PasswordHash, PasswordSalt, Role)
                OUTPUT INSERTED.UserID
                VALUES (@UserName, @PasswordHash, @Salt, @Role)", conn, tx);
            cmd.Parameters.AddWithValue("@UserName", username);
            cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
            cmd.Parameters.AddWithValue("@Salt", salt);
            cmd.Parameters.AddWithValue("@Role", role);
            return (int)cmd.ExecuteScalar()!;
        }

        // ── Failed-login management ────────────────────────────────────
        public void IncrementFailedLogins(int userID)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE Users
                SET    FailedLogins = FailedLogins + 1,
                       IsLocked     = CASE WHEN FailedLogins + 1 >= 5 THEN 1 ELSE 0 END
                WHERE  UserID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", userID);
            cmd.ExecuteNonQuery();
        }

        public void ResetFailedLogins(int userID)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE Users SET FailedLogins = 0, IsLocked = 0, LastActivity = GETDATE()
                WHERE UserID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", userID);
            cmd.ExecuteNonQuery();
        }

        public void UpdateLastActivity(int userID)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(
                "UPDATE Users SET LastActivity = GETDATE() WHERE UserID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", userID);
            cmd.ExecuteNonQuery();
        }

        // ── Password reset ─────────────────────────────────────────────
        public void UpdatePassword(int userID, string newHash, string newSalt)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE Users SET PasswordHash = @Hash, PasswordSalt = @Salt
                WHERE UserID = @ID", conn);
            cmd.Parameters.AddWithValue("@Hash", newHash);
            cmd.Parameters.AddWithValue("@Salt", newSalt);
            cmd.Parameters.AddWithValue("@ID", userID);
            cmd.ExecuteNonQuery();
        }

        // ── Lock / Unlock ──────────────────────────────────────────────
        public void SetLocked(int userID, bool locked)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(
                "UPDATE Users SET IsLocked = @L, FailedLogins = 0 WHERE UserID = @ID", conn);
            cmd.Parameters.AddWithValue("@L", locked ? 1 : 0);
            cmd.Parameters.AddWithValue("@ID", userID);
            cmd.ExecuteNonQuery();
        }

        // ── OTP ───────────────────────────────────────────────────────
        public void SaveOTP(int userID, string code)
        {
            using var conn = Database.GetConnection();
            // Invalidate old codes first
            using var del = new SqlCommand(
                "UPDATE OTPCodes SET IsUsed = 1 WHERE UserID = @ID AND IsUsed = 0", conn);
            del.Parameters.AddWithValue("@ID", userID);
            del.ExecuteNonQuery();

            using var ins = new SqlCommand(@"
                INSERT INTO OTPCodes (UserID, Code, ExpiresAt)
                VALUES (@ID, @Code, DATEADD(MINUTE, 5, GETDATE()))", conn);
            ins.Parameters.AddWithValue("@ID", userID);
            ins.Parameters.AddWithValue("@Code", code);
            ins.ExecuteNonQuery();
        }

        public bool ValidateOTP(int userID, string code)
        {
            using var conn = Database.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT OTPID FROM OTPCodes
                WHERE  UserID = @ID AND Code = @Code
                  AND  IsUsed = 0 AND ExpiresAt > GETDATE()", conn);
            cmd.Parameters.AddWithValue("@ID", userID);
            cmd.Parameters.AddWithValue("@Code", code);
            var result = cmd.ExecuteScalar();
            if (result == null) return false;

            // Mark used
            using var upd = new SqlCommand(
                "UPDATE OTPCodes SET IsUsed = 1 WHERE OTPID = @OID", conn);
            upd.Parameters.AddWithValue("@OID", result);
            upd.ExecuteNonQuery();
            return true;
        }

        private static User MapUser(SqlDataReader r) => new()
        {
            UserID       = r.GetInt32(r.GetOrdinal("UserID")),
            UserName     = r.GetString(r.GetOrdinal("UserName")),
            PasswordHash = r.GetString(r.GetOrdinal("PasswordHash")),
            PasswordSalt = r.IsDBNull(r.GetOrdinal("PasswordSalt")) ? "" : r.GetString(r.GetOrdinal("PasswordSalt")),
            Role         = r.GetString(r.GetOrdinal("Role")),
            FailedLogins = r.GetInt32(r.GetOrdinal("FailedLogins")),
            IsLocked     = r.GetBoolean(r.GetOrdinal("IsLocked")),
            LastActivity = r.IsDBNull(r.GetOrdinal("LastActivity")) ? null : r.GetDateTime(r.GetOrdinal("LastActivity")),
            CreatedAt    = r.GetDateTime(r.GetOrdinal("CreatedAt"))
        };
    }
}
