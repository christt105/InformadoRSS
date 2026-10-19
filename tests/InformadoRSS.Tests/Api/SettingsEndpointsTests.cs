using System.Net;
using System.Text.Json;
using InformadoRSS.Alerts;
using InformadoRSS.Backups;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Sinks.Ntfy;
using InformadoRSS.Storage;
using Microsoft.Extensions.DependencyInjection;
using static InformadoRSS.Tests.Api.ApiTestClient;

namespace InformadoRSS.Tests.Api;

public sealed class SettingsEndpointsTests : IAsyncDisposable
{
    private readonly ApiTestClient _api = new();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private async Task<Dictionary<string, JsonElement>> ListAsync()
    {
        using var response = await _api.GetAsync("/api/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).EnumerateArray().ToDictionary(s => s.GetProperty("key").GetString()!);
    }

    [Fact]
    public async Task DefinedSettingsAreListedBeforeTheyHaveAValue()
    {
        var token = (await ListAsync())[SettingsDiscordTokenProvider.SettingsKey];

        Assert.True(token.GetProperty("secret").GetBoolean());
        Assert.True(token.GetProperty("defined").GetBoolean());
        Assert.False(token.GetProperty("isSet").GetBoolean());
        Assert.Equal("Discord", token.GetProperty("group").GetString());
    }

    [Fact]
    public async Task AlertAndNtfySettingsAreDefined()
    {
        var settings = await ListAsync();

        Assert.Equal("Alerts", settings[AlertSettings.TargetKey].GetProperty("group").GetString());
        Assert.True(settings[AlertSettings.FeedBrokenAfterMinutesKey].GetProperty("defined").GetBoolean());
        Assert.True(settings[BackupSettings.DirectoryKey].GetProperty("defined").GetBoolean());
        Assert.False(settings[NtfySettings.UrlKey].GetProperty("secret").GetBoolean());
        Assert.True(settings[NtfySettings.TokenKey].GetProperty("secret").GetBoolean());
    }

    [Fact]
    public async Task TokenIsWriteOnly()
    {
        using var response = await _api.PutAsync("/api/settings", """{"discord.botToken":"super-secret-token","custom.channelId":"42"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("super-secret-token", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        var settings = await ListAsync();
        var token = settings[SettingsDiscordTokenProvider.SettingsKey];
        Assert.True(token.GetProperty("isSet").GetBoolean());
        Assert.Equal(JsonValueKind.Null, token.GetProperty("value").ValueKind);
        Assert.Equal("42", settings["custom.channelId"].GetProperty("value").GetString());
        Assert.False(settings["custom.channelId"].GetProperty("defined").GetBoolean());
        Assert.Equal(
            "super-secret-token",
            await _api.Services.GetRequiredService<ISettingsStore>().GetAsync(SettingsDiscordTokenProvider.SettingsKey, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UndefinedKeysThatLookLikeCredentialsAreSecret()
    {
        using var response = await _api.PutAsync("/api/settings", """{"ntfy.accessToken":"tk_123"}""");

        var setting = (await ListAsync())["ntfy.accessToken"];
        Assert.True(setting.GetProperty("secret").GetBoolean());
        Assert.Equal(JsonValueKind.Null, setting.GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task NullOrEmptyRemovesAndOmittedKeysAreKept()
    {
        using var first = await _api.PutAsync("/api/settings", """{"a.one":"1","a.two":"2"}""");

        using var second = await _api.PutAsync("/api/settings", """{"a.one":null}""");
        using var third = await _api.PutAsync("/api/settings", """{"a.three":"  "}""");

        var settings = await ListAsync();
        Assert.False(settings.ContainsKey("a.one"));
        Assert.False(settings.ContainsKey("a.three"));
        Assert.Equal("2", settings["a.two"].GetProperty("value").GetString());
    }

    [Theory]
    [InlineData("""{"bad key":"x"}""", "invalid_settings")]
    [InlineData("""{".leading":"x"}""", "invalid_settings")]
    [InlineData("""["not","an","object"]""", "invalid_body")]
    public async Task InvalidBodiesAreRejected(string body, string code)
    {
        using var response = await _api.PutAsync("/api/settings", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, (await ReadAsync(response)).GetProperty("error").GetString());
    }
}
