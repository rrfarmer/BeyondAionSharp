using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_LEGION (Simple). Main legion command dispatcher (create/invite/leave/kick/appoint/rank/notice/permissions/level/nickname/dominion). LegionService red-tolerated.</summary>
public class CM_LEGION : AionClientPacket
{
    private int exOpcode;
    private short deputyPermission;
    private short centurionPermission;
    private short legionarPermission;
    private short volunteerPermission;
    private int rank;
    private int legionDominionId;
    private string legionName;
    private string charName;
    private string newNickname;
    private string announcement;
    private string newSelfIntro;

    public CM_LEGION(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        exOpcode = ReadUC();

        switch (exOpcode)
        {
            // Create a legion
            case 0x00:
                ReadD(); // 00 78 19 00 40
                legionName = ReadS();
                break;
            // Invite to legion
            case 0x01:
                ReadD(); // empty
                charName = ReadS();
                break;
            // Leave legion
            case 0x02:
                ReadD(); // empty
                ReadH(); // empty
                break;
            // Kick member from legion
            case 0x04:
                ReadD(); // empty
                charName = ReadS();
                break;
            // Appoint a new Brigade General
            case 0x05:
                ReadD();
                charName = ReadS();
                break;
            // Change rank
            case 0x06:
                rank = ReadD();
                charName = ReadS();
                break;
            // Show current announcement (via /gnotice)
            case 0x07:
            // Refresh legion info
            case 0x08:
                ReadD(); // 0
                ReadH(); // empty
                break;
            // Edit current announcement (from legion window or via /gnotice New text)
            case 0x09:
                ReadD(); // empty or char id?
                announcement = ReadS();
                break;
            // Change self introduction
            case 0x0A:
                ReadD(); // empty char id?
                newSelfIntro = ReadS();
                break;
            // Edit permissions
            case 0x0D:
                deputyPermission = ReadH();
                centurionPermission = ReadH();
                legionarPermission = ReadH();
                volunteerPermission = ReadH();
                break;
            // Level legion up
            case 0x0E:
                ReadD(); // empty
                ReadH(); // empty
                break;
            case 0x0F:
                charName = ReadS();
                newNickname = ReadS();
                break;
            case 0x10: // selected legion dominion
                legionDominionId = ReadD();
                break;
            default:
                NullLoggerFactory.Instance.CreateLogger(nameof(CM_LEGION)).LogWarning("Unknown Legion exOpcode 0x" + exOpcode.ToString("X", CultureInfo.InvariantCulture));
                break;
        }
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        if (activePlayer.IsLegionMember())
        {
            Legion legion = activePlayer.GetLegion();
            if (charName != null)
                charName = Util.ConvertName(charName);
            switch (exOpcode)
            {
                // invite to legion
                case 0x01:
                    LegionService.GetInstance().InvitePlayerToLegion(activePlayer, charName);
                    break;
                // leave legion
                case 0x02:
                    LegionService.GetInstance().LeaveLegion(activePlayer, false);
                    break;
                // kick member
                case 0x04:
                    LegionService.GetInstance().KickMember(activePlayer, charName);
                    break;
                // appoint a new Brigade General
                case 0x05:
                    LegionService.GetInstance().StartBrigadeGeneralChangeProcess(activePlayer, charName);
                    break;
                // change rank
                case 0x06:
                    LegionService.GetInstance().AppointRank(activePlayer, charName, rank);
                    break;
                // show legion notice (from /gnotice chat command)
                case 0x07:
                    {
                        Legion.Announcement currentAnnouncement = legion.GetAnnouncement();
                        if (currentAnnouncement == null)
                            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_NOSET_GUILD_NOTICE());
                        else
                            SendPacket(SM_SYSTEM_MESSAGE.STR_GUILD_NOTICE(currentAnnouncement.Message, ((DateTimeOffset)currentAnnouncement.Time).ToUnixTimeMilliseconds() / 1000));
                    }
                    break;
                // refresh legion info
                case 0x08:
                    SendPacket(new SM_LEGION_INFO(legion));
                    break;
                // edit announcements
                case 0x09:
                    LegionService.GetInstance().ChangeAnnouncement(activePlayer, announcement);
                    break;
                // change self introduction
                case 0x0A:
                    LegionService.GetInstance().ChangeSelfIntro(activePlayer, newSelfIntro);
                    break;
                // edit permissions
                case 0x0D:
                    LegionService.GetInstance().ChangePermissions(activePlayer, deputyPermission, centurionPermission, legionarPermission,
                        volunteerPermission);
                    break;
                // level up legion
                case 0x0E:
                    LegionService.GetInstance().RequestChangeLevel(activePlayer);
                    break;
                // change nickname
                case 0x0F:
                    LegionService.GetInstance().ChangeNickname(activePlayer, charName, newNickname);
                    break;
                // select Legion Dominion to participate
                case 0x10:
                    LegionService.GetInstance().JoinLegionDominion(activePlayer, legionDominionId);
                    break;
            }
        }
        else
        {
            switch (exOpcode)
            {
                case 0x00: // create a legion
                    LegionService.GetInstance().CreateLegion(activePlayer, legionName);
                    break;
            }
        }
    }
}
