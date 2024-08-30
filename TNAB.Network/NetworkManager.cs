using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace TNAB.Network;

public class NetworkManager
{
    public async Task<HttpResponseMessage> Get(Uri uri)
    {
        OnRequestLoading(new RequestLoadingEventArgs(uri));
        var timer = Stopwatch.StartNew();
        using var client = new HttpClient();
        var result = uri.Scheme switch
        {
            "file" => GetFile(uri),
            "http" or "https" => await client.GetAsync(uri),
            _ => throw new NotSupportedException($"Unsupported URI scheme: {uri.Scheme}"),
        };
        timer.Stop();
        OnRequestLoaded(new RequestLoadedEventArgs(uri, timer.ElapsedMilliseconds));
        return result;
    }

    static HttpResponseMessage GetFile(Uri uri)
    {
        if (!string.IsNullOrEmpty(uri.Authority)) throw new InvalidOperationException("Remote file: not supported");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(File.OpenRead(uri.LocalPath))
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return response;
    }

    public record RequestLoadingEventArgs(Uri Uri);
    public event EventHandler<RequestLoadingEventArgs>? RequestLoading;
    protected virtual void OnRequestLoading(RequestLoadingEventArgs e) => RequestLoading?.Invoke(this, e);

    public record RequestLoadedEventArgs(Uri Uri, long DurationMS);
    public event EventHandler<RequestLoadedEventArgs>? RequestLoaded;
    protected virtual void OnRequestLoaded(RequestLoadedEventArgs e) => RequestLoaded?.Invoke(this, e);
}
