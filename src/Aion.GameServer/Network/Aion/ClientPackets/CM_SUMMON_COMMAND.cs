using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Summons;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SUMMON_COMMAND (ATracer). Issues a summon mode command (attack/rest/guard/etc). SummonsService/SummonMode red-tolerated.</summary>
public class CM_SUMMON_COMMAND : AionClientPacket
{
    private int mode;
    private int targetObjId;

    public CM_SUMMON_COMMAND(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        mode = ReadUC();
        ReadD(); // 0
        ReadD(); // 0
        targetObjId = ReadD();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        Summon summon = activePlayer.GetSummon();
        SummonMode? summonMode = SummonModeExtensions.GetSummonModeById(mode);
        if (summon != null && summonMode != null)
        {
            SummonsService.DoMode(summonMode.Value, summon, targetObjId, UnsummonType.COMMAND);
        }
    }
}
