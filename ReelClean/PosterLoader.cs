using Avalonia.Media.Imaging;

namespace ReelClean;

public sealed class PosterLoader(HttpClient http)
{
    public async Task<Bitmap?> LoadAsync(string? url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        try
        {
            byte[] bytes = await http.GetByteArrayAsync(uri, cancellationToken);
            return new Bitmap(new MemoryStream(bytes));
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
        {
            return null;
        }
    }
}
