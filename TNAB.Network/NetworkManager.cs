using System.Diagnostics;
namespace TNAB.Network;

public class NetworkManager
{
    public async Task<HttpResponseMessage> Get(Uri uri)
    {
        OnRequestLoading(new RequestLoadingEventArgs(uri));
        var timer = Stopwatch.StartNew();
        using var client = new HttpClient();
        var result = await client.GetAsync(uri);
        timer.Stop();
        OnRequestLoaded(new RequestLoadedEventArgs(uri, timer.ElapsedMilliseconds));
        return result;
    }

    public record RequestLoadingEventArgs(Uri Uri);
    public event EventHandler<RequestLoadingEventArgs>? RequestLoading;
    protected virtual void OnRequestLoading(RequestLoadingEventArgs e) => RequestLoading?.Invoke(this, e);

    public record RequestLoadedEventArgs(Uri Uri, long DurationMS);
    public event EventHandler<RequestLoadedEventArgs>? RequestLoaded;
    protected virtual void OnRequestLoaded(RequestLoadedEventArgs e) => RequestLoaded?.Invoke(this, e);
}
