using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GROUP_LOOT (Rhys2002, Sykra). Group-loot roll/distribution entry (group/index/item/corpse/distribution/player/luck).</summary>
public class SM_GROUP_LOOT : AionServerPacket
{
    private readonly int groupId;
    private readonly int index;
    private readonly int itemCount;
    private readonly int itemId;
    private readonly int unk3;
    private readonly int lootCorpseId;
    private readonly int distributionId;
    private readonly int playerId;
    private readonly long luck;

    public SM_GROUP_LOOT(int groupId, int playerId, int itemId, int itemCount, int lootCorpseId, int distributionId, long luck, int index)
    {
        this.groupId = groupId;
        this.index = index;
        this.itemCount = itemCount;
        this.itemId = itemId;
        this.unk3 = 0;
        this.lootCorpseId = lootCorpseId;
        this.distributionId = distributionId;
        this.playerId = playerId;
        this.luck = luck;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(groupId);
        WriteD(index);
        WriteD(itemCount);
        WriteD(itemId);
        WriteC(unk3);
        WriteC(0); // 3.0
        WriteC(0); // 3.5
        WriteD(lootCorpseId);
        WriteC(distributionId);
        WriteD(playerId); // 0 starts the roll option
        WriteD((int)luck);
    }
}
