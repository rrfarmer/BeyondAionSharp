using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Restrictions;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_COMPOSITE_STONES (pixfid). Combines two manastones via a combination tool (CompositionAction). PlayerRestrictions/CompositionAction red-tolerated.</summary>
public class CM_COMPOSITE_STONES : AionClientPacket
{
    private int compinationToolItemObjectId;
    private int firstItemObjectId;
    private int secondItemObjectId;

    public CM_COMPOSITE_STONES(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        compinationToolItemObjectId = ReadD();
        firstItemObjectId = ReadD();
        secondItemObjectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;

        if (player.IsProtectionActive())
        {
            player.GetController().StopProtectionActiveTask();
        }

        if (player.IsCasting())
        {
            player.GetController().CancelCurrentSkill(null);
        }

        Item tools = player.GetInventory().GetItemByObjId(compinationToolItemObjectId);
        if (tools == null)
            return;
        Item first = player.GetInventory().GetItemByObjId(firstItemObjectId);
        if (first == null)
            return;
        Item second = player.GetInventory().GetItemByObjId(secondItemObjectId);
        if (second == null)
            return;

        if (!PlayerRestrictions.CanUseItem(player, tools))
            return;

        CompositionAction action = new CompositionAction();

        if (!action.CanAct(player, tools, first, second))
            return;

        action.Act(player, tools, first, second);
    }
}
