using System;
using System.Collections.Generic;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Team.Common.Service;

/// <summary>Java parity: model/team/common/service/PlayerTeamDistributionService (ATracer, nrg). TemporaryPlayerTeam&lt;?&gt;→TemporaryPlayerTeam&lt;TeamMember&lt;Player&gt;&gt;; nested Consumer&lt;Player&gt;→class w/ Accept passed as Action&lt;Player&gt; method group; Math.round(float)→(int)Math.Floor(x+0.5f); long*=float / int*=float lossy→explicit narrowing cast; instanceof→is. StatFunctions/AbyssPointsService/DropRegistrationService/DamageInfo/owner.GetAi red-tolerated.</summary>
public class PlayerTeamDistributionService
{
    /// <summary>This method will send a reward if a player is in a team</summary>
    public static void DoReward(TemporaryPlayerTeam<ITeamMember<Player>> team, float damagePercent, Npc owner, AionObject winner, TeamDamageList teamDamageList)
    {
        // Find team's members and determine highest level
        bool disableRangeChecks = DropConfig.DISABLE_RANGE_CHECK_MAPS.Contains(owner.GetPosition().GetMapId());
        PlayerTeamRewardStats filteredStats = new(owner, disableRangeChecks);
        if (team is PlayerAlliance alli && alli.IsInLeague())
        {
            alli.GetLeague().GetMembers().ForEach(a => a.ForEach(filteredStats.Accept));
        }
        else
        {
            team.ForEach(filteredStats.Accept);
        }

        // All non-mentors are not nearby or dead
        if (filteredStats.players.Count == 0 || !filteredStats.hasLivingPlayer)
        {
            return;
        }

        long expReward = StatFunctions.CalculateExperienceReward(filteredStats.highestLevel, owner);

        float instanceApMultiplier = owner.GetPosition().GetWorldMapInstance().GetInstanceHandler().GetApMultiplier();
        foreach (Player member in filteredStats.players)
        {
            // dead players shouldn't receive AP/EP/DP
            if (member.IsDead())
                continue;

            // Reward init
            long rewardXp = (int)Math.Floor(expReward * member.GetLevel() / (float)filteredStats.partyLvlSum + 0.5f);
            int rewardDp = StatFunctions.CalculateDPReward(member, owner);
            float rewardAp = 1;

            // Players 10 levels below highest member get 0 reward.
            if (filteredStats.highestLevel - member.GetLevel() >= 10)
            {
                rewardXp = 0;
                rewardDp = 0;
            }

            // Dmg percent correction
            rewardXp = (long)(rewardXp * damagePercent);
            rewardDp = (int)(rewardDp * damagePercent);
            rewardAp *= damagePercent;
            rewardAp *= instanceApMultiplier;

            member.GetCommonData().AddExp(rewardXp, Rates.XP_GROUP_HUNTING, owner.GetObjectTemplate().GetL10n());
            member.GetCommonData().AddDp(rewardDp);
            if (owner.GetAi().Ask(AIQuestion.REWARD_AP) && !(filteredStats.mentorCount > 0 && CustomConfig.MENTOR_GROUP_AP))
            {
                rewardAp *= StatFunctions.CalculatePvEApGained(member, owner);
                int ap = (int)rewardAp / filteredStats.players.Count;
                if (ap >= 1)
                {
                    AbyssPointsService.AddAp(member, owner, ap);
                }
            }
        }
        if (owner.GetAi().Ask(AIQuestion.REWARD_LOOT))
        {
            // Give Drop
            DamageInfo<Player> mostDamageMember = teamDamageList.GetMostDamageByTeam(team);
            if (mostDamageMember == null)
            {
                return;
            }
            Player mostDamagePlayer = mostDamageMember.GetAttacker();
            if (mostDamagePlayer.IsMentor())
            {
                foreach (Player member in team.GetMembers())
                {
                    if (member.GetLevel() == filteredStats.highestLevel)
                        mostDamagePlayer = member;
                }
            }
            if (winner.Equals(team) && (filteredStats.mentorCount == 0 || !owner.GetAi().GetName().Equals("chest")))
            {
                DropRegistrationService.GetInstance().RegisterDrop(owner, mostDamagePlayer, filteredStats.highestLevel, filteredStats.players);
            }
        }
    }

    private class PlayerTeamRewardStats
    {
        internal readonly List<Player> players = new();
        internal readonly bool disableRangeChecks;
        internal int partyLvlSum = 0;
        internal int highestLevel = 0;
        internal int mentorCount = 0;
        internal bool hasLivingPlayer = false;
        internal Npc owner;

        public PlayerTeamRewardStats(Npc owner, bool disableRangeChecks)
        {
            this.owner = owner;
            this.disableRangeChecks = disableRangeChecks;
        }

        public void Accept(Player member)
        {
            if (member.IsOnline() && PositionUtil.IsInRange(member, owner, disableRangeChecks ? 9999 : GroupConfig.GROUP_MAX_DISTANCE))
            {
                Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnKill(new QuestEnv(owner, member, 0));

                if (member.IsMentor())
                {
                    mentorCount++;
                }
                else
                {
                    if (!hasLivingPlayer && !member.IsDead())
                        hasLivingPlayer = true;

                    players.Add(member);
                    partyLvlSum += member.GetLevel();
                    if (member.GetLevel() > highestLevel)
                        highestLevel = member.GetLevel();
                }
            }
        }
    }
}
