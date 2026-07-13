using System.Xml;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.GameServer.Tests;

public sealed class NpcSellingDialogDataTests
{
    private static readonly HashSet<int> UpdatedNpcIds = new()
    {
        203724, 204262, 206333, 206340, 207092, 207093, 278054, 278554, 279048, 279049,
        279052, 279053, 798303, 798304, 798394, 798395, 798399, 798400, 798438, 798441,
        799714, 799715, 800587, 800590, 801511, 830071, 830072, 830073, 830074, 830155,
        830156, 830157, 830158, 830168, 830528, 830530, 830643, 830644, 830645, 830649,
        830650, 830651, 831002, 831003, 831004, 831005, 831006, 831007, 831008, 831009,
        831090, 831092, 831202, 831204, 831207, 831211, 831212, 831227, 831229, 831232,
        831236, 831237, 831336, 831337, 831789, 831790, 831793, 831794, 831855, 831856,
        832264, 832369, 832738, 832739, 832844, 832845, 833463, 833464, 833538
    };

    [Fact]
    public void UpdatedNpcsExposeBuyAndSellFunctionalDialogs()
    {
        var foundNpcIds = new HashSet<int>();
        int? currentNpcId = null;
        using XmlReader reader = XmlReader.Create(RepoFile("game-server", "data", "static_data", "npcs", "npc_templates.xml"));

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "npc_template")
            {
                int npcId = int.Parse(reader.GetAttribute("npc_id")!);
                currentNpcId = UpdatedNpcIds.Contains(npcId) ? npcId : null;
            }
            else if (currentNpcId != null && reader.NodeType == XmlNodeType.Element && reader.Name == "talk_info")
            {
                var talkInfo = new TalkInfo { FuncDialogIdsRaw = reader.GetAttribute("func_dialogs") };
                Assert.Equal(new[] { 2, 3 }, talkInfo.GetFuncDialogIds());
                foundNpcIds.Add(currentNpcId.Value);
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "npc_template")
            {
                currentNpcId = null;
            }
        }

        Assert.True(UpdatedNpcIds.SetEquals(foundNpcIds));
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
