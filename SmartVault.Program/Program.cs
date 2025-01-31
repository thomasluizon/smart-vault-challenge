using Dapper;
using Microsoft.Extensions.Configuration;
using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace SmartVault.Program
{
    partial class Program
    {
        static void Main(string[] args)
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

            WriteEveryThirdFileToFile(accountId, connection, projectRoot);
            GetAllFileSizes(connection);
        }

        private static void GetAllFileSizes(SQLiteConnection connection)
        {
            Console.WriteLine("Calculating real file sizes...");

            var filePaths = connection.Query<string>("SELECT FilePath FROM Document").ToList();

            if (filePaths.Count == 0)
            {
                Console.WriteLine("No files found in the database.");
                return;
            }

            long totalSize = 0;
            int missingFiles = 0;

            foreach (var filePath in filePaths)
            {
                if (File.Exists(filePath))
                {
                    long fileSize = new FileInfo(filePath).Length;
                    totalSize += fileSize;
                }
                else
                {
                    missingFiles++;
                }
            }

            Console.WriteLine($"Total actual file size: {totalSize} bytes");

            if (missingFiles > 0)
            {
                Console.WriteLine($"Warning: {missingFiles} files were missing and not included in the total size.");
            }
        }

        private static void WriteEveryThirdFileToFile(string accountId, SQLiteConnection connection, string projectRoot)
        {
            var filePaths = connection.Query<string>(
                "SELECT FilePath FROM Document WHERE AccountId = @AccountId",
                new { AccountId = accountId }).ToList();

            if (filePaths.Count == 0)
            {
                Console.WriteLine($"No files found for AccountId {accountId}");
                return;
            }

            Console.WriteLine($"Total files found for AccountId {accountId}: {filePaths.Count}");

            string outputFilePath = Path.Combine(projectRoot, $"Consolidated_{accountId}.txt");
            bool hasMatchingFiles = false;

            using (var writer = new StreamWriter(outputFilePath))
            {
                for (int i = 2; i < filePaths.Count; i += 3)
                {
                    string filePath = filePaths[i];

                    if (!File.Exists(filePath))
                    {
                        Console.WriteLine($"File not found: {filePath}");
                        continue;
                    }

                    string content = File.ReadAllText(filePath);
                    Console.WriteLine($"Checking file: {filePath}");

                    if (content.Contains("Smith Property", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Match found in file: {filePath}");
                        writer.WriteLine($"--- File: {Path.GetFileName(filePath)} ---");
                        writer.WriteLine(content);
                        writer.WriteLine();
                        hasMatchingFiles = true;
                    }
                }
            }

            if (hasMatchingFiles)
            {
                Console.WriteLine($"Consolidated file created: {outputFilePath}");
            }
            else
            {
                Console.WriteLine($"No matching files found for AccountId {accountId}. Consolidated file will be empty.");
            }
        }
    }
}
