using System.IO.Compression;

public class ZipExtractor
{
    public async Task ReadAndUnzip(Stream file, string path)
    {
        string fullPath = Path.GetFullPath(path);
        string destinationRoot = Path.EndsInDirectorySeparator(fullPath)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;

        using Stream s = file;
        using ZipArchive archive = new(s);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var filePath = Path.GetFullPath(Path.Combine(fullPath, entry.FullName));
            if (!filePath.StartsWith(destinationRoot, StringComparison.Ordinal))
                throw new InvalidDataException($"ZIP entry '{entry.FullName}' would extract outside target directory.");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await entry.ExtractToFileAsync(filePath, overwrite: true);
        }
    }
}

