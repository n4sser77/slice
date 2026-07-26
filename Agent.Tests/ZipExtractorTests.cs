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

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("../../extracted-sibling/outside.txt")]
    public async Task ReadAndUnzip_PathTraversalEntry_ThrowsInvalidDataException(string entryName)
    {
        string rootDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string extractDir = Path.Combine(rootDir, "extracted");
        Directory.CreateDirectory(extractDir);

        try
        {
            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry(entryName);
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync("malicious");
            }

            zipStream.Position = 0;

            var extractor = new ZipExtractor();
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => extractor.ReadAndUnzip(zipStream, extractDir));

            Assert.Contains(entryName, exception.Message);
        }
        finally
        {
            if (Directory.Exists(rootDir)) Directory.Delete(rootDir, true);
        }
    }
}
