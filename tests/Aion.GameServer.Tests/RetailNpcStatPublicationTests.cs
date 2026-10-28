using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatPublicationTests
{
    [Fact]
    public async Task NoConfigurationLeavesTheLegacyPathSelected()
    {
        Assert.False(RetailNpcStatCatalog.Enabled);
        await RetailNpcStatCatalog.InitializeAsync(null, null);
        Assert.False(RetailNpcStatCatalog.Enabled);
        Assert.Empty(RetailNpcStatCatalog.SupportedMaps);
        Assert.False(RetailNpcStatCatalog.TryGet(220010000, 204260, out _));
    }

    [Theory]
    [InlineData(null, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("missing.json", null)]
    public async Task IncompleteConfigurationCannotPublish(string? path, string? pin)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcStatCatalog.InitializeAsync(path, pin));
        Assert.False(RetailNpcStatCatalog.Enabled);
    }

    [Fact]
    public async Task MissingManifestCannotPublish()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcStatCatalog.InitializeAsync(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), new string('a', 64)));
        Assert.False(RetailNpcStatCatalog.Enabled);
    }

    [SkippableFact]
    public async Task ValidProfilesWithoutACompleteCombatConsumerCannotPublish()
    {
        var (bytes, pin, _) = RetailNpcRuntimeGetterTests.ProfileFixture();
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await File.WriteAllBytesAsync(path, bytes);
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcStatCatalog.InitializeAsync(path, pin));
            Assert.Contains("Statistics", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(RetailNpcStatCatalog.Enabled);
            Assert.Empty(RetailNpcStatCatalog.Members);
        }
        finally { File.Delete(path); }
    }
}
