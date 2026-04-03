using System.Net;

namespace Agent.Cli.Utils;

public class ProgressStreamContent : StreamContent
{
    private readonly Stream _content;
    private readonly IProgress<double>? _progress;

    public ProgressStreamContent(Stream content, IProgress<double>? progress = null)
        : base(content)
    {
        _content = content;
        _progress = progress;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        var buffer = new byte[81920];
        long totalBytes = _content.Length;
        long uploadedBytes = 0;
        int bytesRead;

        while ((bytesRead = await _content.ReadAsync(buffer)) > 0)
        {
            await stream.WriteAsync(buffer.AsMemory(0, bytesRead));
            uploadedBytes += bytesRead;
            _progress?.Report((double)uploadedBytes / totalBytes * 100);
        }
    }
}
