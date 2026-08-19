using System.Text;

namespace Agent.Services;

internal static class PrivateAtomicFile
{
  public static Task WriteTextAsync(
      string destination,
      string content,
      CancellationToken cancellationToken = default) =>
      WriteAsync(
          destination,
          (stream, token) => stream.WriteAsync(Encoding.UTF8.GetBytes(content), token).AsTask(),
          cancellationToken);

  public static async Task WriteAsync(
      string destination,
      Func<Stream, CancellationToken, Task> writeContent,
      CancellationToken cancellationToken = default)
  {
    var directory = Path.GetDirectoryName(destination)!;
    var temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
    try
    {
      var options = new FileStreamOptions
      {
        Mode = FileMode.CreateNew,
        Access = FileAccess.Write,
        Share = FileShare.None,
        BufferSize = 4096,
        Options = FileOptions.Asynchronous
      };
      if (!OperatingSystem.IsWindows())
        options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

      await using (var stream = new FileStream(temporary, options))
      {
        SetPrivatePermissions(temporary);
        await writeContent(stream, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
      }

      File.Move(temporary, destination, overwrite: true);
      SetPrivatePermissions(destination);
    }
    finally
    {
      if (File.Exists(temporary))
        File.Delete(temporary);
    }
  }

  private static void SetPrivatePermissions(string path)
  {
    if (!OperatingSystem.IsWindows())
      File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
  }
}
