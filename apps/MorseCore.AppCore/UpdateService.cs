using System.Net;
using System.Text.Json;

namespace MorseCore.AppCore;

public sealed record ReleaseUpdate(Version Version, Uri Page);
public sealed class UpdateService(HttpClient client)
{
    public static readonly Uri Endpoint=new("https://api.github.com/repos/mustafamertakceylan/MorseCore/releases/latest");
    public async Task<ReleaseUpdate?> CheckAsync(Version current, CancellationToken token=default)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint);
        request.Headers.UserAgent.ParseAdd("MorseCore/0.4.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version","2022-11-28");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
        if(response.StatusCode==HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        // Do not materialize unbounded response content on application startup.
        using var stream=await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var buffer=new MemoryStream();
        byte[] chunk=new byte[4096];
        int read;
        while((read=await stream.ReadAsync(chunk,timeout.Token).ConfigureAwait(false))!=0)
        {
            if(buffer.Length+read>256*1024) throw new InvalidDataException("Release metadata is too large.");
            buffer.Write(chunk,0,read);
        }
        using var json=JsonDocument.Parse(buffer.ToArray());
        var root=json.RootElement;
        if(root.GetProperty("prerelease").GetBoolean() || root.GetProperty("draft").GetBoolean()) return null;
        string tag=root.GetProperty("tag_name").GetString() ?? "";
        if(!Version.TryParse(tag.TrimStart('v','V'),out var version) || version<=current) return null;
        if(!Uri.TryCreate(root.GetProperty("html_url").GetString(),UriKind.Absolute,out var uri) ||
            uri.Scheme!="https" || uri.Host!="github.com" ||
            !uri.AbsolutePath.StartsWith("/mustafamertakceylan/MorseCore/releases/tag/",StringComparison.Ordinal))
            throw new InvalidDataException("Unexpected release URL.");
        return new(version,uri);
    }
}
