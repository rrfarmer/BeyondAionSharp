using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Craft;
using Aion.GameServer.Utils;
using GameServerMain = Aion.GameServer.GameServer;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CRAFT (Mr. Poke). Starts crafting (recipe + materials) at a static object with range/template validation. CraftService/PositionUtil red-tolerated.</summary>
public class CM_CRAFT : AionClientPacket
{
    private int unk;
    private int targetTemplateId;
    private int recipeId;
    private int targetObjId;
    private int craftType;
    private Dictionary<int, long> materialsData = new Dictionary<int, long>();

    public CM_CRAFT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        unk = ReadUC();
        targetTemplateId = ReadD();
        recipeId = ReadD();
        targetObjId = ReadD();
        int materialsCount = ReadUH();
        craftType = ReadUC();
        for (int i = 0; i < materialsCount; i++)
            materialsData[ReadD()] = ReadQ();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        if (player == null || !player.IsSpawned())
            return;
        if (GameServerMain.IsShuttingDownSoon()) // stop crafting to avoid unnecessary material loss
            return;

        // 129 = Morph Substances
        if (unk != 129)
        {
            VisibleObject staticObject = player.GetKnownList().GetObject(targetObjId);
            if (staticObject == null || !PositionUtil.IsInRange(player, staticObject, 10)
                || staticObject.GetObjectTemplate().GetTemplateId() != targetTemplateId)
                return;
        }

        CraftService.StartCrafting(player, recipeId, targetObjId, craftType, materialsData);
    }
}
