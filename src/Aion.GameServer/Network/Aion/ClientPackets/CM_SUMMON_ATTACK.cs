using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SUMMON_ATTACK (ATracer). Orders a summon/mercenary to auto-attack a creature target. Creature/AuditLogger red-tolerated.</summary>
public class CM_SUMMON_ATTACK : AionClientPacket
{
    private int summonObjId;
    private int targetObjId;
    private byte unk1;
    private int time;
    private byte unk3;

    public CM_SUMMON_ATTACK(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        summonObjId = ReadD();
        targetObjId = ReadD();
        unk1 = ReadC();
        time = ReadUH();
        unk3 = ReadC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        Creature summonOrMercenary = player.GetSummonOrMercenary(summonObjId);
        if (summonOrMercenary == null) // commonly due to lags when the pet dies
            return;

        VisibleObject obj = summonOrMercenary.GetKnownList().GetObject(targetObjId); // may be null due to lags during movement
        if (obj is Creature creature)
            summonOrMercenary.GetController().AttackTarget(creature, time, false);
        else if (obj != null) // not a creature (attack should be client restricted)
            AuditLogger.Log(player, "tried to use summon attack on a wrong target: " + obj);
    }
}
