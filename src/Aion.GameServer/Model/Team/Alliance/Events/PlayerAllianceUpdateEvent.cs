using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>
/// Java parity: model/team/alliance/events/PlayerAllianceUpdateEvent (ATracer) : AlwaysTrueTeamEvent. Broadcasts a member-info update
/// to all except the subject for movement/update events. PlayerAllianceEvent PascalCase (Movement/Update/UpdateEffects);
/// Predicates.Players.AllExcept. SM_ALLIANCE_MEMBER_INFO red-tolerated.
/// </summary>
public class PlayerAllianceUpdateEvent : AlwaysTrueTeamEvent
{
    private readonly PlayerAlliance alliance;
    private readonly Player player;
    private readonly PlayerAllianceEvent allianceEvent;
    private readonly PlayerAllianceMember updateMember;
    private readonly int slot;

    public PlayerAllianceUpdateEvent(PlayerAlliance alliance, Player player, PlayerAllianceEvent allianceEvent, int slot)
    {
        this.alliance = alliance;
        this.player = player;
        this.allianceEvent = allianceEvent;
        this.updateMember = alliance.GetMember(player.GetObjectId());
        this.slot = slot;
    }

    public PlayerAllianceUpdateEvent(PlayerAlliance alliance, Player player, PlayerAllianceEvent allianceEvent)
        : this(alliance, player, allianceEvent, 0)
    {
    }

    public override void HandleEvent()
    {
        switch (allianceEvent)
        {
            case PlayerAllianceEvent.Movement:
            case PlayerAllianceEvent.Update:
            case PlayerAllianceEvent.UpdateEffects:
                alliance.SendPacket(Predicates.Players.AllExcept(player), new SM_ALLIANCE_MEMBER_INFO(updateMember, allianceEvent, slot));
                break;
            default:
                // Unsupported
                break;
        }
    }
}
