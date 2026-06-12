using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Players;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_REVIVE (ATracer, orz, avol, Simple). Dispatches the chosen revive type to PlayerReviveService. ReviveType/PlayerReviveService red-tolerated.</summary>
public class CM_REVIVE : AionClientPacket
{
    private int reviveId;

    public CM_REVIVE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        reviveId = ReadUC();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();

        if (!activePlayer.IsDead())
            return;

        ReviveType reviveType = ReviveTypeExtensions.GetReviveTypeById(reviveId);

        switch (reviveType)
        {
            case ReviveType.BIND_REVIVE:
            case ReviveType.OBELISK_REVIVE:
                PlayerReviveService.BindRevive(activePlayer);
                break;
            case ReviveType.REBIRTH_REVIVE:
                PlayerReviveService.RebirthRevive(activePlayer);
                break;
            case ReviveType.ITEM_SELF_REVIVE:
                PlayerReviveService.ItemSelfRevive(activePlayer);
                break;
            case ReviveType.SKILL_REVIVE:
                PlayerReviveService.SkillRevive(activePlayer);
                break;
            case ReviveType.KISK_REVIVE:
                PlayerReviveService.KiskRevive(activePlayer);
                break;
            case ReviveType.INSTANCE_REVIVE:
                PlayerReviveService.InstanceRevive(activePlayer);
                break;
        }
    }
}
