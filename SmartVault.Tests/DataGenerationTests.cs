using Dapper;
using System.Data.SQLite;

namespace SmartVault.Tests
{
    public class DataGenerationTests : IDisposable
    {
        private readonly string _databaseFile;
        private readonly SQLiteConnection _connection;

        public DataGenerationTests()
        {
            string projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName;
            _databaseFile = Path.Combine(projectRoot, "testdb.sqlite");

            if (File.Exists(_databaseFile))
                File.Delete(_databaseFile);

            DataGeneration.Program.Main([]);

            _connection = new SQLiteConnection($"Data Source={_databaseFile};Version=3;");
            _connection.Open();
        }

        [Fact]
        public void Should_Create_Database_File()
        {
            // Assert
            Assert.True(File.Exists(_databaseFile), "Database file was not created.");
        }

        [Fact]
        public void Should_Create_Tables()
        {
            // Act
            var tables = _connection.Query<string>("SELECT name FROM sqlite_master WHERE type='table';");

            // Assert
            Assert.Contains("Account", tables);
            Assert.Contains("Document", tables);
            Assert.Contains("User", tables);
        }

        [Fact]
        public void Should_Insert_Test_Data_Correctly()
        {
            // Act
            int accountCount = _connection.QuerySingle<int>("SELECT COUNT(*) FROM Account;");
            int userCount = _connection.QuerySingle<int>("SELECT COUNT(*) FROM User;");
            int documentCount = _connection.QuerySingle<int>("SELECT COUNT(*) FROM Document;");

            // Assert
            Assert.Equal(100, accountCount);
            Assert.Equal(100, userCount);
            Assert.True(documentCount > 0, "No documents were inserted.");
        }

        [Fact]
        public void Should_Contain_Correct_Initial_Records()
        {
            // Act
            var sampleUser = _connection.QueryFirstOrDefault<dynamic>("SELECT * FROM User LIMIT 1;");
            var sampleAccount = _connection.QueryFirstOrDefault<dynamic>("SELECT * FROM Account LIMIT 1;");
            var sampleDocument = _connection.QueryFirstOrDefault<dynamic>("SELECT * FROM Document LIMIT 1;");

            // Assert
            Assert.NotNull(sampleUser);
            Assert.NotNull(sampleAccount);
            Assert.NotNull(sampleDocument);

            Assert.StartsWith("FName", sampleUser.FirstName);
            Assert.StartsWith("Account", sampleAccount.Name);
            Assert.StartsWith("Document", sampleDocument.Name);
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();

            if (File.Exists(_databaseFile))
                File.Delete(_databaseFile);

            GC.SuppressFinalize(this);
        }
    }
}
