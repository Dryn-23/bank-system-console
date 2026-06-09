// Helpers/SecurityHelper.cs
using System;
using System.Security.Cryptography;
using System.Text;

namespace BankSystem.Helpers
{
    public static class SecurityHelper
    {
        // ── Salted SHA-256 hash ────────────────────────────────────────
        public static string GenerateSalt()
        {
            byte[] saltBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(saltBytes);
            return Convert.ToHexString(saltBytes);
        }

        public static string HashPassword(string password, string salt)
        {
            string salted = password + salt;
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salted));
            return Convert.ToHexString(bytes);
        }

        // ── Backward-compatible: hash with no salt (old records) ──────
        public static string HashPasswordLegacy(string password)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        // ── OTP generation ────────────────────────────────────────────
        public static string GenerateOTP()
        {
            int code = RandomNumberGenerator.GetInt32(100000, 999999);
            return code.ToString();
        }

        // ── Simulate "sending" OTP (console simulation) ───────────────
        public static void SimulateSendOTP(string username, string code)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("  ┌─────────────────────────────────────────┐");
            Console.WriteLine($"  │  OTP for {username,-12}: [ {code} ]          │");
            Console.WriteLine("  │  (Expires in 5 minutes)                 │");
            Console.WriteLine("  └─────────────────────────────────────────┘");
            Console.ResetColor();
        }
    }
}
