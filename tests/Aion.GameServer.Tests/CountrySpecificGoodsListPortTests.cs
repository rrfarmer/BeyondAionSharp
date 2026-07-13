using System.Xml.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders.LoadingUtils;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class CountrySpecificGoodsListPortTests
{
    [Theory]
    [InlineData(1, "usa")]
    [InlineData(2, "europe")]
    [InlineData(4, "japan")]
    [InlineData(5, "china")]
    [InlineData(6, "taiwan")]
    [InlineData(7, "russia")]
    public void CountryCodeSelectsMatchingExistingFile(int countryCode, string region)
    {
        using var temp = new TempDirectory();
        string baseFile = Path.Combine(temp.Path, "goodslists.xml");
        string regionFile = Path.Combine(temp.Path, $"goodslists_{region}.xml");
        File.WriteAllText(baseFile, "<goodslists />");
        File.WriteAllText(regionFile, "<goodslists />");

        Assert.Equal(regionFile, XmlMerger.ApplyCountryOverride(baseFile, countryCode));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(2)]
    public void UnknownOrMissingCountryOverrideFallsBackToBaseFile(int countryCode)
    {
        using var temp = new TempDirectory();
        string baseFile = Path.Combine(temp.Path, "goodslists.xml");
        File.WriteAllText(baseFile, "<goodslists />");

        Assert.Equal(baseFile, XmlMerger.ApplyCountryOverride(baseFile, countryCode));
    }

    [Fact]
    public void MergerImportsConfiguredCountryFileAndTracksItInCacheMetadata()
    {
        using var temp = new TempDirectory();
        string sourceFile = Path.Combine(temp.Path, "static_data.xml");
        string baseFile = Path.Combine(temp.Path, "goodslists.xml");
        string regionFile = Path.Combine(temp.Path, "goodslists_usa.xml");
        string cacheFile = Path.Combine(temp.Path, "cache", "static_data.xml");
        File.WriteAllText(sourceFile, "<static_data><import file=\"goodslists.xml\" /></static_data>");
        File.WriteAllText(baseFile, "<goodslists><list id=\"1\" /></goodslists>");
        File.WriteAllText(regionFile, "<goodslists><list id=\"2\" /></goodslists>");

        int originalCountryCode = GSConfig.SERVER_COUNTRY_CODE;
        try
        {
            GSConfig.SERVER_COUNTRY_CODE = 1;
            XmlMergeResult result = new XmlMerger(sourceFile, cacheFile).Merge();
            var document = XDocument.Load(cacheFile);

            Assert.Equal(regionFile, Assert.Single(result.ImportedFiles));
            Assert.Equal("2", (string?)Assert.Single(document.Descendants("list")).Attribute("id"));
        }
        finally
        {
            GSConfig.SERVER_COUNTRY_CODE = originalCountryCode;
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aion-goodslists-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
