using InformadoRSS.Sinks.Discord;
using InformadoRSS.Storage;
using Microsoft.Extensions.Configuration;

namespace InformadoRSS.Tests.Sinks.Discord;

public sealed class SettingsDiscordTokenProviderTests
{
    private sealed class FakeSettingsStore(string? token) : ISettingsStore
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(key == SettingsDiscordTokenProvider.SettingsKey ? token : null);

        public Task SetAsync(string key, string? value, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, string>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static Task<string?> Resolve(string? setting, string? configured)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [SettingsDiscordTokenProvider.ConfigurationKey] = configured })
            .Build();
        return new SettingsDiscordTokenProvider(new FakeSettingsStore(setting), configuration)
            .GetTokenAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("from-settings", "from-config", "from-settings")]
    [InlineData(null, "from-config", "from-config")]
    [InlineData("  ", "from-config", "from-config")]
    [InlineData("Bot abc", null, "abc")]
    [InlineData(" abc ", null, "abc")]
    [InlineData(null, null, null)]
    [InlineData(null, "", null)]
    public async Task PrefersSettingsOverConfiguration(string? setting, string? configured, string? expected) =>
        Assert.Equal(expected, await Resolve(setting, configured));
}
