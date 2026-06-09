// Program.cs  —  BankSystem Entry Point
using BankSystem.Helpers;
using BankSystem.Models;
using BankSystem.Services;
using BankSystem.UI;

class Program
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var authSvc = new AuthService();

        while (true)
        {
            ConsoleHelper.Header("BANK SYSTEM  v2.0");

            Console.WriteLine("  1. Register User");
            Console.WriteLine("  2. Login");
            Console.WriteLine("  0. Exit");

            int choice = InputHelper.GetMenuChoice(0, 2);

            switch (choice)
            {
                case 1:
                    bool ok = authSvc.Register(out string regMsg);
                    if (ok) ConsoleHelper.ShowSuccess(regMsg);
                    else    ConsoleHelper.ShowError(regMsg);
                    ConsoleHelper.Pause();
                    break;

                case 2:
                    Session? session = authSvc.Login(out string loginMsg);

                    if (session == null)
                    {
                        ConsoleHelper.ShowError(loginMsg);
                        ConsoleHelper.Pause();
                        break;
                    }

                    ConsoleHelper.ShowSuccess(loginMsg);
                    ConsoleHelper.Pause();

                    // ── Session loop with timeout check ────────────────
                    bool timedOut = false;
                    while (!timedOut)
                    {
                        if (authSvc.CheckTimeout(session))
                        {
                            ConsoleHelper.ShowWarning($"Session timed out after {Session.TimeoutMinutes} minutes of inactivity.");
                            ConsoleHelper.Pause();
                            timedOut = true;
                            break;
                        }

                        // Route to the right menu
                        if (session.IsTeller)
                        {
                            new TellerUI(session).Show();
                            break;  // Logout chosen inside menu
                        }
                        else if (session.IsCustomer)
                        {
                            new CustomerUI(session).Show();
                            break;  // Logout chosen inside menu
                        }
                        else
                        {
                            ConsoleHelper.ShowError("Unknown role.");
                            break;
                        }
                    }
                    break;

                case 0:
                    ConsoleHelper.ShowInfo("Goodbye!");
                    return;
            }
        }
    }
}
