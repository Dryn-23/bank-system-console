// ================================================================
//  BankSystem.cs  — fully matched to actual DB schema
//  Tables: Users, Customers, Accounts, Transactions, Trasfer
// ================================================================
//
//  ONE-TIME SETUP — run this SQL once before first use:
//
//      ALTER TABLE Customers
//          ADD UserID INT NULL REFERENCES Users(UserID);
//
//  This links a logged-in User to their Customer + Account rows.
// ================================================================

using Microsoft.Data.SqlClient;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

class BankSystem
{
    static readonly string ConnectionString =
        @"Server=DESKTOP-51HVDT7;Database=BankSystem;Trusted_Connection=True;TrustServerCertificate=True;";

    static int loggedInUserID = -1;
    static string loggedInRole = "";
    static int loggedInAccountID = -1;  // Accounts.AccountID — resolved on login

    // ─────────────────────────────────────────────
    //  ENTRY POINT
    // ─────────────────────────────────────────────
    static void Main()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=================================");
            Console.WriteLine("        BANK SYSTEM MENU         ");
            Console.WriteLine("=================================");
            Console.WriteLine("1. Register User");
            Console.WriteLine("2. Login");
            Console.WriteLine("0. Exit");
            Console.Write("\nSelect: ");

            if (!int.TryParse(Console.ReadLine(), out int choice))
            { ShowError("Invalid input."); Pause(); continue; }

            switch (choice)
            {
                case 1: RegisterUser(); break;
                case 2: Login(); break;
                case 0: return;
                default: ShowError("Invalid option."); Pause(); break;
            }
        }
    }

    // ─────────────────────────────────────────────
    //  REGISTER
    //  Creates: Users row
    //  If Customer: also Customers row + Accounts row
    // ─────────────────────────────────────────────
    static void RegisterUser()
    {
        Console.Clear();
        Console.WriteLine("=== REGISTER USER ===");

        Console.Write("Username                 : ");
        string username = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(username))
        { ShowError("Username cannot be empty."); Pause(); return; }

        Console.Write("Password                 : ");
        string passwordHash = HashPassword(Console.ReadLine() ?? "");

        Console.Write("Role (Customer / Teller) : ");
        string role = Console.ReadLine()?.Trim() ?? "";
        if (role != "Customer" && role != "Teller")
        { ShowError("Role must be 'Customer' or 'Teller'."); Pause(); return; }

        try
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();
            using SqlTransaction tx = conn.BeginTransaction();

            // 1. Insert into Users
            const string insertUser = @"
                INSERT INTO Users (UserName, PasswordHash, Role)
                OUTPUT INSERTED.UserID
                VALUES (@UserName, @PasswordHash, @Role)";

            using SqlCommand cmdUser = new(insertUser, conn, tx);
            cmdUser.Parameters.AddWithValue("@UserName", username);
            cmdUser.Parameters.AddWithValue("@PasswordHash", passwordHash);
            cmdUser.Parameters.AddWithValue("@Role", role);

            object userResult = cmdUser.ExecuteScalar();
            if (userResult == null)
            { ShowError("Failed to create user."); tx.Rollback(); Pause(); return; }

            int newUserID = (int)userResult;

            // 2. Customer-only: Customers row + Accounts row
            if (role == "Customer")
            {
                Console.Write("First Name               : ");
                string firstName = Console.ReadLine()?.Trim() ?? "";

                Console.Write("Last Name                : ");
                string lastName = Console.ReadLine()?.Trim() ?? "";

                Console.Write("Address                  : ");
                string address = Console.ReadLine()?.Trim() ?? "";

                Console.Write("Phone                    : ");
                string phone = Console.ReadLine()?.Trim() ?? "";

                Console.Write("Email                    : ");
                string email = Console.ReadLine()?.Trim() ?? "";

                Console.Write("Account Type (Savings / Checking): ");
                string accountType = Console.ReadLine()?.Trim() ?? "Savings";

                string accountNumber = $"ACC{newUserID:D6}";

                // Insert into Customers (UserID column added via ALTER TABLE)
                const string insertCustomer = @"
                    INSERT INTO Customers (FirstName, LastName, [Address], Phone, Email, UserID)
                    OUTPUT INSERTED.CustomerID
                    VALUES (@FirstName, @LastName, @Address, @Phone, @Email, @UserID)";

                using SqlCommand cmdCust = new(insertCustomer, conn, tx);
                cmdCust.Parameters.AddWithValue("@FirstName", firstName);
                cmdCust.Parameters.AddWithValue("@LastName", lastName);
                cmdCust.Parameters.AddWithValue("@Address", address);
                cmdCust.Parameters.AddWithValue("@Phone", phone);
                cmdCust.Parameters.AddWithValue("@Email", email);
                cmdCust.Parameters.AddWithValue("@UserID", newUserID);

                object custResult = cmdCust.ExecuteScalar();
                if (custResult == null)
                { ShowError("Failed to create customer profile."); tx.Rollback(); Pause(); return; }

                int newCustomerID = (int)custResult;

                // Insert into Accounts — links to CustomerID, not UserID
                const string insertAccount = @"
                    INSERT INTO Accounts (CustomerID, AcountNumber, AccountType, Balance, [Status])
                    VALUES (@CustomerID, @AccountNumber, @AccountType, 0, 'Active')";

                using SqlCommand cmdAcc = new(insertAccount, conn, tx);
                cmdAcc.Parameters.AddWithValue("@CustomerID", newCustomerID);
                cmdAcc.Parameters.AddWithValue("@AccountNumber", accountNumber);
                cmdAcc.Parameters.AddWithValue("@AccountType", accountType);
                cmdAcc.ExecuteNonQuery();
            }

            tx.Commit();
            ShowSuccess($"'{username}' registered successfully as {role}.");
        }
        catch (Exception ex) { ShowError($"Registration error: {ex.Message}"); }

        Pause();
    }

    // ─────────────────────────────────────────────
    //  LOGIN
    //  Reads: Users.UserID, Users.Role
    //  Then resolves: Accounts.AccountID via Customers.UserID
    // ─────────────────────────────────────────────
    static void Login()
    {
        Console.Clear();
        Console.WriteLine("=== LOGIN ===");

        Console.Write("Username : ");
        string username = Console.ReadLine()?.Trim() ?? "";

        Console.Write("Password : ");
        string passwordHash = HashPassword(Console.ReadLine() ?? "");

        // Query uses Users.UserID and Users.PasswordHash
        const string query = @"
            SELECT UserID, Role
            FROM   Users
            WHERE  UserName     = @UserName
              AND  PasswordHash = @PasswordHash";

        try
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();

            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@UserName", username);
            cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);

            using SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            { ShowError("Invalid username or password."); Pause(); return; }

            loggedInUserID = reader.GetInt32(0);
            loggedInRole = reader.GetString(1);
            reader.Close();

            // For Customers: resolve Accounts.AccountID
            // Path: Users.UserID → Customers.UserID → Accounts.CustomerID → Accounts.AccountID
            if (loggedInRole == "Customer")
            {
                const string accQuery = @"
                    SELECT a.AccountID
                    FROM   Accounts  a
                    JOIN   Customers c ON c.CustomerID = a.CustomerID
                    WHERE  c.UserID   = @UserID
                      AND  a.[Status] = 'Active'";

                using SqlCommand accCmd = new(accQuery, conn);
                accCmd.Parameters.AddWithValue("@UserID", loggedInUserID);

                object accResult = accCmd.ExecuteScalar();
                if (accResult == null)
                {
                    ShowError("No active account linked to this user.");
                    Logout(); Pause(); return;
                }
                loggedInAccountID = (int)accResult;
            }

            ShowSuccess($"Login successful! Role: {loggedInRole}");
            Pause();
            ShowMenu();
        }
        catch (Exception ex) { ShowError($"Login error: {ex.Message}"); Pause(); }
    }

    static void ShowMenu()
    {
        if (loggedInRole == "Teller") TellerMenu();
        else if (loggedInRole == "Customer") CustomerMenu();
        else ShowError("Unknown role.");
    }

    // ─────────────────────────────────────────────
    //  TELLER MENU
    // ─────────────────────────────────────────────
    static void TellerMenu()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== TELLER MENU ===");
            Console.WriteLine("1. Register User");
            Console.WriteLine("2. View All Users");
            Console.WriteLine("3. View All Customers");
            Console.WriteLine("4. View All Accounts");
            Console.WriteLine("5. Deactivate Account");
            Console.WriteLine("0. Logout");
            Console.Write("\nSelect: ");

            if (!int.TryParse(Console.ReadLine(), out int choice))
            { ShowError("Invalid input."); Pause(); continue; }

            switch (choice)
            {
                case 1: RegisterUser(); break;
                case 2: ViewUsers(); break;
                case 3: ViewAllCustomers(); break;
                case 4: ViewAllAccounts(); break;
                case 5: DeactivateAccount(); break;
                case 0: Logout(); return;
                default: ShowError("Invalid option."); Pause(); break;
            }
        }
    }

    // Reads: Users.UserID, Users.UserName, Users.Role
    static void ViewUsers()
    {
        Console.Clear();
        Console.WriteLine("=== ALL USERS ===");
        Console.WriteLine($"  {"ID",-5} {"Username",-20} {"Role",-10}");
        Console.WriteLine("  " + new string('-', 38));

        try
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();

            // UserID matches Users.UserID
            using SqlCommand cmd = new(
                "SELECT UserID, UserName, Role FROM Users ORDER BY UserID", conn);
            using SqlDataReader reader = cmd.ExecuteReader();

            bool any = false;
            while (reader.Read())
            {
                any = true;
                Console.WriteLine($"  {reader["UserID"],-5} {reader["UserName"],-20} {reader["Role"],-10}");
            }
            if (!any) Console.WriteLine("  No users found.");
        }
        catch (Exception ex) { ShowError($"Error: {ex.Message}"); }

        Pause();
    }

    // Reads: Customers — all columns
    static void ViewAllCustomers()
    {
        Console.Clear();
        Console.WriteLine("=== ALL CUSTOMERS ===");
        Console.WriteLine($"  {"ID",-5} {"First",-12} {"Last",-12} {"Phone",-15} {"Email",-25} {"Since",-12}");
        Console.WriteLine("  " + new string('-', 83));

        try
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();

            const string query = @"
                SELECT CustomerID, FirstName, LastName, Phone, Email, DateCreated
                FROM   Customers
                ORDER  BY CustomerID";

            using SqlCommand cmd = new(query, conn);
            using SqlDataReader reader = cmd.ExecuteReader();

            bool any = false;
            while (reader.Read())
            {
                any = true;
                Console.WriteLine(
                    $"  {reader["CustomerID"],-5} " +
                    $"{reader["FirstName"],-12} " +
                    $"{reader["LastName"],-12} " +
                    $"{reader["Phone"],-15} " +
                    $"{reader["Email"],-25} " +
                    $"{Convert.ToDateTime(reader["DateCreated"]):yyyy-MM-dd}");
            }
            if (!any) Console.WriteLine("  No customers found.");
        }
        catch (Exception ex) { ShowError($"Error: {ex.Message}"); }

        Pause();
    }

    // Reads: Accounts joined with Customers
    static void ViewAllAccounts()
    {
        Console.Clear();
        Console.WriteLine("=== ALL ACCOUNTS ===");
        Console.WriteLine($"  {"AccID",-6} {"AccNumber",-12} {"Type",-10} {"Balance",12} {"Status",-10} {"Owner",-22}");
        Console.WriteLine("  " + new string('-', 78));

        try
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();

            // Accounts.CustomerID → Customers.CustomerID
            const string query = @"
                SELECT a.AccountID,
                       a.AcountNumber,
                       a.AccountType,
                       a.Balance,
                       a.[Status],
                       c.FirstName + ' ' + c.LastName AS FullName
                FROM   Accounts  a
                JOIN   Customers c ON c.CustomerID = a.CustomerID
                ORDER  BY a.AccountID";

            using SqlCommand cmd = new(query, conn);
            using SqlDataReader reader = cmd.ExecuteReader();

            bool any = false;
            while (reader.Read())
            {
                any = true;
                Console.WriteLine(
                    $"  {reader["AccountID"],-6} " +
                    $"{reader["AcountNumber"],-12} " +
                    $"{reader["AccountType"],-10} " +
                    $"P{Convert.ToDecimal(reader["Balance"]),11:N2} " +
                    $"{reader["Status"],-10} " +
                    $"{reader["FullName"],-22}");
            }
            if (!any) Console.WriteLine("  No accounts found.");
        }
        catch (Exception ex) { ShowError($"Error: {ex.Message}"); }

        Pause();
    }

    // Updates: Accounts.[Status] by Accounts.AccountID
    static void DeactivateAccount()
    {
        Console.Clear();
        Console.WriteLine("=== DEACTIVATE ACCOUNT ===");
        Console.Write("Enter AccountID to deactivate: ");

        if (!int.TryParse(Console.ReadLine(), out int accountID))
        { ShowError("Invalid AccountID."); Pause(); return; }

        try
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();

            using SqlCommand cmd = new(@"
                UPDATE Accounts
                SET    [Status] = 'Inactive'
                WHERE  AccountID = @AccountID", conn);
            cmd.Parameters.AddWithValue("@AccountID", accountID);

            int rows = cmd.ExecuteNonQuery();
            if (rows > 0) ShowSuccess($"Account {accountID} deactivated.");
            else ShowError("Account not found.");
        }
        catch (Exception ex) { ShowError($"Error: {ex.Message}"); }

        Pause();
    }

    // ─────────────────────────────────────────────
    //  CUSTOMER MENU
    // ─────────────────────────────────────────────
    static void CustomerMenu()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== CUSTOMER MENU ===");
            Console.WriteLine("1. Deposit");
            Console.WriteLine("2. Withdraw");
            Console.WriteLine("3. Transfer");
            Console.WriteLine("4. Check Balance");
            Console.WriteLine("5. Transaction History");
            Console.WriteLine("6. Print Transactions to PDF");
            Console.WriteLine("0. Logout");
            Console.Write("\nSelect: ");

            if (!int.TryParse(Console.ReadLine(), out int choice))
            { ShowError("Invalid input."); Pause(); continue; }

            switch (choice)
            {
                case 1: Deposit(); break;
                case 2: Withdraw(); break;
                case 3: Transfer(); break;
                case 4: CheckBalance(); break;
                case 5: ViewTransactions(); break;
                case 6: PrintTransactionsPDF(); break;
                case 0: Logout(); return;
                default: ShowError("Invalid option."); Pause(); break;
            }
        }
    }

    // Updates: Accounts.Balance WHERE Accounts.AccountID
    static void Deposit()
    {
        Console.Clear();
        Console.WriteLine("=== DEPOSIT ===");
        Console.Write("Amount: P");

        if (!decimal.TryParse(Console.ReadLine(), out decimal amount) || amount <= 0)
        { ShowError("Invalid amount."); Pause(); return; }

        try
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();

            // Accounts.AccountID — resolved at login into loggedInAccountID
            using SqlCommand cmd = new(@"
                UPDATE Accounts
                SET    Balance = Balance + @Amount
                WHERE  AccountID = @AccountID", conn);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@AccountID", loggedInAccountID);
            cmd.ExecuteNonQuery();

            LogTransaction(conn, "Deposit", amount);
            ShowSuccess($"Deposited P{amount:N2} successfully.");
        }
        catch (Exception ex) { ShowError($"Deposit error: {ex.Message}"); }

        Pause();
    }

    // Updates: Accounts.Balance WHERE Accounts.AccountID (inside a transaction)
    static void Withdraw()
    {
        Console.Clear();
        Console.WriteLine("=== WITHDRAW ===");
        Console.Write("Amount: P");

        if (!decimal.TryParse(Console.ReadLine(), out decimal amount) || amount <= 0)
        { ShowError("Invalid amount."); Pause(); return; }

        try
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();
            using SqlTransaction tx = conn.BeginTransaction();

            // Read Accounts.Balance WHERE Accounts.AccountID
            using SqlCommand checkCmd = new(
                "SELECT Balance FROM Accounts WHERE AccountID = @AccountID", conn, tx);
            checkCmd.Parameters.AddWithValue("@AccountID", loggedInAccountID);

            object result = checkCmd.ExecuteScalar();
            if (result == null)
            { ShowError("Account not found."); tx.Rollback(); Pause(); return; }

            decimal balance = Convert.ToDecimal(result);
            if (balance < amount)
            { ShowError($"Insufficient funds. Balance: P{balance:N2}"); tx.Rollback(); Pause(); return; }

            // Update Accounts.Balance WHERE Accounts.AccountID
            using SqlCommand updateCmd = new(@"
                UPDATE Accounts
                SET    Balance = Balance - @Amount
                WHERE  AccountID = @AccountID", conn, tx);
            updateCmd.Parameters.AddWithValue("@Amount", amount);
            updateCmd.Parameters.AddWithValue("@AccountID", loggedInAccountID);
            updateCmd.ExecuteNonQuery();

            LogTransaction(conn, "Withdraw", amount, tx);
            tx.Commit();
            ShowSuccess($"Withdrew P{amount:N2} successfully.");
        }
        catch (Exception ex) { ShowError($"Withdraw error: {ex.Message}"); }

        Pause();
    }

    // Updates: Accounts.Balance for two accounts; inserts into Trasfer
    static void Transfer()
    {
        Console.Clear();
        Console.WriteLine("=== TRANSFER ===");
        Console.Write("Destination Account Number: ");
        string destAccNumber = Console.ReadLine()?.Trim() ?? "";

        Console.Write("Amount: P");
        if (!decimal.TryParse(Console.ReadLine(), out decimal amount) || amount <= 0)
        { ShowError("Invalid amount."); Pause(); return; }

        try
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();
            using SqlTransaction tx = conn.BeginTransaction();

            // Resolve destination via Accounts.AcountNumber (note: your column has typo)
            using SqlCommand destCmd = new(@"
                SELECT AccountID FROM Accounts
                WHERE  AcountNumber = @AccNumber
                  AND  [Status]     = 'Active'", conn, tx);
            destCmd.Parameters.AddWithValue("@AccNumber", destAccNumber);

            object destResult = destCmd.ExecuteScalar();
            if (destResult == null)
            { ShowError("Destination account not found or inactive."); tx.Rollback(); Pause(); return; }

            int destAccountID = (int)destResult;
            if (destAccountID == loggedInAccountID)
            { ShowError("Cannot transfer to your own account."); tx.Rollback(); Pause(); return; }

            // Check sender Accounts.Balance
            using SqlCommand balCmd = new(
                "SELECT Balance FROM Accounts WHERE AccountID = @AccountID", conn, tx);
            balCmd.Parameters.AddWithValue("@AccountID", loggedInAccountID);

            decimal senderBalance = Convert.ToDecimal(balCmd.ExecuteScalar());
            if (senderBalance < amount)
            { ShowError($"Insufficient funds. Balance: P{senderBalance:N2}"); tx.Rollback(); Pause(); return; }

            // Debit sender Accounts.Balance
            using SqlCommand debitCmd = new(@"
                UPDATE Accounts
                SET    Balance = Balance - @Amount
                WHERE  AccountID = @AccountID", conn, tx);
            debitCmd.Parameters.AddWithValue("@Amount", amount);
            debitCmd.Parameters.AddWithValue("@AccountID", loggedInAccountID);
            debitCmd.ExecuteNonQuery();

            // Credit receiver Accounts.Balance
            using SqlCommand creditCmd = new(@"
                UPDATE Accounts
                SET    Balance = Balance + @Amount
                WHERE  AccountID = @AccountID", conn, tx);
            creditCmd.Parameters.AddWithValue("@Amount", amount);
            creditCmd.Parameters.AddWithValue("@AccountID", destAccountID);
            creditCmd.ExecuteNonQuery();

            // Insert into Trasfer (Trasfer.FromAccountID, Trasfer.ToAccountID, Trasfer.Amount)
            using SqlCommand transferCmd = new(@"
                INSERT INTO Trasfer (FromAccountID, ToAccountID, Amount)
                VALUES (@From, @To, @Amount)", conn, tx);
            transferCmd.Parameters.AddWithValue("@From", loggedInAccountID);
            transferCmd.Parameters.AddWithValue("@To", destAccountID);
            transferCmd.Parameters.AddWithValue("@Amount", amount);
            transferCmd.ExecuteNonQuery();

            // Log sender's side in Transactions
            LogTransaction(conn, "Transfer Out", amount, tx);

            tx.Commit();
            ShowSuccess($"Transferred P{amount:N2} to account {destAccNumber}.");
        }
        catch (Exception ex) { ShowError($"Transfer error: {ex.Message}"); }

        Pause();
    }

    // Reads: Accounts.Balance, Accounts.AcountNumber, Accounts.AccountType,
    //        Accounts.[Status], Customers.FirstName, Customers.LastName
    //        via Accounts.AccountID and Accounts.CustomerID → Customers.CustomerID
    static void CheckBalance()
    {
        Console.Clear();
        Console.WriteLine("=== ACCOUNT BALANCE ===");

        try
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();

            const string query = @"
                SELECT a.AcountNumber,
                       a.AccountType,
                       a.Balance,
                       a.[Status],
                       c.FirstName + ' ' + c.LastName AS FullName
                FROM   Accounts  a
                JOIN   Customers c ON c.CustomerID = a.CustomerID
                WHERE  a.AccountID = @AccountID";

            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@AccountID", loggedInAccountID);

            using SqlDataReader r = cmd.ExecuteReader();
            if (r.Read())
            {
                Console.WriteLine();
                Console.WriteLine($"  Account Holder  : {r["FullName"]}");
                Console.WriteLine($"  Account Number  : {r["AcountNumber"]}");
                Console.WriteLine($"  Account Type    : {r["AccountType"]}");
                Console.WriteLine($"  Status          : {r["Status"]}");
                Console.WriteLine($"  Balance         : P{Convert.ToDecimal(r["Balance"]):N2}");
            }
            else ShowError("Account not found.");
        }
        catch (Exception ex) { ShowError($"Error: {ex.Message}"); }

        Pause();
    }

    // Reads: Transactions.TransactionType, Transactions.Amount, Transactions.TracsactionDate
    //        WHERE Transactions.AccountID = loggedInAccountID
    static void ViewTransactions()
    {
        Console.Clear();
        Console.WriteLine("=== TRANSACTION HISTORY ===");
        Console.WriteLine($"  {"Date",-20} {"Type",-14} {"Amount",12}");
        Console.WriteLine("  " + new string('-', 48));

        try
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();

            // TransactionType replaces old "Type" column
            // TracsactionDate replaces old "Date" column (keeping your typo)
            // AccountID replaces old "UserID" column
            const string query = @"
                SELECT TransactionType,
                       Amount,
                       TracsactionDate
                FROM   Transactions
                WHERE  AccountID = @AccountID
                ORDER  BY TracsactionDate DESC";

            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@AccountID", loggedInAccountID);

            using SqlDataReader reader = cmd.ExecuteReader();

            bool any = false;
            while (reader.Read())
            {
                any = true;
                Console.WriteLine(
                    $"  {Convert.ToDateTime(reader["TracsactionDate"]),-20:yyyy-MM-dd HH:mm}" +
                    $"  {reader["TransactionType"],-14}" +
                    $"  P{Convert.ToDecimal(reader["Amount"]),10:N2}");
            }
            if (!any) Console.WriteLine("  No transactions yet.");
        }
        catch (Exception ex) { ShowError($"Error: {ex.Message}"); }

        Pause();
    }

    static void PrintTransactionsPDF()
    {
        Console.Clear();
        Console.WriteLine("=== PRINT TRANSACTIONS TO PDF ===");

        // Output path — saves to user's Desktop
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string fileName = $"Transactions_{loggedInAccountID}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        string outputPath = System.IO.Path.Combine(desktop, fileName);

        // Path to your Python script — adjust if needed
        string scriptPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "generate_transactions_pdf.py");

        Console.WriteLine($"\n  Generating PDF...");

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "python",          // or "python3" on some systems
                Arguments = $"\"{scriptPath}\" {loggedInAccountID} \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = Process.Start(psi)!;
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                ShowSuccess($"PDF saved to: {outputPath}");

                // Auto-open the PDF
                Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
            }
            else
            {
                ShowError($"PDF generation failed:\n  {stderr}");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
            Console.WriteLine("\n  Make sure Python is installed and 'generate_transactions_pdf.py'");
            Console.WriteLine("  is in the same folder as BankSystem.exe");
        }

        Pause();
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    // Inserts into Transactions:
    //   AccountID       = loggedInAccountID  (Transactions.AccountID → Accounts.AccountID)
    //   TransactionType = type
    //   Amount          = amount
    //   TracsactionDate = GETDATE() (default, not passed)
    static void LogTransaction(SqlConnection conn, string type, decimal amount,
                               SqlTransaction? tx = null)
    {
        const string query = @"
            INSERT INTO Transactions (AccountID, TransactionType, Amount)
            VALUES (@AccountID, @Type, @Amount)";

        using SqlCommand cmd = tx != null
            ? new(query, conn, tx)
            : new(query, conn);

        cmd.Parameters.AddWithValue("@AccountID", loggedInAccountID);
        cmd.Parameters.AddWithValue("@Type", type);
        cmd.Parameters.AddWithValue("@Amount", amount);
        cmd.ExecuteNonQuery();
    }

    static string HashPassword(string password)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    static void Logout()
    {
        loggedInUserID = -1;
        loggedInRole = "";
        loggedInAccountID = -1;
    }

    static void ShowSuccess(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  [OK]  {msg}");
        Console.ResetColor();
    }

    static void ShowError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n  [ERR] {msg}");
        Console.ResetColor();
    }

    static void Pause()
    {
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
    }
}