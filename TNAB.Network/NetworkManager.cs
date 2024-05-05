namespace TNAB.Network;

public class NetworkManager
{
    public static async Task<Stream> Get(Uri uri)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(uri);
        return response.Content.ReadAsStream();
    }
}
