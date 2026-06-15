using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Services.Panesterra;
using Aion.GameServer.Services.Panesterra.Ahserion;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("panesterra_entry_corridor")]
public class PanesterraEntryCorridorAI : GeneralNpcAI
{
    private readonly int relatedFortressId;

    public PanesterraEntryCorridorAI(Npc owner) : base(owner)
    {
        relatedFortressId = GetNpcId() switch
        {
            730946 or 730950 => 10111,
            730947 or 730951 => 10211,
            730948 or 730952 => 10311,
            730949 or 730953 => 10411,
            _ => 0,
        };
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1 && CanTeleport(player))
        {
            // teleportToFortress(player);
            PanesterraService.GetInstance().TeleportToEventLocation(player);
        }
        return true;
    }

    private bool CanTeleport(Player player)
    {
        if (player.GetLevel() < 65)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_TELEPOTER_GAB1_USER04());
            return false;
        }
        // TODO: Remove after event
        if (EventService.GetInstance().GetActiveEvents().Any(
            @event => @event.GetEventTemplate().GetName().Equals("Beyond Aion Birthday Event", System.StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        // Don't allow access if any fortress is under siege
        if (new[] { 10111, 10211, 10311, 10411 }.Any(id => SiegeService.GetInstance().GetSiege(id) != null))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_READY_PANGAEA());
            return false;
        }

        if (SiegeService.GetInstance().GetFortress(relatedFortressId).GetRace() != SiegeRaceExtensions.GetByRace(player.GetRace()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE());
            return false;
        }
        return true;
    }

    private void TeleportToFortress(Player player)
    {
        // TODO
        player.SetPanesterraFaction(PanesterraFactionExtensions.GetByFortressId(relatedFortressId));
    }
}
