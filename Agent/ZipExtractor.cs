using System.IO.Compression;

public class ZipExtractor
{
    public void ReadAndUnzip(Stream file, string path)
    {
        using (Stream s = file)
        using (ZipArchive archive = new ZipArchive(s))
        {
            foreach (ZipArchiveEntry entry
                    in archive.Entries)
            {
                var filePath =
                    Path.Combine(path,
                            entry.FullName);
                if (!filePath.StartsWith(
                            Path.GetFullPath(path)))
                {
                    continue;
                }

                entry.ExtractToFile(
                        filePath, overwrite: true);
            }
        }
    }
}


