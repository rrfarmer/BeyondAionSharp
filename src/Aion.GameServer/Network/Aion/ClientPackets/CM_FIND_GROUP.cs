using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Findgroup;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_FIND_GROUP (cura, MrPoke). Find-group / find-instance-group window actions. FindGroupService red-tolerated.</summary>
public class CM_FIND_GROUP : AionClientPacket
{
    /// <summary>
    /// The message is sent to every player who opens the group finder, so it must not be able to grow the packet
    /// beyond what the client reads.
    /// </summary>
    private const int MAX_MESSAGE_LENGTH = 255;

    private int action;
    private int playerOrTeamId;
    private int bannedPlayerId;
    private string message;
    private int groupType;
    private int classId;
    private int level;
    private byte serverId;
    private byte unk1;
    private byte unk2;
    private byte unk3;
    private int instanceMaskId;
    private int minMembers;
    private byte instanceApplicationReply;

    public CM_FIND_GROUP(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        action = ReadUC();

        switch (action)
        {
            case 0: // recruit list
                break;
            case 1: // offer delete
                playerOrTeamId = ReadD();
                serverId = ReadC();
                unk1 = ReadC();
                unk2 = ReadC();
                unk3 = ReadC();
                break;
            case 2: // send offer
                playerOrTeamId = ReadD();
                message = ReadS();
                groupType = ReadUC();
                break;
            case 3: // recruit update
                playerOrTeamId = ReadD();
                serverId = ReadC();
                unk1 = ReadC();
                unk2 = ReadC();
                unk3 = ReadC();
                message = ReadS();
                groupType = ReadUC();
                break;
            case 4: // apply list
                break;
            case 5: // post delete
                playerOrTeamId = ReadD();
                break;
            case 6: // apply create
            case 7: // apply update
                playerOrTeamId = ReadD();
                message = ReadS();
                groupType = ReadUC();
                classId = ReadUC();
                level = ReadUC();
                break;
            case 8: // register InstanceGroup
                instanceMaskId = ReadD();
                ReadUC(); // unk 0
                message = ReadS();// text
                minMembers = ReadUC();// minMembers chosen by writer
                break;
            case 9: // remove instance group
                playerOrTeamId = ReadD();
                instanceMaskId = ReadD();
                break;
            case 10: // show instance groups
                break;
            case 11: // apply for instance group
                playerOrTeamId = ReadD();
                instanceMaskId = ReadD();
                break;
            case 12: // accept/deny instance group applicant
                playerOrTeamId = ReadD();
                instanceApplicationReply = ReadC(); // 1: accept, 0: deny
                break;
            case 13: // triggered every 50s when instance group tab is open or option "Automatic search when the window is closed" is checked
                break;
            case 15: // show instance group member info
                playerOrTeamId = ReadD();
                instanceMaskId = ReadD();
                break;
            case 17:
                playerOrTeamId = ReadD();
                instanceMaskId = ReadD();
                message = ReadS();
                break;
            case 20: // clicked Enter button in Prepare for entry window
                break;
            case 25: // ban from instance group
                playerOrTeamId = ReadD();
                instanceMaskId = ReadD();
                bannedPlayerId = ReadD();
                break;
            default:
                AionLog.For(nameof(CM_FIND_GROUP)).LogWarning("Unknown find group action {Action}", action);
                break;
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        // Upstream truncates the field in place. Here it is read into a local first, because testing the field for
        // null would make it nullable for the whole dispatch below; ReadS never returns null, so the empty default
        // only stands in for an action that read no message, and none of those reach a call that uses one.
        string message = this.message ?? "";
        if (message.Length > MAX_MESSAGE_LENGTH)
        {
            global::Aion.GameServer.Utils.Audit.AuditLogger.Log(player, "sent group recruitment or application message exceeding "
                + MAX_MESSAGE_LENGTH + " characters: " + message);
            message = message.Substring(0, MAX_MESSAGE_LENGTH);
        }

        switch (action)
        {
            case 0:
                FindGroupService.GetInstance().ShowRecruitments(player);
                break;
            case 1:
                FindGroupService.GetInstance().RemoveRecruitment(player, serverId, unk1, unk2, unk3);
                break;
            case 2:
                FindGroupService.GetInstance().AddRecruitment(player, message, groupType);
                break;
            case 3:
                FindGroupService.GetInstance().UpdateRecruitment(player, message, groupType);
                break;
            case 4:
                FindGroupService.GetInstance().ShowApplications(player);
                break;
            case 5:
                FindGroupService.GetInstance().RemoveApplication(player);
                break;
            case 6:
                FindGroupService.GetInstance().AddApplication(player, message, groupType, classId, level);
                break;
            case 7:
                FindGroupService.GetInstance().UpdateApplication(player, message, groupType, classId, level);
                break;
            case 8:
                FindGroupService.GetInstance().RegisterInstanceGroup(player, instanceMaskId, message, minMembers);
                break;
            case 9:
                FindGroupService.GetInstance().RemoveInstanceGroup(player);
                break;
            case 10:
                FindGroupService.GetInstance().ShowInstanceGroups(player, false);
                break;
            case 11:
                FindGroupService.GetInstance().SendInstanceApplication(player, playerOrTeamId);
                break;
            case 12:
                FindGroupService.GetInstance().SendInstanceApplicationResult(player, playerOrTeamId, instanceApplicationReply);
                break;
            case 13:
                FindGroupService.GetInstance().ShowInstanceGroups(player, true);
                break;
            case 15:
                FindGroupService.GetInstance().ShowInstanceGroupMembersInfo(player, playerOrTeamId);
                break;
            case 17:
                FindGroupService.GetInstance().UpdateInstanceGroup(player, message);
                break;
        }
    }
}
