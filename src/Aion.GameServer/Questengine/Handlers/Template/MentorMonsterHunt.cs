using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Questengine.Handlers.Models;
using Aion.GameServer.Questengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Questengine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/MentorMonsterHunt (MrPoke, Bobobear, Pad) : MonsterHunt. switch on QuestMentorType MENTOR/MENTE; super.onKillEvent→base.OnKillEvent; DataManager/PlayerGroup/PositionUtil/GroupConfig red-tolerated.</summary>
public class MentorMonsterHunt : MonsterHunt
{
    private int menteMinLevel;
    private int menteMaxLevel;

    public MentorMonsterHunt(int questId, List<int> startNpcIds, List<int> endNpcIds, List<Monster> monsters,
        int menteMinLevel, int menteMaxLevel, bool reward, bool rewardNextStep)
        : base(questId, startNpcIds, endNpcIds, monsters, 0, 0, null, 0, null, 0, reward, rewardNextStep)
    {
        this.menteMinLevel = menteMinLevel;
        this.menteMaxLevel = menteMaxLevel;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            switch (DataManager.QUEST_DATA.GetQuestById(questId).GetMentorType())
            {
                case QuestMentorType.MENTOR:
                    if (player.IsMentor())
                    {
                        PlayerGroup group = player.GetPlayerGroup();
                        foreach (Player member in group.GetMembers())
                        {
                            if (member.GetLevel() >= menteMinLevel && member.GetLevel() <= menteMaxLevel
                                && PositionUtil.IsInRange(player, member, GroupConfig.GROUP_MAX_DISTANCE))
                            {
                                return base.OnKillEvent(env);
                            }
                        }
                    }
                    break;
                case QuestMentorType.MENTE:
                    if (player.IsInGroup())
                    {
                        PlayerGroup group = player.GetPlayerGroup();
                        foreach (Player member in group.GetMembers())
                        {
                            if (member.IsMentor() && PositionUtil.IsInRange(player, member, GroupConfig.GROUP_MAX_DISTANCE))
                                return base.OnKillEvent(env);
                        }
                    }
                    break;
            }
        }
        return false;
    }
}
