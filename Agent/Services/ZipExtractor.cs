using System.IO.Compression;

public class ZipExtractor
{
    public async Task ReadAndUnzip(Stream file, string path)
    {
        string fullPath = Path.GetFullPath(path);
        using (Stream s = file)
        using (ZipArchive archive = new ZipArchive(s))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var filePath = Path.GetFullPath(Path.Combine(fullPath, entry.FullName));
                if (!filePath.StartsWith(fullPath)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                await entry.ExtractToFileAsync(filePath, overwrite: true);
            }
        }
    }
}


