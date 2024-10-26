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
            "tnab-resource" => GetResource(uri),
            "http" or "https" => await client.GetAsync(uri),
            _ => throw new NotSupportedException($"Unsupported URI scheme: {uri.Scheme}"),
        };
        timer.Stop();
        OnRequestLoaded(new RequestLoadedEventArgs(uri, timer.ElapsedMilliseconds));
        return result;
    }

    static HttpResponseMessage GetResource(Uri uri)
    {
        var assemblyName = uri.Segments.Length > 1 ? uri.Segments[1].TrimEnd('/') : "";
        var resourceName = uri.Segments.Length > 2 ? assemblyName + "." + uri.Segments[2] : "";
        return AppDomain
            .CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name! == assemblyName)?
            .GetManifestResourceStream(resourceName)?
            .GetResponseMessage()
            ?? throw new FileNotFoundException($"Resource not found: {uri}");
    }

    public record RequestLoadingEventArgs(Uri Uri);
    public event EventHandler<RequestLoadingEventArgs>? RequestLoading;
    protected virtual void OnRequestLoading(RequestLoadingEventArgs e) => RequestLoading?.Invoke(this, e);

    public record RequestLoadedEventArgs(Uri Uri, long DurationMS);
    public event EventHandler<RequestLoadedEventArgs>? RequestLoaded;
    protected virtual void OnRequestLoaded(RequestLoadedEventArgs e) => RequestLoaded?.Invoke(this, e);
}

static class StreamExtensions
{
    public static HttpResponseMessage GetResponseMessage(this Stream stream)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return response;
    }
}
