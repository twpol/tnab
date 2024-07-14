namespace TNAB.Network;

public class NetworkManager
{
    public static async Task<HttpResponseMessage> Get(Uri uri)
    {
        using var client = new HttpClient();
        return await client.GetAsync(uri);
    }
}
