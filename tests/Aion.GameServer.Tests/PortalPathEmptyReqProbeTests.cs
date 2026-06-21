using System.Collections;
using System.Reflection;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.Templates.Portal;

namespace Aion.GameServer.Tests;

/// <summary>
/// Pins down the "cannot enter Haramel — BLOCKED by CheckQuests" bug: a portal path with no &lt;quest_req&gt;
/// in the XML must NOT come back with a non-null quest-requirement list (Java/JAXB leaves it null).
/// </summary>
public sealed class PortalPathEmptyReqProbeTests
{
    [Fact]
    public void HaramelEntrancePath_HasNoQuestOrItemRequirement()
    {
        var path = ResolveStaticDataFile("portals", "portal_template2.xml");
        var data = JaxbHolderLoader.LoadFromFile<Portal2Data>(path);

        PortalPath haramel = FirstPathFor(data, 730319); // Haramel Asmo entrance
        Assert.NotNull(haramel);

        // Java parity: getQuestReq()/getItemReq() are null when the element is absent, so CheckQuests/
        // CheckAndRemoveRequiredItems treat the path as having no requirement and allow entry.
        Assert.Null(haramel!.GetQuestReq());
        Assert.Null(haramel.GetItemReq());
    }

    private static PortalPath FirstPathFor(Portal2Data data, int npcId)
    {
        var field = typeof(Portal2Data).GetField("portalUses", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (IDictionary)field.GetValue(data)!;
        var portalUse = dict[npcId]!;
        var paths = (IList)portalUse.GetType().GetMethod("GetPortalPaths")!.Invoke(portalUse, null)!;
        return (PortalPath)paths[0]!;
    }

    private static string ResolveStaticDataFile(params string[] relativeUnderStaticData)
    {
        var directory = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = System.IO.Path.Combine(
                new[] { directory.FullName, "game-server", "data", "static_data" }
                    .Concat(relativeUnderStaticData).ToArray());
            if (System.IO.File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new System.IO.FileNotFoundException($"Could not locate static_data/{string.Join('/', relativeUnderStaticData)}");
    }
}
