using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CUBE_UPDATE (Sweetkr). Updates cube/warehouse/legion-warehouse size + expansion counts (action 0) or advanced stigma slots (action 6). Static factories stigmaSlots/cubeSize; switch-on-StorageType; type.ordinal()->(int)type. Converges ItemSplitService SM_CUBE_UPDATE.cubeSize. AionServerPacket/write* red-tolerated.</summary>
public class SM_CUBE_UPDATE : AionServerPacket
{
    private int action;
    /// <summary>
    /// for action 0 - its storage type; for action 6 - its advanced stigma count
    /// </summary>
    private int actionValue;

    private int itemsCount;
    private int npcExpands;
    private int questExpands;
    private int itemExpands;

    public static SM_CUBE_UPDATE StigmaSlots(int slots)
    {
        return new SM_CUBE_UPDATE(6, slots);
    }

    public static SM_CUBE_UPDATE CubeSize(StorageType type, Player player)
    {
        int itemsCount = 0;
        int npcExpands = 0;
        int questExpands = 0;
        int itemExpands = 0;
        switch (type)
        {
            case StorageType.CUBE:
                itemsCount = player.GetInventory().Size();
                npcExpands = player.GetNpcExpands();
                questExpands = player.GetQuestExpands();
                itemExpands = player.GetItemExpands();
                break;
            case StorageType.REGULAR_WAREHOUSE:
                itemsCount = player.GetWarehouse().Size();
                npcExpands = player.GetWhNpcExpands();
                questExpands = player.GetWhBonusExpands();
                break;
            case StorageType.LEGION_WAREHOUSE:
                itemsCount = player.GetLegion().GetLegionWarehouse().Size();
                npcExpands = player.GetLegion().GetWarehouseExpansions();
                break;
        }

        return new SM_CUBE_UPDATE(0, type.GetId(), itemsCount, npcExpands, questExpands, itemExpands);
    }

    private SM_CUBE_UPDATE(int action, int actionValue, int itemsCount, int npcExpands, int questExpands, int itemExpands)
        : this(action, actionValue)
    {
        this.itemsCount = itemsCount;
        this.npcExpands = npcExpands;
        this.questExpands = questExpands;
        this.itemExpands = itemExpands;
    }

    private SM_CUBE_UPDATE(int action, int actionValue)
    {
        this.action = action;
        this.actionValue = actionValue;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(action);
        WriteC(actionValue);
        switch (action)
        {
            case 0:
                WriteD(itemsCount);
                WriteC(npcExpands); // cube size from npc (so max 5 for now)
                WriteC(questExpands); // cube size from quest (so max 2 for now)
                WriteC(itemExpands); // count of used items (tickets)
                break;
        }
    }
}
