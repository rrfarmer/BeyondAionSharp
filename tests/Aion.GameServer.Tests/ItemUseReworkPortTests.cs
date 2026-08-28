using System.Xml.Linq;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Tests;

public sealed class ItemUseReworkPortTests
{
    [Fact]
    public void CastingDelayAttributeBindsWithZeroDefault()
    {
        var template = new ItemTemplate();
        Assert.Equal(0, template.GetCastingDelay());
        template.castingDelay = 3000;
        Assert.Equal(3000, template.GetCastingDelay());
    }

    [Fact]
    public void ItemTypeAndLevelRestrictionsDefaultLikeUpstream()
    {
        var template = new ItemTemplate();
        Assert.Equal(ItemType.NORMAL, template.itemType);
        Assert.Equal(17, template.levelRestrictions.Length);
        Assert.All(template.levelRestrictions, b => Assert.Equal(1, b));
    }

    [Fact]
    public void RealItemDataCarriesCastingDelays()
    {
        var path = RepoFile("game-server", "data", "static_data", "items", "item_templates.xml");
        int count = 0;
        int juicyPepentoDelay = -1;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Contains("casting_delay=\""))
            {
                count++;
                if (line.Contains("id=\"152000065\""))
                {
                    var element = XElement.Parse(TrimToElement(line));
                    juicyPepentoDelay = (int)element.Attribute("casting_delay")!;
                }
            }
        }
        Assert.Equal(6103, count);
        Assert.Equal(3000, juicyPepentoDelay);
    }

    private static string TrimToElement(string line)
    {
        string trimmed = line.Trim();
        return trimmed.EndsWith("/>") ? trimmed : trimmed.TrimEnd('>') + "/>";
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not find repository file", Path.Combine(parts));
    }
}
