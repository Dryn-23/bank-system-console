// UI/TellerUI.cs
using BankSystem.Helpers;
using BankSystem.Models;
using BankSystem.Repositories;
using BankSystem.Services;
using System;
using System.Collections.Generic;

namespace BankSystem.UI
{
    public class TellerUI
    {
        private readonly Session _session;
        private readonly AccountService _accSvc = new();
        private readonly AuthService _authSvc = new();
        private readonly CustomerRepository _custRepo = new();
        private readonly UserRepository _userRepo = new();
        private readonly PendingTransferRepository _pendingRepo = new();
        private readonly TransactionRepository _txRepo = new();
        private readonly AuditRepository _auditRepo = new();

        public TellerUI(Session session) => _session = session;

        public void Show()
        {
            while (true)
            {
                _session.Touch();
                ConsoleHelper.Header($"TELLER MENU  — {_session.UserName}");

                Console.WriteLine("  ── Users & Customers ──────────────────");
                Console.WriteLine("  1.  Register New User");
                Console.WriteLine("  2.  View All Users");
                Console.WriteLine("  3.  View All Customers");
                Console.WriteLine("  4.  Search Customer");
                Console.WriteLine("  5.  Edit Customer Info");
                Console.WriteLine("  6.  Reset User Password");
                Console.WriteLine("  7.  Lock / Unlock User");
                Console.WriteLine();
                Console.WriteLine("  ── Accounts ────────────────────────────");
                Console.WriteLine("  8.  View All Accounts");
                Console.WriteLine("  9.  Freeze / Unfreeze Account");
                Console.WriteLine("  10. Deactivate Account");
                Console.WriteLine("  11. Apply Monthly Interest");
                Console.WriteLine();
                Console.WriteLine("  ── Transfers ───────────────────────────");
                Console.WriteLine("  12. Approve / Reject Pending Transfers");
                Console.WriteLine();
                Console.WriteLine("  ── Reports & Analytics ─────────────────");
                Console.WriteLine("  13. Top Customers by Balance");
                Console.WriteLine("  14. Audit Log");
                Console.WriteLine("  15. Spending Breakdown (by account)");
                Console.WriteLine();
                Console.WriteLine("  0.  Logout");

                int choice = InputHelper.GetMenuChoice(0, 15);
                switch (choice)
                {
                    case 1:  DoRegister(); break;
                    case 2:  DoViewUsers(); break;
                    case 3:  DoViewCustomers(); break;
                    case 4:  DoSearchCustomer(); break;
                    case 5:  DoEditCustomer(); break;
                    case 6:  DoResetPassword(); break;
                    case 7:  DoLockUnlock(); break;
                    case 8:  DoViewAccounts(); break;
                    case 9:  DoFreezeAccount(); break;
                    case 10: DoDeactivateAccount(); break;
                    case 11: DoApplyInterest(); break;
                    case 12: DoApprovePending(); break;
                    case 13: DoTopCustomers(); break;
                    case 14: DoAuditLog(); break;
                    case 15: DoSpendingBreakdown(); break;
                    case 0:  return;
                }
            }
        }

        // ── Register ─────────────────────────────────────────────────
        void DoRegister()
        {
            bool ok = _authSvc.Register(out string msg);
            if (ok) ConsoleHelper.ShowSuccess(msg); else ConsoleHelper.ShowError(msg);
            ConsoleHelper.Pause();
        }

        // ── View Users ───────────────────────────────────────────────
        void DoViewUsers()
        {
            ConsoleHelper.Header("ALL USERS");
            var users = _userRepo.GetAll();
            Console.WriteLine($"  {"ID",-6} {"Username",-20} {"Role",-10} {"Locked",-8} {"Failed",7} {"Created",-12}");
            ConsoleHelper.Divider(66);
            foreach (var u in users)
            {
                Console.ForegroundColor = u.IsLocked ? ConsoleColor.Red : ConsoleColor.White;
                Console.WriteLine($"  {u.UserID,-6} {u.UserName,-20} {u.Role,-10} " +
                                  $"{(u.IsLocked ? "YES" : "no"),-8} {u.FailedLogins,7} " +
                                  $"{u.CreatedAt:yyyy-MM-dd}");
                Console.ResetColor();
            }
            ConsoleHelper.Pause();
        }

        // ── View Customers ───────────────────────────────────────────
        void DoViewCustomers()
        {
            ConsoleHelper.Header("ALL CUSTOMERS");
            var custs = _custRepo.GetAll();
            Console.WriteLine($"  {"ID",-5} {"Name",-24} {"Phone",-15} {"Email",-26} {"Since",-12}");
            ConsoleHelper.Divider(85);
            foreach (var c in custs)
                Console.WriteLine($"  {c.CustomerID,-5} {c.FullName,-24} {c.Phone,-15} {c.Email,-26} {c.DateCreated:yyyy-MM-dd}");
            ConsoleHelper.Pause();
        }

        // ── Search ───────────────────────────────────────────────────
        void DoSearchCustomer()
        {
            ConsoleHelper.Header("SEARCH CUSTOMER");
            string term = InputHelper.GetStringInput("  Search (name / email / account number): ");
            var results = _custRepo.Search(term);
            if (results.Count == 0) { ConsoleHelper.ShowInfo("No results found."); ConsoleHelper.Pause(); return; }
            Console.WriteLine($"  {"ID",-5} {"Name",-24} {"Phone",-15} {"Email",-26}");
            ConsoleHelper.Divider(72);
            foreach (var c in results)
                Console.WriteLine($"  {c.CustomerID,-5} {c.FullName,-24} {c.Phone,-15} {c.Email,-26}");
            ConsoleHelper.Pause();
        }

        // ── Edit Customer ────────────────────────────────────────────
        void DoEditCustomer()
        {
            ConsoleHelper.Header("EDIT CUSTOMER INFO");
            int custID = InputHelper.GetIntInput("  Customer ID: ");
            var custs = _custRepo.GetAll();
            var cust = custs.Find(c => c.CustomerID == custID);
            if (cust == null) { ConsoleHelper.ShowError("Customer not found."); ConsoleHelper.Pause(); return; }

            Console.WriteLine($"  Current: {cust.FullName} | {cust.Phone} | {cust.Email}");
            Console.WriteLine("  (Press ENTER to keep current value)");

            Console.Write($"  First Name [{cust.FirstName}]: ");
            string fn = Console.ReadLine()?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(fn)) cust.FirstName = fn;

            Console.Write($"  Last Name [{cust.LastName}]: ");
            string ln = Console.ReadLine()?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(ln)) cust.LastName = ln;

            Console.Write($"  Phone [{cust.Phone}]: ");
            string ph = Console.ReadLine()?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(ph)) cust.Phone = ph;

            Console.Write($"  Email [{cust.Email}]: ");
            string em = Console.ReadLine()?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(em)) cust.Email = em;

            Console.Write($"  Address [{cust.Address}]: ");
            string ad = Console.ReadLine()?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(ad)) cust.Address = ad;

            _custRepo.Update(cust);
            _auditRepo.Log(_session.UserID, "EDIT_CUSTOMER", $"CustomerID={custID}");
            ConsoleHelper.ShowSuccess("Customer info updated.");
            ConsoleHelper.Pause();
        }

        // ── Reset Password ───────────────────────────────────────────
        void DoResetPassword()
        {
            ConsoleHelper.Header("RESET USER PASSWORD");
            int userID = InputHelper.GetIntInput("  User ID: ");
            var user = _userRepo.GetByID(userID);
            if (user == null) { ConsoleHelper.ShowError("User not found."); ConsoleHelper.Pause(); return; }

            Console.Write($"  New password for '{user.UserName}': ");
            string newPwd = Console.ReadLine() ?? "";
            if (newPwd.Length < 6) { ConsoleHelper.ShowError("Password must be ≥ 6 chars."); ConsoleHelper.Pause(); return; }

            string salt = Helpers.SecurityHelper.GenerateSalt();
            string hash = Helpers.SecurityHelper.HashPassword(newPwd, salt);
            _userRepo.UpdatePassword(userID, hash, salt);
            _auditRepo.Log(_session.UserID, "RESET_PASSWORD", $"UserID={userID} ({user.UserName})");
            ConsoleHelper.ShowSuccess($"Password reset for {user.UserName}.");
            ConsoleHelper.Pause();
        }

        // ── Lock / Unlock ────────────────────────────────────────────
        void DoLockUnlock()
        {
            ConsoleHelper.Header("LOCK / UNLOCK USER");
            int userID = InputHelper.GetIntInput("  User ID: ");
            var user = _userRepo.GetByID(userID);
            if (user == null) { ConsoleHelper.ShowError("User not found."); ConsoleHelper.Pause(); return; }

            bool newState = !user.IsLocked;
            _userRepo.SetLocked(userID, newState);
            string action = newState ? "LOCKED" : "UNLOCKED";
            _auditRepo.Log(_session.UserID, action, $"UserID={userID} ({user.UserName})");
            ConsoleHelper.ShowSuccess($"User '{user.UserName}' is now {action}.");
            ConsoleHelper.Pause();
        }

        // ── View Accounts ────────────────────────────────────────────
        void DoViewAccounts()
        {
            ConsoleHelper.Header("ALL ACCOUNTS");
            var accounts = _accSvc.GetAllAccounts();
            Console.WriteLine($"  {"AccID",-6} {"Number",-14} {"Type",-10} {"Balance",13} {"Status",-10} {"Owner",-24}");
            ConsoleHelper.Divider(80);
            foreach (var a in accounts)
            {
                Console.ForegroundColor = a.Status switch
                {
                    "Active"   => ConsoleColor.White,
                    "Frozen"   => ConsoleColor.Cyan,
                    "Inactive" => ConsoleColor.DarkGray,
                    _          => ConsoleColor.White
                };
                Console.WriteLine($"  {a.AccountID,-6} {a.AccountNumber,-14} {a.AccountType,-10} " +
                                  $"₱{a.Balance,11:N2} {a.Status,-10} {a.OwnerName,-24}");
                Console.ResetColor();
            }
            ConsoleHelper.Pause();
        }

        // ── Freeze / Unfreeze ────────────────────────────────────────
        void DoFreezeAccount()
        {
            ConsoleHelper.Header("FREEZE / UNFREEZE ACCOUNT");
            int accID = InputHelper.GetIntInput("  Account ID: ");
            var acc = _accSvc.GetAccount(accID);
            if (acc == null) { ConsoleHelper.ShowError("Account not found."); ConsoleHelper.Pause(); return; }

            string newStatus = acc.Status == "Frozen" ? "Active" : "Frozen";
            _accSvc.SetStatus(accID, newStatus, _session.UserID);
            ConsoleHelper.ShowSuccess($"Account {accID} is now {newStatus}.");
            ConsoleHelper.Pause();
        }

        // ── Deactivate ───────────────────────────────────────────────
        void DoDeactivateAccount()
        {
            ConsoleHelper.Header("DEACTIVATE ACCOUNT");
            int accID = InputHelper.GetIntInput("  Account ID: ");
            if (!InputHelper.Confirm("  Confirm deactivation?"))
            { ConsoleHelper.ShowWarning("Cancelled."); ConsoleHelper.Pause(); return; }
            _accSvc.SetStatus(accID, "Inactive", _session.UserID);
            ConsoleHelper.ShowSuccess($"Account {accID} deactivated.");
            ConsoleHelper.Pause();
        }

        // ── Apply Interest ───────────────────────────────────────────
        void DoApplyInterest()
        {
            ConsoleHelper.Header("APPLY MONTHLY INTEREST");
            ConsoleHelper.ShowInfo("This applies interest to all eligible Savings accounts (every 30 days).");
            if (!InputHelper.Confirm("  Proceed?")) { ConsoleHelper.ShowWarning("Cancelled."); ConsoleHelper.Pause(); return; }
            _accSvc.ApplyInterest();
            ConsoleHelper.ShowSuccess("Monthly interest applied to eligible Savings accounts.");
            ConsoleHelper.Pause();
        }

        // ── Approve Pending Transfers ────────────────────────────────
        void DoApprovePending()
        {
            ConsoleHelper.Header("PENDING TRANSFERS");
            var pending = _pendingRepo.GetPending();
            if (pending.Count == 0) { ConsoleHelper.ShowInfo("No pending transfers."); ConsoleHelper.Pause(); return; }

            Console.WriteLine($"  {"ID",-6} {"From",-14} {"To",-14} {"Amount",12} {"Owner",-20} {"Requested",-20}");
            ConsoleHelper.Divider(86);
            foreach (var p in pending)
                Console.WriteLine($"  {p.PendingID,-6} {p.FromAccountNumber,-14} {p.ToAccountNumber,-14} " +
                                  $"₱{p.Amount,10:N2} {p.FromOwner,-20} {p.RequestedAt:yyyy-MM-dd HH:mm}");

            int pid = InputHelper.GetIntInput("  Enter Pending ID to action (0 to go back): ", 0);
            if (pid == 0) return;

            var item = _pendingRepo.GetByID(pid);
            if (item == null) { ConsoleHelper.ShowError("Not found."); ConsoleHelper.Pause(); return; }

            Console.WriteLine($"\n  Transfer ₱{item.Amount:N2} from {item.FromOwner} → {item.ToOwner}");
            Console.WriteLine("  [1] Approve   [2] Reject   [0] Cancel");
            int action = InputHelper.GetMenuChoice(0, 2);

            if (action == 0) return;

            if (action == 1)
            {
                // Execute the actual transfer
                var accRepo = new AccountRepository();
                var txR     = new TransactionRepository();
                using var conn = Database.GetConnection();
                using var tx   = conn.BeginTransaction();
                decimal bal = accRepo.GetBalance(item.FromAccountID, conn, tx);
                if (bal < item.Amount)
                {
                    ConsoleHelper.ShowError($"Sender has insufficient funds (₱{bal:N2}).");
                    _pendingRepo.SetStatus(pid, "Rejected", _session.UserID);
                    ConsoleHelper.Pause(); return;
                }
                accRepo.UpdateBalance(item.FromAccountID, -item.Amount, conn, tx);
                accRepo.UpdateBalance(item.ToAccountID, item.Amount, conn, tx);
                txR.Log(item.FromAccountID, "Transfer Out", item.Amount, conn, tx);
                txR.Log(item.ToAccountID, "Transfer In", item.Amount, conn, tx);
                tx.Commit();
                _pendingRepo.SetStatus(pid, "Approved", _session.UserID);
                _auditRepo.Log(_session.UserID, "APPROVE_TRANSFER",
                    $"PendingID={pid} Amount=₱{item.Amount:N2}");
                ConsoleHelper.ShowSuccess($"Transfer of ₱{item.Amount:N2} approved and executed.");
            }
            else
            {
                _pendingRepo.SetStatus(pid, "Rejected", _session.UserID);
                _auditRepo.Log(_session.UserID, "REJECT_TRANSFER", $"PendingID={pid}");
                ConsoleHelper.ShowWarning("Transfer rejected.");
            }
            ConsoleHelper.Pause();
        }

        // ── Top Customers ────────────────────────────────────────────
        void DoTopCustomers()
        {
            ConsoleHelper.Header("TOP CUSTOMERS BY BALANCE");
            var top = _accSvc.GetTopAccounts(10);
            Console.WriteLine($"  {"#",-4} {"Account",-14} {"Type",-10} {"Balance",13} {"Owner",-24}");
            ConsoleHelper.Divider(68);
            for (int i = 0; i < top.Count; i++)
            {
                var a = top[i];
                Console.ForegroundColor = i == 0 ? ConsoleColor.Yellow
                                        : i == 1 ? ConsoleColor.Gray
                                        : ConsoleColor.White;
                string medal = i == 0 ? " 🥇" : i == 1 ? " 🥈" : i == 2 ? " 🥉" : "";
                Console.WriteLine($"  {i + 1,-4} {a.AccountNumber,-14} {a.AccountType,-10} ₱{a.Balance,11:N2} {a.OwnerName,-24}{medal}");
                Console.ResetColor();
            }
            ConsoleHelper.Pause();
        }

        // ── Audit Log ────────────────────────────────────────────────
        void DoAuditLog()
        {
            ConsoleHelper.Header("AUDIT LOG (LAST 50 ENTRIES)");
            var logs = _auditRepo.GetRecent(50);
            Console.WriteLine($"  {"ID",-6} {"User",-16} {"Action",-24} {"Details",-30} {"Time",-20}");
            ConsoleHelper.Divider(98);
            foreach (var l in logs)
            {
                Console.ForegroundColor = l.Action.Contains("FAIL") || l.Action.Contains("LOCK")
                    ? ConsoleColor.Red : ConsoleColor.White;
                string detail = (l.Details ?? "").Length > 28
                    ? (l.Details ?? "")[..28] + ".." : (l.Details ?? "");
                Console.WriteLine($"  {l.LogID,-6} {l.UserName,-16} {l.Action,-24} {detail,-30} {l.LoggedAt:yyyy-MM-dd HH:mm:ss}");
                Console.ResetColor();
            }
            ConsoleHelper.Pause();
        }

        // ── Spending Breakdown ───────────────────────────────────────
        void DoSpendingBreakdown()
        {
            ConsoleHelper.Header("SPENDING BREAKDOWN");
            int accID = InputHelper.GetIntInput("  Account ID: ");
            var acc = _accSvc.GetAccount(accID);
            if (acc == null) { ConsoleHelper.ShowError("Account not found."); ConsoleHelper.Pause(); return; }

            Console.WriteLine($"  Account: {acc.AccountNumber}  |  Owner: {acc.OwnerName}");
            ConsoleHelper.Divider();

            var summary = _txRepo.GetMonthlySummary(accID);
            Console.WriteLine($"  {"Month",-10} {"Deposits",14} {"Withdrawals",14} {"Transfers",14} {"Net",14}");
            ConsoleHelper.Divider(58);
            if (summary.Count == 0) { Console.WriteLine("  No transaction data."); ConsoleHelper.Pause(); return; }

            decimal totalDep = 0, totalWit = 0, totalTrf = 0;
            foreach (var (mo, dep, wit, trf) in summary)
            {
                totalDep += dep; totalWit += wit; totalTrf += trf;
                decimal net = dep - wit - trf;
                Console.ForegroundColor = net >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"  {mo,-10} ₱{dep,12:N2} ₱{wit,12:N2} ₱{trf,12:N2} ₱{net,12:N2}");
                Console.ResetColor();
            }
            ConsoleHelper.Divider(58);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  {"TOTAL",-10} ₱{totalDep,12:N2} ₱{totalWit,12:N2} ₱{totalTrf,12:N2} ₱{totalDep - totalWit - totalTrf,12:N2}");
            Console.ResetColor();
            ConsoleHelper.Pause();
        }
    }
}
