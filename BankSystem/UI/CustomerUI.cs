// UI/CustomerUI.cs
using BankSystem.Helpers;
using BankSystem.Models;
using BankSystem.Repositories;
using BankSystem.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BankSystem.UI
{
    public class CustomerUI
    {
        private readonly Session _session;
        private readonly AccountService _accSvc = new();
        private readonly LoanService _loanSvc = new();
        private readonly BillPaymentService _billSvc = new();
        private readonly TransactionRepository _txRepo = new();

        // Customers may switch between their own accounts
        private int ActiveAccountID => _session.AccountID;

        public CustomerUI(Session session) => _session = session;

        public void Show()
        {
            while (true)
            {
                _session.Touch();

                ConsoleHelper.Header($"CUSTOMER MENU  — {_session.UserName}");
                var acc = _accSvc.GetAccount(ActiveAccountID);
                if (acc != null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"  Account: {acc.AccountNumber}  |  Type: {acc.AccountType}" +
                                      $"  |  Balance: ₱{acc.Balance:N2}");
                    Console.ResetColor();
                    Console.WriteLine();
                }

                Console.WriteLine("  ── Transactions ────────────────────────");
                Console.WriteLine("  1.  Deposit");
                Console.WriteLine("  2.  Withdraw");
                Console.WriteLine("  3.  Transfer (Instant)");
                Console.WriteLine("  4.  Transfer (Pending — needs approval)");
                Console.WriteLine();
                Console.WriteLine("  ── Accounts ────────────────────────────");
                Console.WriteLine("  5.  Check Balance");
                Console.WriteLine("  6.  Switch Active Account");
                Console.WriteLine("  7.  Transaction History");
                Console.WriteLine("  8.  Mini Statement (last 5)");
                Console.WriteLine("  9.  Monthly Summary");
                Console.WriteLine("  10. Print Statement (PDF)");
                Console.WriteLine();
                Console.WriteLine("  ── Loans ───────────────────────────────");
                Console.WriteLine("  11. Apply for Loan");
                Console.WriteLine("  12. Pay Loan");
                Console.WriteLine("  13. View Loan Status");
                Console.WriteLine();
                Console.WriteLine("  ── Bills ───────────────────────────────");
                Console.WriteLine("  14. Pay Bill");
                Console.WriteLine("  15. Bill Payment History");
                Console.WriteLine();
                Console.WriteLine("  0.  Logout");

                int choice = InputHelper.GetMenuChoice(0, 15);

                switch (choice)
                {
                    case 1:  DoDeposit(); break;
                    case 2:  DoWithdraw(); break;
                    case 3:  DoTransfer(pending: false); break;
                    case 4:  DoTransfer(pending: true); break;
                    case 5:  DoCheckBalance(); break;
                    case 6:  DoSwitchAccount(); break;
                    case 7:  DoTransactionHistory(); break;
                    case 8:  DoMiniStatement(); break;
                    case 9:  DoMonthlySummary(); break;
                    case 10: DoPrintPDF(); break;
                    case 11: DoApplyLoan(); break;
                    case 12: DoPayLoan(); break;
                    case 13: DoLoanStatus(); break;
                    case 14: DoPayBill(); break;
                    case 15: DoBillHistory(); break;
                    case 0:  return;
                }
            }
        }

        // ── Deposit ──────────────────────────────────────────────────
        void DoDeposit()
        {
            ConsoleHelper.Header("DEPOSIT");
            decimal amount = InputHelper.GetDecimalInput("  Amount: ₱");
            var (ok, msg) = _accSvc.Deposit(ActiveAccountID, amount, _session.UserID);
            if (ok) ConsoleHelper.ShowSuccess(msg); else ConsoleHelper.ShowError(msg);
            ConsoleHelper.Pause();
        }

        // ── Withdraw ─────────────────────────────────────────────────
        void DoWithdraw()
        {
            ConsoleHelper.Header("WITHDRAW");
            decimal amount = InputHelper.GetDecimalInput("  Amount: ₱");
            var (ok, msg) = _accSvc.Withdraw(ActiveAccountID, amount, _session.UserID);
            if (ok) ConsoleHelper.ShowSuccess(msg); else ConsoleHelper.ShowError(msg);
            ConsoleHelper.Pause();
        }

        // ── Transfer ─────────────────────────────────────────────────
        void DoTransfer(bool pending)
        {
            ConsoleHelper.Header(pending ? "TRANSFER (PENDING APPROVAL)" : "TRANSFER (INSTANT)");
            if (!pending)
                ConsoleHelper.ShowInfo($"Note: A transfer fee of ₱{AccountService.TransferFee:N2} applies.");

            string dest = InputHelper.GetStringInput("  Destination Account Number: ");
            decimal amount = InputHelper.GetDecimalInput("  Amount: ₱");

            if (!InputHelper.Confirm($"  Confirm transfer of ₱{amount:N2} to {dest}?"))
            { ConsoleHelper.ShowWarning("Transfer cancelled."); ConsoleHelper.Pause(); return; }

            (bool ok, string msg) result = pending
                ? _accSvc.RequestPendingTransfer(ActiveAccountID, dest, amount, _session.UserID)
                : _accSvc.Transfer(ActiveAccountID, dest, amount, _session.UserID);

            if (result.ok) ConsoleHelper.ShowSuccess(result.msg);
            else ConsoleHelper.ShowError(result.msg);
            ConsoleHelper.Pause();
        }

        // ── Balance ──────────────────────────────────────────────────
        void DoCheckBalance()
        {
            ConsoleHelper.Header("ACCOUNT BALANCE");
            var acc = _accSvc.GetAccount(ActiveAccountID);
            if (acc == null) { ConsoleHelper.ShowError("Account not found."); ConsoleHelper.Pause(); return; }

            Console.WriteLine($"  Account Holder  : {acc.OwnerName}");
            Console.WriteLine($"  Account Number  : {acc.AccountNumber}");
            Console.WriteLine($"  Account Type    : {acc.AccountType}");
            Console.WriteLine($"  Status          : {acc.Status}");
            Console.WriteLine($"  Interest Rate   : {acc.InterestRate * 100:N1}% / month");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Balance         : ₱{acc.Balance:N2}");
            Console.ResetColor();
            ConsoleHelper.Pause();
        }

        // ── Switch Account ───────────────────────────────────────────
        void DoSwitchAccount()
        {
            ConsoleHelper.Header("SWITCH ACCOUNT");
            var accounts = _accSvc.GetAccountsForUser(_session.UserID);
            if (accounts.Count <= 1)
            {
                ConsoleHelper.ShowInfo("You only have one account.");
                ConsoleHelper.Pause(); return;
            }

            Console.WriteLine($"  {"#",-4} {"Account Number",-14} {"Type",-10} {"Balance",12} {"Status",-10}");
            ConsoleHelper.Divider(52);
            for (int i = 0; i < accounts.Count; i++)
            {
                var a = accounts[i];
                string active = a.AccountID == ActiveAccountID ? " ◄ active" : "";
                Console.WriteLine($"  {i + 1,-4} {a.AccountNumber,-14} {a.AccountType,-10} ₱{a.Balance,10:N2} {a.Status,-10}{active}");
            }

            int sel = InputHelper.GetMenuChoice(1, accounts.Count);
            _session.AccountID = accounts[sel - 1].AccountID;
            ConsoleHelper.ShowSuccess($"Switched to account {accounts[sel - 1].AccountNumber}.");
            ConsoleHelper.Pause();
        }

        // ── Transaction History ──────────────────────────────────────
        void DoTransactionHistory()
        {
            ConsoleHelper.Header("TRANSACTION HISTORY");
            var txs = _txRepo.GetByAccount(ActiveAccountID);

            Console.WriteLine($"  {"Date",-20} {"Type",-16} {"Amount",12}");
            ConsoleHelper.Divider(52);
            if (txs.Count == 0) { Console.WriteLine("  No transactions yet."); ConsoleHelper.Pause(); return; }

            int page = 0, pageSize = 15;
            while (true)
            {
                int start = page * pageSize;
                int end   = Math.Min(start + pageSize, txs.Count);
                for (int i = start; i < end; i++)
                {
                    var t = txs[i];
                    Console.ForegroundColor = t.TransactionType.StartsWith("Deposit") || t.TransactionType == "Transfer In" || t.TransactionType == "Loan Disbursed"
                        ? ConsoleColor.Green : ConsoleColor.Red;
                    Console.WriteLine($"  {t.Date,-20:yyyy-MM-dd HH:mm}  {t.TransactionType,-16}  ₱{t.Amount,10:N2}");
                    Console.ResetColor();
                }
                Console.WriteLine($"\n  Page {page + 1} of {(txs.Count + pageSize - 1) / pageSize}");
                Console.WriteLine("  [N]ext  [P]rev  [Q]uit");
                Console.Write("  > ");
                string? key = Console.ReadLine()?.Trim().ToUpper();
                if (key == "Q" || key == null) break;
                if (key == "N" && end < txs.Count) page++;
                else if (key == "P" && page > 0) page--;
            }
        }

        // ── Mini Statement ───────────────────────────────────────────
        void DoMiniStatement()
        {
            ConsoleHelper.Header("MINI STATEMENT (LAST 5)");
            var acc = _accSvc.GetAccount(ActiveAccountID);
            var txs = _txRepo.GetByAccount(ActiveAccountID, 5);

            Console.WriteLine($"  Account  : {acc?.AccountNumber}  |  Balance: ₱{acc?.Balance:N2}");
            ConsoleHelper.Divider();
            Console.WriteLine($"  {"Date",-20} {"Type",-16} {"Amount",12}");
            ConsoleHelper.Divider();
            if (txs.Count == 0) Console.WriteLine("  No transactions yet.");
            foreach (var t in txs)
            {
                bool credit = t.TransactionType is "Deposit" or "Transfer In" or "Loan Disbursed";
                Console.ForegroundColor = credit ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"  {t.Date,-20:yyyy-MM-dd HH:mm}  {t.TransactionType,-16}  ₱{t.Amount,10:N2}");
                Console.ResetColor();
            }
            ConsoleHelper.Pause();
        }

        // ── Monthly Summary ──────────────────────────────────────────
        void DoMonthlySummary()
        {
            ConsoleHelper.Header("MONTHLY SUMMARY");
            var rows = _txRepo.GetMonthlySummary(ActiveAccountID);
            Console.WriteLine($"  {"Month",-10} {"Deposits",14} {"Withdrawals",14} {"Transfers",14} {"Net",14}");
            ConsoleHelper.Divider(68);
            if (rows.Count == 0) { Console.WriteLine("  No data."); ConsoleHelper.Pause(); return; }
            foreach (var (mo, dep, wit, trf) in rows)
            {
                decimal net = dep - wit - trf;
                Console.ForegroundColor = net >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"  {mo,-10} ₱{dep,12:N2} ₱{wit,12:N2} ₱{trf,12:N2} ₱{net,12:N2}");
                Console.ResetColor();
            }
            ConsoleHelper.Pause();
        }

        // ── PDF ──────────────────────────────────────────────────────
        void DoPrintPDF()
        {
            ConsoleHelper.Header("PRINT STATEMENT (PDF)");
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string fileName = $"Statement_{ActiveAccountID}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string outPath  = System.IO.Path.Combine(desktop, fileName);
            string script   = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                      "generate_transactions_pdf.py");
            Console.WriteLine("  Generating PDF...");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName              = "python",
                    Arguments             = $"\"{script}\" {ActiveAccountID} \"{outPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute       = false,
                    CreateNoWindow        = true
                };
                using var p = Process.Start(psi)!;
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode == 0)
                {
                    ConsoleHelper.ShowSuccess($"PDF saved to Desktop: {fileName}");
                    Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
                }
                else ConsoleHelper.ShowError($"PDF error:\n  {stderr}");
            }
            catch (Exception ex)
            {
                ConsoleHelper.ShowError($"Error: {ex.Message}");
                ConsoleHelper.ShowInfo("Ensure Python + reportlab + pyodbc are installed.");
            }
            ConsoleHelper.Pause();
        }

        // ── Loan ─────────────────────────────────────────────────────
        void DoApplyLoan()
        {
            ConsoleHelper.Header("APPLY FOR LOAN");
            ConsoleHelper.ShowInfo($"Interest: {LoanService.LoanInterestRate * 100:N0}% flat/month | Term: {LoanService.DefaultTermMonths} months");
            decimal principal = InputHelper.GetDecimalInput("  Loan Amount: ₱", 500m);
            decimal total   = principal + principal * LoanService.LoanInterestRate * LoanService.DefaultTermMonths;
            decimal monthly = total / LoanService.DefaultTermMonths;
            Console.WriteLine($"\n  Principal    : ₱{principal:N2}");
            Console.WriteLine($"  Total Due    : ₱{total:N2}");
            Console.WriteLine($"  Monthly Pmt  : ₱{monthly:N2}");
            if (!InputHelper.Confirm("  Proceed?")) { ConsoleHelper.ShowWarning("Cancelled."); ConsoleHelper.Pause(); return; }
            var (ok, msg) = _loanSvc.Apply(ActiveAccountID, principal, _session.UserID);
            if (ok) ConsoleHelper.ShowSuccess(msg); else ConsoleHelper.ShowError(msg);
            ConsoleHelper.Pause();
        }

        void DoPayLoan()
        {
            ConsoleHelper.Header("LOAN PAYMENT");
            var loan = _loanSvc.GetActiveLoan(ActiveAccountID);
            if (loan == null) { ConsoleHelper.ShowInfo("No active loan."); ConsoleHelper.Pause(); return; }
            Console.WriteLine($"  Remaining    : ₱{loan.RemainingBalance:N2}");
            Console.WriteLine($"  Monthly Pmt  : ₱{loan.MonthlyPayment:N2}");
            decimal amount = InputHelper.GetDecimalInput("  Payment Amount: ₱", 1m);
            var (ok, msg) = _loanSvc.Pay(ActiveAccountID, amount, _session.UserID);
            if (ok) ConsoleHelper.ShowSuccess(msg); else ConsoleHelper.ShowError(msg);
            ConsoleHelper.Pause();
        }

        void DoLoanStatus()
        {
            ConsoleHelper.Header("LOAN STATUS");
            var loans = _loanSvc.GetLoans(ActiveAccountID);
            if (loans.Count == 0) { Console.WriteLine("  No loans on record."); ConsoleHelper.Pause(); return; }
            Console.WriteLine($"  {"ID",-5} {"Principal",12} {"Total Due",12} {"Paid",12} {"Remaining",12} {"Status",-10} {"Due Date",-12}");
            ConsoleHelper.Divider(76);
            foreach (var l in loans)
            {
                Console.ForegroundColor = l.Status == "Paid" ? ConsoleColor.Green
                                        : l.Status == "Defaulted" ? ConsoleColor.Red
                                        : ConsoleColor.White;
                Console.WriteLine($"  {l.LoanID,-5} ₱{l.Principal,10:N2} ₱{l.TotalDue,10:N2} " +
                                  $"₱{l.AmountPaid,10:N2} ₱{l.RemainingBalance,10:N2} " +
                                  $"{l.Status,-10} {l.DueDate:yyyy-MM-dd}");
                Console.ResetColor();
            }
            ConsoleHelper.Pause();
        }

        // ── Bill Payment ─────────────────────────────────────────────
        void DoPayBill()
        {
            ConsoleHelper.Header("PAY BILL");
            Console.WriteLine("  Available Billers:");
            for (int i = 0; i < BillPaymentService.Billers.Length; i++)
                Console.WriteLine($"  {i + 1,3}. {BillPaymentService.Billers[i]}");
            int sel = InputHelper.GetMenuChoice(1, BillPaymentService.Billers.Length);
            string biller = BillPaymentService.Billers[sel - 1];
            string refNo  = InputHelper.GetStringInput($"  Reference / Account No. for {biller}: ");
            decimal amount = InputHelper.GetDecimalInput("  Amount: ₱");
            if (!InputHelper.Confirm($"  Pay ₱{amount:N2} to {biller} (Ref: {refNo})?"))
            { ConsoleHelper.ShowWarning("Cancelled."); ConsoleHelper.Pause(); return; }
            var (ok, msg) = _billSvc.Pay(ActiveAccountID, biller, refNo, amount, _session.UserID);
            if (ok) ConsoleHelper.ShowSuccess(msg); else ConsoleHelper.ShowError(msg);
            ConsoleHelper.Pause();
        }

        void DoBillHistory()
        {
            ConsoleHelper.Header("BILL PAYMENT HISTORY");
            var bills = _billSvc.GetHistory(ActiveAccountID);
            Console.WriteLine($"  {"Date",-22} {"Biller",-15} {"Reference",-18} {"Amount",12}");
            ConsoleHelper.Divider(70);
            if (bills.Count == 0) { Console.WriteLine("  No bill payments yet."); ConsoleHelper.Pause(); return; }
            foreach (var (biller, refNo, amount, paidAt) in bills)
                Console.WriteLine($"  {paidAt,-22:yyyy-MM-dd HH:mm} {biller,-15} {refNo,-18} ₱{amount,10:N2}");
            ConsoleHelper.Pause();
        }
    }
}
