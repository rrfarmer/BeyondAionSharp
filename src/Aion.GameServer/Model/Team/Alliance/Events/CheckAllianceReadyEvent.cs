using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>
/// Java parity: model/team/alliance/events/CheckAllianceReadyEvent (ATracer) : AlwaysTrueTeamEvent. Drives the alliance ready-check
/// state machine and broadcasts SM_ALLIANCE_READY_CHECK per member. TeamCommand SCREAMING (ALLIANCE_CHECKREADY_*). SM_* red-tolerated.
/// </summary>
public class CheckAllianceReadyEvent : AlwaysTrueTeamEvent
{
    private readonly PlayerAlliance alliance;
    private readonly Player player;
    private readonly TeamCommand eventCode;

    public CheckAllianceReadyEvent(PlayerAlliance alliance, Player player, TeamCommand eventCode)
    {
        this.alliance = alliance;
        this.player = player;
        this.eventCode = eventCode;
    }

    public override void HandleEvent()
    {
        int readyStatus = alliance.GetAllianceReadyStatus();
        switch (eventCode)
        {
            case TeamCommand.ALLIANCE_CHECKREADY_CANCEL:
                readyStatus = 0;
                break;
            case TeamCommand.ALLIANCE_CHECKREADY_START:
                readyStatus = alliance.GetOnlineMembers().Count - 1;
                break;
            case TeamCommand.ALLIANCE_CHECKREADY_AUTOCANCEL:
                readyStatus = 0;
                break;
            case TeamCommand.ALLIANCE_CHECKREADY_READY:
            case TeamCommand.ALLIANCE_CHECKREADY_NOTREADY:
                readyStatus -= 1;
                break;
        }
        alliance.SetAllianceReadyStatus(readyStatus);
        alliance.ForEach(member =>
        {
            switch (eventCode)
            {
                case TeamCommand.ALLIANCE_CHECKREADY_CANCEL:
                    PacketSendUtility.SendPacket(member, new SM_ALLIANCE_READY_CHECK(player.GetObjectId(), 0));
                    break;
                case TeamCommand.ALLIANCE_CHECKREADY_START:
                    PacketSendUtility.SendPacket(member, new SM_ALLIANCE_READY_CHECK(player.GetObjectId(), 5));
                    PacketSendUtility.SendPacket(member, new SM_ALLIANCE_READY_CHECK(player.GetObjectId(), 1));
                    break;
                case TeamCommand.ALLIANCE_CHECKREADY_AUTOCANCEL:
                    PacketSendUtility.SendPacket(member, new SM_ALLIANCE_READY_CHECK(player.GetObjectId(), 2));
                    break;
                case TeamCommand.ALLIANCE_CHECKREADY_READY:
                    PacketSendUtility.SendPacket(member, new SM_ALLIANCE_READY_CHECK(player.GetObjectId(), 5));
                    if (alliance.GetAllianceReadyStatus() == 0)
                    {
                        PacketSendUtility.SendPacket(member, new SM_ALLIANCE_READY_CHECK(0, 3));
                    }
                    break;
                case TeamCommand.ALLIANCE_CHECKREADY_NOTREADY:
                    PacketSendUtility.SendPacket(member, new SM_ALLIANCE_READY_CHECK(player.GetObjectId(), 4));
                    if (alliance.GetAllianceReadyStatus() == 0)
                    {
                        PacketSendUtility.SendPacket(member, new SM_ALLIANCE_READY_CHECK(0, 3));
                    }
                    break;
            }
        });
    }
}
