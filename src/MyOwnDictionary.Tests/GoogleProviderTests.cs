using System.Net;
using MyOwnDictionary.Core;

namespace MyOwnDictionary.Tests;

public class GoogleProviderTests
{
    [Fact]
    public async Task Translate_ParsesResponse_AndRetriesOnce()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueFailure(new HttpRequestException("down"));
        handler.EnqueueJson("""[[["سلام","hello",null,null,1]],null,"en"]""");
        var directory = Path.Combine(Path.GetTempPath(), "mydict-tests", Guid.NewGuid().ToString("N"));
        var settings = new SettingsStore(Path.Combine(directory, "settings.json"));
        var provider = new GoogleFreeTranslationProvider(new HttpClient(handler), settings);

        var translation = await provider.TranslateAsync("hello", Language.En, Language.Fa, CancellationToken.None);

        Assert.Equal("سلام", translation);
        Assert.Equal(2, handler.Calls);
        Assert.Contains("translate.googleapis.com", handler.Urls[0], StringComparison.Ordinal);
        Assert.Contains("client=gtx", handler.Urls[0], StringComparison.Ordinal);
        Assert.Contains("sl=en", handler.Urls[0], StringComparison.Ordinal);
        Assert.Contains("tl=fa", handler.Urls[0], StringComparison.Ordinal);
        Assert.Contains("q=hello", handler.Urls[0], StringComparison.Ordinal);
        Assert.Contains("clients5.google.com", handler.Urls[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelledRequest_IsNotRetried()
    {
        var handler = new ScriptedHandler();
        var directory = Path.Combine(Path.GetTempPath(), "mydict-tests", Guid.NewGuid().ToString("N"));
        var settings = new SettingsStore(Path.Combine(directory, "settings.json"));
        var provider = new GoogleFreeTranslationProvider(new HttpClient(handler), settings);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.TranslateAsync("hello", Language.En, Language.Fa, cts.Token));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void BuildUrl_UsesPersianCodes()
    {
        var url = GoogleFreeTranslationProvider.BuildUrl("سلام دنیا", Language.Fa, Language.En);
        Assert.Contains("sl=fa", url, StringComparison.Ordinal);
        Assert.Contains("tl=en", url, StringComparison.Ordinal);
        Assert.Contains("client=gtx", url, StringComparison.Ordinal);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<CancellationToken, HttpResponseMessage>> _steps = new();
        public int Calls { get; private set; }
        public List<string> Urls { get; } = [];

        public void EnqueueFailure(Exception exception) => _steps.Enqueue(_ => throw exception);

        public void EnqueueJson(string json) => _steps.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(_steps.Dequeue()(cancellationToken));
        }
    }
}
