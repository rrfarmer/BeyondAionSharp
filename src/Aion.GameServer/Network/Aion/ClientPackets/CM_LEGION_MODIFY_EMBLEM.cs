using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_LEGION_MODIFY_EMBLEM (Simple, cura, Neon). Stores legion emblem id/color/type. LegionEmblemType/LegionService red-tolerated.</summary>
public class CM_LEGION_MODIFY_EMBLEM : AionClientPacket
{
    private int legionId;
    private int emblemId;
    private int alpha;
    private int red;
    private int green;
    private int blue;
    private LegionEmblemType emblemType;

    public CM_LEGION_MODIFY_EMBLEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        legionId = ReadD();
        emblemId = ReadUC();
        emblemType = (ReadUC() == LegionEmblemType.DEFAULT.GetValue()) ? LegionEmblemType.DEFAULT : LegionEmblemType.CUSTOM;
        alpha = ReadUC();
        red = ReadUC();
        green = ReadUC();
        blue = ReadUC();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        if (activePlayer.IsLegionMember() && activePlayer.GetLegion().GetLegionId() == legionId)
            LegionService.GetInstance().StoreLegionEmblem(activePlayer, emblemId, alpha, red, green, blue, emblemType);
    }
}
