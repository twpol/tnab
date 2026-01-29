using TNAB.Parsers;
using TNAB.Network;
using System.Diagnostics;

namespace TNAB.Browser;

public class Navigable(NetworkManager networkManager)
{
    readonly NetworkManager NetworkManager = networkManager;

    public MarkupDocument ActiveDocument { get; private set; } = new MarkupDocument(new Uri("about:blank"));

    public async Task Navigate(Uri uri)
    {
        OnDocumentLoading(new DocumentLoadingEventArgs(uri));
        var timer = Stopwatch.StartNew();
        var loading = 0;
        OnFeatureUsed(new("http.methods.GET"));
        var response = await NetworkManager.Get(uri);
        OnFeatureUsed(new($"http.status.{(int)response.StatusCode}"));
        var stream = response.Content.ReadAsStream();
        var htmlParser = new HtmlParser(uri, stream);
        htmlParser.FeatureUsed += (sender, e) => OnFeatureUsed(new(e.Feature));
        htmlParser.StyleSheet += async (sender, e) =>
        {
            OnResourceLoading(new ResourceLoadingEventArgs(e.Uri));
            var timer = Stopwatch.StartNew();
            loading++;
            try
            {
                if (e.Uri == null && e.Node.Children.Count != 1) return;
                var stream = e.Uri == null ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(e.Node.Children[0].Value ?? "")) : (await NetworkManager.Get(e.Uri)).Content.ReadAsStream();
                var cssParser = new CssParser(e.Uri ?? uri, stream);
                cssParser.FeatureUsed += (sender, e) => OnFeatureUsed(new(e.Feature));
                cssParser.Parse();
                e.Node.Children.Add(new MarkupStyleSheet(cssParser.Root));
            }
            catch
            {
                // It is not safe to let any exceptions out of here -- https://learn.microsoft.com/en-us/dotnet/csharp/modern-events#events-with-async-subscribers
            }
            finally
            {
                loading--;
                timer.Stop();
                OnResourceLoaded(new ResourceLoadedEventArgs(e.Uri, timer.ElapsedMilliseconds));
            }
        };
        htmlParser.Parse();
        while (loading > 0) await Task.Delay(100);
        ActiveDocument = htmlParser.Root;
        timer.Stop();
        OnDocumentLoaded(new DocumentLoadedEventArgs(uri, timer.ElapsedMilliseconds));
    }

    public record DocumentLoadingEventArgs(Uri Uri);
    public event EventHandler<DocumentLoadingEventArgs>? DocumentLoading;
    protected virtual void OnDocumentLoading(DocumentLoadingEventArgs e) => DocumentLoading?.Invoke(this, e);

    public record DocumentLoadedEventArgs(Uri Uri, long DurationMS);
    public event EventHandler<DocumentLoadedEventArgs>? DocumentLoaded;
    protected virtual void OnDocumentLoaded(DocumentLoadedEventArgs e) => DocumentLoaded?.Invoke(this, e);

    public record ResourceLoadingEventArgs(Uri? Uri);
    public event EventHandler<ResourceLoadingEventArgs>? ResourceLoading;
    protected virtual void OnResourceLoading(ResourceLoadingEventArgs e) => ResourceLoading?.Invoke(this, e);

    public record ResourceLoadedEventArgs(Uri? Uri, long DurationMS);
    public event EventHandler<ResourceLoadedEventArgs>? ResourceLoaded;
    protected virtual void OnResourceLoaded(ResourceLoadedEventArgs e) => ResourceLoaded?.Invoke(this, e);

    public record FeatureUsedEventArgs(string Feature);
    public event EventHandler<FeatureUsedEventArgs>? FeatureUsed;
    protected virtual void OnFeatureUsed(FeatureUsedEventArgs e) => FeatureUsed?.Invoke(this, e);
}
