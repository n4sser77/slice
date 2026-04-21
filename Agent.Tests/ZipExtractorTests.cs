using System.IO.Compression;

namespace Agent.Tests;

public class ZipExtractorTests
{
    [Fact]
    public async Task ReadAndUnzip_ShouldExtractFilesToCorrectLocationAsync()
    {
        // ARRANGE
        string uniqueId = Guid.NewGuid().ToString();
        string rootDir = Path.Combine(Path.GetTempPath(), uniqueId);
        string extractDir = Path.Combine(rootDir, "extracted");

        string sourceFile = Path.Combine(rootDir, "test.txt");
        string zipPath = Path.Combine(rootDir, "test.zip");
        string expectedContent = "en test fil";

        Directory.CreateDirectory(rootDir);
        Directory.CreateDirectory(extractDir);
        File.WriteAllText(sourceFile, expectedContent);

        using (var zipStream = new FileStream(zipPath, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(sourceFile, Path.GetFileName(sourceFile));
        }

        ZipExtractor z = new();
        string expectedFilePath = Path.Combine(extractDir, "test.txt");

        // ACT
        using (var zipToRead = new FileStream(zipPath, FileMode.Open, FileAccess.Read))
        {
            // Antar att ReadAndUnzip tar (stream, targetPath)
            await z.ReadAndUnzip(zipToRead, extractDir);
        }

        // ASSERT
        Assert.True(File.Exists(expectedFilePath), "Filen extraherades inte till målmappen.");
        Assert.Equal(expectedContent, File.ReadAllText(expectedFilePath));

        // CLEANUP
        if (Directory.Exists(rootDir)) Directory.Delete(rootDir, true);
    }
}

