using System;
using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CHAT_WINDOW (ginho1, Cheatkiller, Neon). Target chat-info window: group (2)/alliance (3)/no-group (4)/single (1) layouts. PlayerClass.values().length->Enum.GetValues; explicit iterator consumption for captains; getName(true)->GetName(true). PlayerGroup/PlayerAlliance red-tolerated.</summary>
public class SM_CHAT_WINDOW : AionServerPacket
{
    private Player target;
    private bool isGroup;

    public SM_CHAT_WINDOW(Player target, bool isGroup)
    {
        this.target = target;
        this.isGroup = isGroup;
    }

    protected override void WriteImpl(AionConnection con)
    {
        if (target == null)
            return;

        if (isGroup)
        {
            if (target.IsInGroup())
            {
                WriteC(2); // group
                WriteS(target.GetName(true));
                PlayerGroup group = target.GetPlayerGroup();
                WriteD(group.GetTeamId());
                WriteS(group.GetLeader().GetName());

                ICollection<Player> members = group.GetMembers();
                foreach (Player groupMember in members)
                    WriteC(groupMember.GetLevel());

                for (int i = group.Size(); i < 6; i++)
                    WriteC(0);

                foreach (Player groupMember in members)
                    WriteC(groupMember.GetPlayerClass().GetClassId());

                for (int i = group.Size(); i < 6; i++)
                    WriteC(0);
            }
            else if (target.IsInAlliance())
            {
                WriteC(3); // alliance

                PlayerAlliance alliance = target.GetPlayerAlliance();

                WriteS(alliance.GetLeader().GetName());
                WriteD(alliance.GetTeamId());

                ICollection<Player> members = alliance.GetMembers();
                IEnumerator<Player> membersIt = alliance.GetMembers().GetEnumerator();
                string[] capitans = new string[] { "", "", "", "" };
                for (int i = 0; i < capitans.Length; i++)
                {
                    while (membersIt.MoveNext())
                    {
                        Player groupMember = membersIt.Current;
                        if (alliance.IsSomeCaptain(groupMember))
                        {
                            capitans[i] = groupMember.GetName();
                            break;
                        }
                    }
                }
                foreach (string capitan in capitans)
                {
                    WriteS(capitan);
                }
                WriteH(0);
                WriteC(alliance.Size());
                WriteH(alliance.GetMinExpPlayerLevel());// LVL
                WriteH(alliance.GetMaxExpPlayerLevel());
                short[] counts = new short[Enum.GetValues<PlayerClass>().Length];
                foreach (Player groupMember in members)
                {
                    counts[groupMember.GetPlayerClass().GetClassId()]++;
                }
                foreach (short count in counts)
                {
                    WriteH(count);
                }
            }
            else
            {
                WriteC(4); // no group
                WriteS(target.GetName(true));
                WriteD(0); // no group yet
                WriteC(target.GetPlayerClass().GetClassId());
                WriteC(target.GetLevel());
                WriteC(0); // unk
            }
        }
        else
        {
            WriteC(1);
            WriteS(target.GetName(true));
            WriteS(target.GetLegion() != null ? target.GetLegion().GetName() : "");
            WriteC(target.GetLevel());
            WriteH(target.GetPlayerClass().GetClassId());
            WriteS(target.GetCommonData().GetNote());
            WriteD(1); // unk
            WriteC(target.GetAccount().GetMembership()); // vip level
        }
    }
}
