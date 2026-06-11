using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ITEM_USAGE_ANIMATION (ATracer). Broadcasts an item-use cast animation (player/target/item ids, time, end/unk flags). On a timed use it marks the player's using-item. Converges item/skill service usages (ItemActionService, ItemSocketService, PlayerReviveService). AionServerPacket/write* red-tolerated.</summary>
public class SM_ITEM_USAGE_ANIMATION : AionServerPacket
{
    private int playerObjId;
    private int targetObjId;
    private int itemObjId;
    private int itemId;
    private int time;
    private int end;
    private int unk;
    private int unk1;
    private int unk2 = 1;
    private int unk3;

    public SM_ITEM_USAGE_ANIMATION(int playerObjId, int itemObjId, int itemId)
    {
        this.playerObjId = playerObjId;
        this.targetObjId = playerObjId;
        this.itemObjId = itemObjId;
        this.itemId = itemId;
        this.time = 0;
        this.end = 1;
        this.unk3 = 1;
    }

    public SM_ITEM_USAGE_ANIMATION(int playerObjId, int itemObjId, int itemId, int time, int end)
    {
        this.playerObjId = playerObjId;
        this.targetObjId = playerObjId;
        this.itemObjId = itemObjId;
        this.itemId = itemId;
        this.time = time;
        this.end = end;
    }

    public SM_ITEM_USAGE_ANIMATION(int playerObjId, int itemObjId, int itemId, int time, int end, int unk)
    {
        this.playerObjId = playerObjId;
        this.targetObjId = playerObjId;
        this.itemObjId = itemObjId;
        this.itemId = itemId;
        this.time = time;
        this.end = end;
        this.unk3 = unk;
    }

    public SM_ITEM_USAGE_ANIMATION(int playerObjId, int targetObjId, int itemObjId, int itemId, int time, int end, int unk)
    {
        this.playerObjId = playerObjId;
        this.targetObjId = targetObjId;
        this.itemObjId = itemObjId;
        this.itemId = itemId;
        this.time = time;
        this.end = end;
        this.unk3 = unk;
    }

    public SM_ITEM_USAGE_ANIMATION(int playerObjId, int targetObjId, int itemObjId, int itemId, int time, int end, int unk, int unk1, int unk2, int unk3)
    {
        this.playerObjId = playerObjId;
        this.targetObjId = targetObjId;
        this.itemObjId = itemObjId;
        this.itemId = itemId;
        this.time = time;
        this.end = end;
        this.unk = unk;
        this.unk1 = unk1;
        this.unk2 = unk2;
        this.unk3 = unk3;
    }

    protected override void WriteImpl(AionConnection con)
    {
        if (time > 0)
        {
            Player player = World.GetInstance().GetPlayer(playerObjId);
            Item item = player.GetInventory().GetItemByObjId(itemObjId);
            player.SetUsingItem(item);
        }

        WriteD(playerObjId); // player obj id
        WriteD(targetObjId); // target obj id

        WriteD(itemObjId); // itemObjId
        WriteD(itemId); // item id

        WriteD(time); // unk
        WriteC(end); // unk
        WriteC(unk); // unk
        WriteC(unk1);
        WriteC(unk2);// unk
        WriteD(unk3);// mb cd?
    }
}
