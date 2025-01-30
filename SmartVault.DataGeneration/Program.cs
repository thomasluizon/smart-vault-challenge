using Dapper;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SmartVault.Library;
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
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json").Build();

            string databaseFile = configuration["DatabaseFileName"];

            if (File.Exists(databaseFile))
                File.Delete(databaseFile);

            SQLiteConnection.CreateFile(databaseFile);
            string connectionString = string.Format(configuration["ConnectionStrings:DefaultConnection"] ?? "", databaseFile);

            using var connection = new SQLiteConnection(connectionString);
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                CreateTables(connection, transaction);
                transaction.Commit();
            }

            InsertTestData(connection);
        }

        static void CreateTables(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var files = Directory.GetFiles(@"..\..\..\..\BusinessObjectSchema");

            foreach (var file in files)
            {
                var serializer = new XmlSerializer(typeof(BusinessObject));
                var businessObject = serializer.Deserialize(new StreamReader(file)) as BusinessObject;

                if (businessObject?.Script != null)
                {
                    connection.Execute(businessObject.Script, transaction: transaction);
                }
            }

            connection.Execute("CREATE INDEX idx_document_account ON Document(AccountId);", transaction: transaction);
            connection.Execute("CREATE INDEX idx_user_account ON User(AccountId);", transaction: transaction);
        }

        static void InsertTestData(SQLiteConnection connection)
        {
            string testDocumentContent = "This is my test document\n";
            string testDocumentPath = "TestDoc.txt";

            File.WriteAllText(testDocumentPath, string.Concat(Enumerable.Repeat(testDocumentContent, 100)));

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
