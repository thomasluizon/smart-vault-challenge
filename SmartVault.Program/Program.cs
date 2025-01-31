using Microsoft.Extensions.Configuration;
using SmartVault.Program.Utils;
using System;
using System.Data.SQLite;
using System.IO;

namespace SmartVault.Program
{
    public partial class Program
    {
        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            string projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName;

            string databaseFile = Path.Combine(projectRoot, configuration["DatabaseFileName"]);
            string connectionString = configuration["ConnectionStrings:DefaultConnection"].Replace("{DatabaseFilePath}", databaseFile);

            Console.WriteLine($"Database file path: {databaseFile}");

            if (!File.Exists(databaseFile))
            {
                Console.WriteLine("Error: Database file not found. Run SmartVault.DataGeneration first.");
                return;
            }

            using var connection = new SQLiteConnection(connectionString);
            connection.Open();
            Console.WriteLine("Database connection opened.");

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: SmartVault.Program <accountId>");
                return;
            }

            string accountId = args[0];

            FileProcessor.WriteEveryThirdFileToFile(accountId, connection, projectRoot);
            FileProcessor.GetAllFileSizes(connection);
        }
    }
}
