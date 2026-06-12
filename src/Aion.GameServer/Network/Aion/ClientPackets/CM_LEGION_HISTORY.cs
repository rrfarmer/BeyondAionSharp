using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Type = global::Aion.GameServer.Model.Team.Legion.LegionHistoryAction.Type;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_LEGION_HISTORY (Simple, xTz, Sykra). Requests a page of legion history of a given type (REWARD restricted to brigade general). LegionHistoryAction.Type aliased; ordinal index via Enum.GetValues. SM_LEGION_HISTORY red-tolerated.</summary>
public class CM_LEGION_HISTORY : AionClientPacket
{
    private int page;
    private Type type;

    public CM_LEGION_HISTORY(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        page = ReadD();
        type = Enum.GetValues<Type>()[ReadUC()];
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.GetLegion() == null)
            return;
        if (type == Type.REWARD && !player.GetLegionMember().IsBrigadeGeneral())
            return;
        SendPacket(new SM_LEGION_HISTORY(player.GetLegion().GetHistory(type), page, type));
    }
}
