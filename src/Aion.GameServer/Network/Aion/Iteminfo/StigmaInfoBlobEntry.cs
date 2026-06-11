using System.Collections.Generic;
using Aion.Commons.Nio;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Skillengine.Model;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/StigmaInfoBlobEntry (-Nemesiss-, Rolandas, Neon). Contains stigma info. Stigma/SkillTemplate
/// red-tolerated.
/// </summary>
public class StigmaInfoBlobEntry : ItemBlobEntry
{
    internal StigmaInfoBlobEntry() : base(ItemBlobType.STIGMA_INFO)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        Stigma stigma = ownerItem.GetItemTemplate().GetStigma();
        List<SkillTemplate> firstGroupSkills = stigma.GetGainSkillsByGroup(1);
        List<SkillTemplate> secondGroupSkills = stigma.GetGainSkillsByGroup(2);

        WriteD(buf, firstGroupSkills == null ? 0 : firstGroupSkills[0].GetSkillId()); // group 1 skill id
        WriteD(buf, secondGroupSkills == null ? 0 : secondGroupSkills[0].GetSkillId()); // group 2 skill id
        WriteD(buf, 0); // Shard count in 4.7

        Skip(buf, 192);
        WriteH(buf, 0x1); // unk
        WriteH(buf, 0);
        Skip(buf, 96);
        WriteH(buf, 0); // unk
    }

    public override int GetSize()
    {
        return 306; // 12 + 192 + 4 + 96 + 2
    }
}
