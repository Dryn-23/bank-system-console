// Helpers/InputHelper.cs
using System;

namespace BankSystem.Helpers
{
    public static class InputHelper
    {
        public static decimal GetDecimalInput(string prompt, decimal min = 0.01m)
        {
            while (true)
            {
                Console.Write(prompt);
                string? raw = Console.ReadLine();
                if (decimal.TryParse(raw, out decimal val) && val >= min)
                    return val;
                ConsoleHelper.ShowError($"Please enter a valid amount (minimum ₱{min:N2}).");
            }
        }

        public static int GetIntInput(string prompt, int min = 1)
        {
            while (true)
            {
                Console.Write(prompt);
                string? raw = Console.ReadLine();
                if (int.TryParse(raw, out int val) && val >= min)
                    return val;
                ConsoleHelper.ShowError($"Please enter a valid number (minimum {min}).");
            }
        }

        public static string GetStringInput(string prompt, bool allowEmpty = false)
        {
            while (true)
            {
                Console.Write(prompt);
                string val = Console.ReadLine()?.Trim() ?? "";
                if (allowEmpty || !string.IsNullOrWhiteSpace(val))
                    return val;
                ConsoleHelper.ShowError("This field cannot be empty.");
            }
        }

        public static int GetMenuChoice(int min, int max)
        {
            while (true)
            {
                Console.Write("\n  Select: ");
                string? raw = Console.ReadLine();
                if (int.TryParse(raw, out int val) && val >= min && val <= max)
                    return val;
                ConsoleHelper.ShowError($"Please enter a number between {min} and {max}.");
            }
        }

        public static bool Confirm(string prompt)
        {
            Console.Write($"{prompt} (Y/N): ");
            return (Console.ReadLine()?.Trim().ToUpper() ?? "") == "Y";
        }
    }

    public static class ConsoleHelper
    {
        public static void ShowSuccess(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  [✓]  {msg}");
            Console.ResetColor();
        }

        public static void ShowError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n  [✗]  {msg}");
            Console.ResetColor();
        }

        public static void ShowInfo(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  [i]  {msg}");
            Console.ResetColor();
        }

        public static void ShowWarning(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  [!]  {msg}");
            Console.ResetColor();
        }

        public static void Pause()
        {
            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        public static void Header(string title)
        {
            Console.Clear();
            int width = 45;
            string line = new('═', width);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  ╔{line}╗");
            Console.WriteLine($"  ║{title.PadLeft((width + title.Length) / 2).PadRight(width)}║");
            Console.WriteLine($"  ╚{line}╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        public static void Divider(int width = 47)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  " + new string('─', width));
            Console.ResetColor();
        }

        public static void PrintTableRow(params (string text, int width)[] cols)
        {
            Console.Write("  ");
            foreach (var (text, w) in cols)
                Console.Write(text.PadRight(w));
            Console.WriteLine();
        }
    }
}
