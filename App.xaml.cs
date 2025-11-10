using System;
using System.Windows;
using System.Windows.Threading;
using DotNetEnv;
using KosovaPOS.Database;
using KosovaPOS.Services;

namespace KosovaPOS
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            var logPath = @"C:\Users\Administrator\POS\startup_errors.log";
            
            // Global exception handlers
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = (Exception)args.ExceptionObject;
                var errorMsg = $"Gabim fatal:\n{ex.Message}\n\n{ex.StackTrace}";
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] FATAL: {errorMsg}\n\n");
                MessageBox.Show(errorMsg, "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            
            DispatcherUnhandledException += (s, args) =>
            {
                var errorMsg = $"Gabim:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}";
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] DISPATCHER: {errorMsg}\n\n");
                MessageBox.Show(errorMsg, "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
                Shutdown();
            };
            
            try
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Starting application...\n");
                
                // Load environment variables
                try
                {
                    Env.Load();
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Environment variables loaded\n");
                }
                catch (Exception envEx)
                {
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] .env file not found (optional): {envEx.Message}\n");
                    // .env file is optional
                }
                
                // Ensure database directory exists
                var dbPath = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "./Database/KosovaPOS.db";
                var dbDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(dbPath));
                if (!System.IO.Directory.Exists(dbDirectory))
                {
                    System.IO.Directory.CreateDirectory(dbDirectory);
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Created database directory: {dbDirectory}\n");
                }
                
                // Initialize database
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Initializing database at: {System.IO.Path.GetFullPath(dbPath)}\n");
                using (var context = new POSDbContext())
                {
                    context.Database.EnsureCreated();
                }
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Database initialized\n");
                
                // Initialize services
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Initializing services...\n");
                ServiceLocator.Initialize();
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Services initialized\n");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Startup complete!\n");
            }
            catch (Exception ex)
            {
                var errorMsg = $"Gabim gjatë inicializimit:\n{ex.Message}\n\n{ex.StackTrace}";
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] STARTUP ERROR: {errorMsg}\n\n");
                MessageBox.Show(errorMsg, "Gabim Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
