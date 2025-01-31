using Dapper;
using SmartVault.Program.Utils;
using System.Data.SQLite;

namespace SmartVault.Tests
{
    public class FileProcessingTests : IDisposable
    {
        private readonly SQLiteConnection _connection;
        private readonly string _projectRoot;

        public FileProcessingTests()
        {
            _connection = new SQLiteConnection("Data Source=:memory:;Version=3;");
            _connection.Open();

            _connection.Execute(@"CREATE TABLE Document (
                Id INTEGER PRIMARY KEY,
                Name TEXT,
                FilePath TEXT,
                Length INTEGER,
                AccountId INTEGER
            )");

            _projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName;
        }

        [Fact]
        public void Should_Calculate_Total_File_Size_Correctly()
        {
            // Arrange
            string testFilePath = Path.Combine(_projectRoot, "TestFile1.txt");
            File.WriteAllText(testFilePath, new string('A', 100));

            _connection.Execute("INSERT INTO Document (Id, Name, FilePath, Length, AccountId) VALUES (1, 'TestDoc1', @FilePath, 100, 1)",
                new { FilePath = testFilePath });

            // Act
            long totalSize = FileProcessor.GetAllFileSizes(_connection);

            // Assert
            Assert.Equal(100, totalSize);

            // Cleanup
            File.Delete(testFilePath);
        }

        [Fact]
        public void Should_Handle_Missing_Files_When_Calculating_Size()
        {
            // Arrange
            string missingFilePath = Path.Combine(_projectRoot, "MissingFile.txt");

            _connection.Execute("INSERT INTO Document (Id, Name, FilePath, Length, AccountId) VALUES (1, 'MissingDoc', @FilePath, 200, 1)",
                new { FilePath = missingFilePath });

            // Act
            long totalSize = FileProcessor.GetAllFileSizes(_connection);

            // Assert
            Assert.Equal(0, totalSize);
        }

        [Fact]
        public void Should_Handle_Empty_Account_When_Consolidating_Files()
        {
            // Act
            string consolidatedFile = Path.Combine(_projectRoot, "Consolidated_99.txt");
            FileProcessor.WriteEveryThirdFileToFile("99", _connection, _projectRoot);

            // Assert
            Assert.False(File.Exists(consolidatedFile));
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
