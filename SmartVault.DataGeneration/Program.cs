using Dapper;
using Microsoft.Extensions.Configuration;
using SmartVault.CodeGeneration;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace SmartVault.DataGeneration
{
    partial class Program
    {
        static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            string databaseFile = configuration["DatabaseFileName"];

            Console.WriteLine($"Database file path: {databaseFile}");

            if (File.Exists(databaseFile))
            {
                Console.WriteLine("Database already exists. Deleting...");
                File.Delete(databaseFile);
            }

            Console.WriteLine("Creating new database...");
            SQLiteConnection.CreateFile(databaseFile);
            Console.WriteLine("Database created successfully.");

            string connectionString = configuration["ConnectionStrings:DefaultConnection"];
            Console.WriteLine($"Using connection string: {connectionString}");

            using var connection = new SQLiteConnection(connectionString);
            connection.Open();
            Console.WriteLine("Database connection opened.");

            using (var transaction = connection.BeginTransaction())
            {
                Console.WriteLine("Creating tables...");
                CreateTables(connection, transaction);
                transaction.Commit();
                Console.WriteLine("Tables created successfully.");
            }

            Console.WriteLine("Inserting test data...");
            InsertTestData(connection);
            Console.WriteLine("Test data inserted successfully.");

            Console.WriteLine("Database setup complete.");
        }



        static void CreateTables(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var files = Directory.GetFiles(@"..\..\..\..\BusinessObjectSchema");

            if (files.Length == 0)
            {
                Console.WriteLine("No schema files found in BusinessObjects directory.");
                return;
            }

            foreach (var file in files)
            {
                Console.WriteLine($"Processing schema file: {file}");

                var serializer = new XmlSerializer(typeof(BusinessObject));
                using var reader = new StreamReader(file);
                var businessObject = serializer.Deserialize(reader) as BusinessObject;

                if (businessObject?.Script != null)
                {
                    Console.WriteLine($"Executing SQL script from {file}");

                    try
                    {
                        connection.Execute(businessObject.Script, transaction: transaction);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error executing script from {file}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"Invalid schema file: {file}");
                }
            }

            var tables = connection.Query<string>("SELECT name FROM sqlite_master WHERE type='table';").ToList();
            Console.WriteLine("Database tables created:");
            foreach (var table in tables)
            {
                Console.WriteLine($"- {table}");
            }
        }


        static void InsertTestData(SQLiteConnection connection)
        {
            string baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName;
            string testDocumentPath = Path.Combine(baseDirectory, "TestDoc.txt");

            Console.WriteLine($"Creating test document at: {testDocumentPath}");

            var lines = new List<string>
            {
                "This is my test document",
                "Some important data here",
                "Another random line",
                "Smith Property",
                "More text that doesn't contain it",
                "Yet another piece of content",
                "Smith Property appears again later in the document",
                "Final line of the document"
            };

            File.WriteAllText(testDocumentPath, string.Join("\n", lines));

            int documentNumber = 0;

            using (var transaction = connection.BeginTransaction())
            {
                for (int i = 0; i < 100; i++)
                {
                    string randomDate = GetRandomDate().ToString("yyyy-MM-dd");

                    connection.Execute($"INSERT INTO User (Id, FirstName, LastName, DateOfBirth, AccountId, Username, Password) VALUES('{i}', 'FName{i}', 'LName{i}', '{randomDate}', '{i}', 'UserName-{i}', 'e10adc3949ba59abbe56e057f20f883e')", transaction: transaction);
                    connection.Execute($"INSERT INTO Account (Id, Name) VALUES('{i}', 'Account{i}')", transaction: transaction);

                    var documentPath = new FileInfo(testDocumentPath).FullName;
                    var documentList = new List<object>();

                    for (int d = 0; d < 10000; d++, documentNumber++)
                    {
                        documentList.Add(new
                        {
                            Id = documentNumber,
                            Name = $"Document{i}-{d}.txt",
                            FilePath = documentPath,
                            Length = new FileInfo(documentPath).Length,
                            AccountId = i
                        });
                    }

                    connection.Execute("INSERT INTO Document (Id, Name, FilePath, Length, AccountId) VALUES (@Id, @Name, @FilePath, @Length, @AccountId)", documentList, transaction: transaction);
                }

                transaction.Commit();
            }

            Console.WriteLine($"Test document successfully created: {testDocumentPath}");
            PrintSummary(connection);
        }

        static void PrintSummary(SQLiteConnection connection)
        {
            var accountData = connection.Query<int>("SELECT COUNT(*) FROM Account;").AsList()[0];
            Console.WriteLine($"AccountCount: {accountData}");

            var documentData = connection.Query<int>("SELECT COUNT(*) FROM Document;").AsList()[0];
            Console.WriteLine($"DocumentCount: {documentData}");

            var userData = connection.Query<int>("SELECT COUNT(*) FROM User;").AsList()[0];
            Console.WriteLine($"UserCount: {userData}");
        }

        static DateTime GetRandomDate()
        {
            var gen = new Random();
            var start = new DateTime(1985, 1, 1);
            int range = (DateTime.Today - start).Days;
            return start.AddDays(gen.Next(range));
        }
    }
}
