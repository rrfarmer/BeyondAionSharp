using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_FUSION_WEAPONS (zdead, Wakizashi, Neon). Fuses (compounds) two weapons at an armsfusion officer. ArmsfusionService/DialogAction/AuditLogger red-tolerated.</summary>
public class CM_FUSION_WEAPONS : AionClientPacket
{
    public CM_FUSION_WEAPONS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    private int npcObjId;
    private int mainWeaponObjId;
    private int fuseWeaponObjId;

    protected override void ReadImpl()
    {
        npcObjId = ReadD();
        mainWeaponObjId = ReadD();
        fuseWeaponObjId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsTargetingNpcWithFunction(npcObjId, DialogAction.COMPOUND_WEAPON))
            ArmsfusionService.FusionWeapons(GetConnection().GetActivePlayer(), mainWeaponObjId, fuseWeaponObjId);
        else
            AuditLogger.Log(player, "tried to fuse weapons without targeting an armsfusion officer");
    }
}
