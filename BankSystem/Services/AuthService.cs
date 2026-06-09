// Services/AuthService.cs
using BankSystem.Helpers;
using BankSystem.Models;
using BankSystem.Repositories;
using Microsoft.Data.SqlClient;
using System;

namespace BankSystem.Services
{
    public class AuthService
    {
        private readonly UserRepository _users = new();
        private readonly CustomerRepository _customers = new();
        private readonly AccountRepository _accounts = new();
        private readonly AuditRepository _audit = new();

        public const int MaxFailedAttempts = 5;

        // ── Register ─────────────────────────────────────────────────
        public bool Register(out string message)
        {
            message = "";
            ConsoleHelper.Header("REGISTER USER");

            string username = InputHelper.GetStringInput("  Username                 : ");

            // Check duplicate
            if (_users.GetByUsername(username) != null)
            { message = "Username already exists."; return false; }

            Console.Write("  Password                 : ");
            string password = Console.ReadLine() ?? "";
            if (password.Length < 6)
            { message = "Password must be at least 6 characters."; return false; }

            Console.Write("  Confirm Password         : ");
            string confirm = Console.ReadLine() ?? "";
            if (password != confirm)
            { message = "Passwords do not match."; return false; }

            Console.Write("  Role (Customer / Teller) : ");
            string role = Console.ReadLine()?.Trim() ?? "";
            if (role != "Customer" && role != "Teller")
            { message = "Role must be 'Customer' or 'Teller'."; return false; }

            string salt = SecurityHelper.GenerateSalt();
            string hash = SecurityHelper.HashPassword(password, salt);

            try
            {
                using var conn = Database.GetConnection();
                using var tx = conn.BeginTransaction();

                int newUserID = _users.Insert(username, hash, salt, role, conn, tx);

                if (role == "Customer")
                {
                    var customer = new Customer
                    {
                        FirstName = InputHelper.GetStringInput("  First Name               : "),
                        LastName  = InputHelper.GetStringInput("  Last Name                : "),
                        Address   = InputHelper.GetStringInput("  Address                  : "),
                        Phone     = InputHelper.GetStringInput("  Phone                    : "),
                        Email     = InputHelper.GetStringInput("  Email                    : ")
                    };

                    Console.Write("  Account Type (Savings / Checking): ");
                    string accountType = Console.ReadLine()?.Trim() ?? "Savings";
                    if (accountType != "Savings" && accountType != "Checking")
                        accountType = "Savings";

                    // Ask about opening a second account type
                    bool addSecond = InputHelper.Confirm("  Add a second account (both Savings & Checking)?");

                    int custID = _customers.Insert(customer, newUserID, conn, tx);

                    // Primary account
                    string accNum = $"ACC{newUserID:D6}";
                    _accounts.Insert(custID, accNum, accountType, conn, tx);

                    // Optional second account
                    if (addSecond)
                    {
                        string secondType = accountType == "Savings" ? "Checking" : "Savings";
                        string accNum2 = $"ACC{newUserID:D6}B";
                        _accounts.Insert(custID, accNum2, secondType, conn, tx);
                    }
                }

                tx.Commit();
                _audit.Log(newUserID, "REGISTER", $"New {role} registered: {username}");
                message = $"'{username}' registered successfully as {role}.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Registration error: {ex.Message}";
                return false;
            }
        }

        // ── Login ─────────────────────────────────────────────────────
        public Session? Login(out string message)
        {
            message = "";
            ConsoleHelper.Header("LOGIN");

            string username = InputHelper.GetStringInput("  Username : ");
            Console.Write("  Password : ");
            string password = Console.ReadLine() ?? "";

            User? user = _users.GetByUsername(username);

            // User not found — don't reveal that
            if (user == null)
            {
                message = "Invalid username or password.";
                _audit.Log(null, "LOGIN_FAIL", $"Unknown user: {username}");
                return null;
            }

            // Locked
            if (user.IsLocked)
            {
                message = "Account is locked after too many failed attempts. Contact a Teller.";
                _audit.Log(user.UserID, "LOGIN_LOCKED", username);
                return null;
            }

            // Verify password — support both salted (new) and legacy (old)
            bool valid;
            if (!string.IsNullOrEmpty(user.PasswordSalt))
                valid = SecurityHelper.HashPassword(password, user.PasswordSalt) == user.PasswordHash;
            else
                valid = SecurityHelper.HashPasswordLegacy(password) == user.PasswordHash;

            if (!valid)
            {
                _users.IncrementFailedLogins(user.UserID);
                int remaining = MaxFailedAttempts - (user.FailedLogins + 1);
                _audit.Log(user.UserID, "LOGIN_FAIL", $"Wrong password for {username}");
                message = remaining > 0
                    ? $"Invalid password. {remaining} attempt(s) remaining before lock."
                    : "Account locked after too many failed attempts.";
                return null;
            }

            // ── OTP Challenge ─────────────────────────────────────────
            string otpCode = SecurityHelper.GenerateOTP();
            _users.SaveOTP(user.UserID, otpCode);
            SecurityHelper.SimulateSendOTP(username, otpCode);

            Console.Write("\n  Enter OTP code: ");
            string enteredOTP = Console.ReadLine()?.Trim() ?? "";

            if (!_users.ValidateOTP(user.UserID, enteredOTP))
            {
                _audit.Log(user.UserID, "LOGIN_OTP_FAIL", username);
                message = "Invalid or expired OTP.";
                return null;
            }

            // ── Success ────────────────────────────────────────────────
            _users.ResetFailedLogins(user.UserID);
            _audit.Log(user.UserID, "LOGIN", $"{user.Role} logged in");

            var session = new Session
            {
                UserID    = user.UserID,
                UserName  = user.UserName,
                Role      = user.Role,
                LoginTime = DateTime.Now,
                LastActivity = DateTime.Now
            };

            // Resolve AccountID for customers
            if (user.Role == "Customer")
            {
                using var conn = Database.GetConnection();
                int? accID = _accounts.GetActiveAccountIDForUser(user.UserID, conn);
                if (accID == null)
                {
                    message = "No active account linked to this user.";
                    _audit.Log(user.UserID, "LOGIN_NO_ACCOUNT", username);
                    return null;
                }
                session.AccountID = accID.Value;
            }

            message = $"Welcome, {user.UserName}! Role: {user.Role}";
            return session;
        }

        // ── Check session timeout ─────────────────────────────────────
        public bool CheckTimeout(Session session)
        {
            if (!session.IsTimedOut) return false;
            _audit.Log(session.UserID, "SESSION_TIMEOUT", session.UserName);
            return true;
        }
    }
}
