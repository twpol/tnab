using System.Net;
using System.Net.Http.Headers;

namespace TNAB.Network;

public class NetworkManager
{
    public static async Task<HttpResponseMessage> Get(Uri uri)
    {
        using var client = new HttpClient();
        return uri.Scheme switch
        {
            "file" => GetFile(uri),
            "http" or "https" => await client.GetAsync(uri),
            _ => throw new NotSupportedException($"Unsupported URI scheme: {uri.Scheme}"),
        };
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
}
