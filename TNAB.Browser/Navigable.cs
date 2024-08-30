using TNAB.Parsers;
using TNAB.Network;
using System.Diagnostics;

namespace TNAB.Browser;

public class Navigable(NetworkManager networkManager)
{
    readonly NetworkManager NetworkManager = networkManager;

    public MarkupDocument ActiveDocument { get; private set; } = new MarkupDocument(null);

    public async Task Navigate(Uri uri)
    {
        OnDocumentLoading(new DocumentLoadingEventArgs(uri));
        var timer = Stopwatch.StartNew();
        var response = await NetworkManager.Get(uri);
        var stream = response.Content.ReadAsStream();
        var htmlParser = new HtmlParser(stream);
        // TODO: Implement style sheet hook here
        htmlParser.Parse();
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
}
