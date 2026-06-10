using System;
using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.DataHolders;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_QUEST_SHARE (ginho1, Neon). Shares a quest with nearby online group/alliance members. Java Predicate.and() chain -> combined lambda; TemporaryPlayerTeam wildcard -> var. QuestService/DataManager/SM_* red-tolerated.</summary>
public class CM_QUEST_SHARE : AionClientPacket
{
    private int questId;

    public CM_QUEST_SHARE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        this.questId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        QuestTemplate questTemplate = DataManager.QUEST_DATA.GetQuestById(questId);
        if (questTemplate == null || questTemplate.IsCannotShare())
        {
            PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(1100001)); // This quest cannot be shared.
            return;
        }

        QuestState questState = player.GetQuestStateList().GetQuestState(questId);
        if (questState == null || questState.GetStatus() == QuestStatus.COMPLETE)
            return;

        List<Player> membersToShareWith;
        var currentGroup = player.GetCurrentGroup();
        if (currentGroup == null)
        {
            membersToShareWith = new List<Player>();
        }
        else
        {
            Predicate<Player> memberFilter = member =>
                Predicates.Players.AllExcept(player)(member)
                && Predicates.Players.ONLINE(member)
                && PositionUtil.IsInRange(member, player, GroupConfig.GROUP_MAX_DISTANCE);
            membersToShareWith = currentGroup.FilterMembers(memberFilter);
        }
        if (membersToShareWith.Count == 0)
        {
            if (questTemplate.GetTarget() == QuestTarget.ALLIANCE)
            {
                PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(1100005)); // There are no Alliance members to share the quest with.
            }
            else
            {
                PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(1100000)); // There are no group members to share the quest with.
            }
            return;
        }

        foreach (Player member in membersToShareWith)
        {
            if (!QuestService.CheckStartConditions(member, questId, false))
            {
                PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(1100003, member.GetName())); // You failed to share the quest with %0.
            }
            else
            {
                PacketSendUtility.SendPacket(member, new SM_QUEST_ACTION(questId, player.GetObjectId(), member.IsInAlliance()));
                PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(1100002, member.GetName())); // You shared the quest with %0.
            }
        }
    }
}
