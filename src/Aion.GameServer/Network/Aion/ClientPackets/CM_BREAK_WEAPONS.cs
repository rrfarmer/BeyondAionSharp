using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_BREAK_WEAPONS (zdead). Decompound (defuse) a fused weapon at an armsfusion officer. DialogAction/ArmsfusionService/AuditLogger red-tolerated.</summary>
public class CM_BREAK_WEAPONS : AionClientPacket
{
    public CM_BREAK_WEAPONS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    private int npcObjId;
    private int weaponObjId;

    protected override void ReadImpl()
    {
        npcObjId = ReadD();
        weaponObjId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsTargetingNpcWithFunction(npcObjId, DialogAction.DECOMPOUND_WEAPON))
            ArmsfusionService.BreakWeapons(player, weaponObjId);
        else
            AuditLogger.Log(player, "tried to defuse a weapon without targeting an armsfusion officer");
    }
}
