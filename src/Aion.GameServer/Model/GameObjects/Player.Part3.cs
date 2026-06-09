using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Model.GameObjects.Player;

/// <summary>
/// Java parity: model/gameobjects/player/Player — partial #3 (Java lines ~863-1209): enemy/pvp relations,
/// FFA/panesterra, canSee, tribe, summon/kisk, item cooldowns, resurrection/fly-before-death,
/// alliance/team accessors, portal/craft cooldowns, postman/account, quest-complete, casting override,
/// hit-time boost, chain skills, last-counter-skill.
/// </summary>
public partial class Player
{
    public override bool IsEnemy(Creature creature)
    {
        return creature.IsEnemyFrom(this) || IsEnemyFrom(creature);
    }

    public override bool IsEnemyFrom(Npc enemy)
    {
        switch (enemy.GetTypeValue(this))
        {
            case Aion.GameServer.Ai.AIQuestion.AGGRESSIVE:
            case Aion.GameServer.Ai.AIQuestion.ATTACKABLE:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Player enemies:<br/>
    /// - different race<br/>
    /// - duel partner<br/>
    /// - in pvp zone
    /// </summary>
    public override bool IsEnemyFrom(Player enemy)
    {
        if (Equals(enemy))
            return false;
        if (IsInCustomState(Aion.GameServer.Model.GameObjects.Player.CustomPlayerState.ENEMY_OF_ALL_PLAYERS) || enemy.IsInCustomState(Aion.GameServer.Model.GameObjects.Player.CustomPlayerState.ENEMY_OF_ALL_PLAYERS))
        {
            return !isInFfaTeamMode || !enemy.IsInFfaTeamMode() || !IsInSameTeam(enemy);
        }
        return CanPvP(enemy) || IsDueling(enemy);
    }

    public bool IsAggroIconTo(Player enemy)
    {
        if (IsInCustomState(Aion.GameServer.Model.GameObjects.Player.CustomPlayerState.ENEMY_OF_ALL_PLAYERS) || enemy.IsInCustomState(Aion.GameServer.Model.GameObjects.Player.CustomPlayerState.ENEMY_OF_ALL_PLAYERS))
        {
            return !isInFfaTeamMode || !enemy.IsInFfaTeamMode() || !IsInSameTeam(enemy);
        }
        return IsHostileInPanesterra(enemy) || enemy.GetRace() != GetRace();
    }

    public void SetInFfaTeamMode(bool isInFfaTeamMode)
    {
        this.isInFfaTeamMode = isInFfaTeamMode;
    }

    public bool IsInFfaTeamMode()
    {
        return isInFfaTeamMode;
    }

    private bool IsHostileInPanesterra(Player enemy)
    {
        if (panesterraFaction != null && Aion.GameServer.World.WorldMapType.IsPanesterraMap(GetWorldId()))
        {
            return panesterraFaction != enemy.GetPanesterraFaction();
        }
        return false;
    }

    private bool CanPvP(Player enemy)
    {
        int worldId = enemy.GetWorldId();
        if (enemy.GetRace() != GetRace() || IsHostileInPanesterra(enemy))
        {
            return IsInsidePvPZone() && enemy.IsInsidePvPZone();
        }
        else if (worldId == 110010000 || worldId == 120010000 || IsInInstance())
        {
            return IsInsideZoneType(Aion.GameServer.World.Zone.ZoneType.PVP) && enemy.IsInsideZoneType(Aion.GameServer.World.Zone.ZoneType.PVP) && !IsInSameTeam(enemy);
        }
        return false;
    }

    public bool IsDueling(Creature creature)
    {
        return creature.GetMaster() is Player master && Aion.GameServer.Services.DuelService.GetInstance().IsDueling(master, this);
    }

    public bool IsInSameTeam(Player player)
    {
        int teamId = GetCurrentTeamId();
        return teamId != 0 && teamId == player.GetCurrentTeamId();
    }

    public override bool CanSee(VisibleObject obj)
    {
        if (base.CanSee(obj))
            return true;

        if (obj is Creature creature)
        {
            if (creature.GetMaster() is Player player) // player or a summon's master
            {
                if (IsInSameTeam(player) && !IsDueling(player))
                    return true;
            }
            // invisible kisks can be seen from players of the same race
            return obj is Kisk kisk && kisk.GetOwnerRace() == GetRace();
        }

        return false;
    }

    public override Aion.GameServer.Model.TribeClass GetTribe()
    {
        Aion.GameServer.Model.TribeClass? transformTribe = GetTransformModel().GetTribe();
        if (transformTribe != null)
        {
            return transformTribe.Value;
        }
        return GetRace() == Aion.GameServer.Model.Race.ELYOS ? Aion.GameServer.Model.TribeClass.PC : Aion.GameServer.Model.TribeClass.PC_DARK;
    }

    public override Aion.GameServer.Model.TribeClass GetBaseTribe()
    {
        Aion.GameServer.Model.TribeClass? transformTribe = GetTransformModel().GetTribe();
        if (transformTribe != null)
        {
            return Aion.GameServer.Dataholders.DataManager.TRIBE_RELATIONS_DATA.GetBaseTribe(transformTribe.Value);
        }
        return GetTribe();
    }

    public Summon GetSummon()
    {
        return summon;
    }

    public void SetSummon(Summon summon)
    {
        this.summon = summon;
    }

    public Creature GetSummonOrMercenary(int objectId)
    {
        if (summon != null && summon.GetObjectId() == objectId)
            return summon;
        if (GetKnownList().GetObject(objectId) is Npc npc && npc.GetCreatorId() == GetObjectId() && npc.GetNpcTemplateType() == Aion.GameServer.Model.Templates.Npc.NpcTemplateType.MERCENARY)
            return npc;
        return null;
    }

    /// <param name="newKisk">kisk to bind to (null if unbinding)</param>
    public void SetKisk(Kisk newKisk)
    {
        this.kisk = newKisk;
    }

    public Kisk GetKisk()
    {
        return kisk;
    }

    public bool HasCooldown(Item item)
    {
        Aion.GameServer.Model.Templates.Item.ItemUseLimits limits = item.GetItemTemplate().GetUseLimits();
        if (limits == null)
            return false;

        long reuseTime = GetItemReuseTime(limits.GetDelayId());
        if (reuseTime == 0)
            return false;

        if (reuseTime <= System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            itemCoolDowns.Remove(limits.GetDelayId());
            return false;
        }
        return true;
    }

    public long GetItemReuseTime(int delayId)
    {
        itemCoolDowns.TryGetValue(delayId, out Aion.GameServer.Model.Items.ItemCooldown cd);
        return cd == null ? 0 : cd.GetReuseTime();
    }

    public IDictionary<int, Aion.GameServer.Model.Items.ItemCooldown> GetItemCoolDowns()
    {
        return itemCoolDowns;
    }

    public void AddItemCoolDown(int delayId, long time, int useDelay)
    {
        itemCoolDowns[delayId] = new Aion.GameServer.Model.Items.ItemCooldown(time, useDelay);
    }

    public void RemoveItemCoolDown(int delayId)
    {
        itemCoolDowns.Remove(delayId);
    }

    public void SetPlayerResActivate(bool isActivated)
    {
        this.isResByPlayer = isActivated;
    }

    public bool GetResStatus()
    {
        return isResByPlayer;
    }

    public int GetResurrectionSkill()
    {
        return resurrectionSkill;
    }

    public void SetResurrectionSkill(int resurrectionSkill)
    {
        this.resurrectionSkill = resurrectionSkill;
    }

    public void SetIsFlyingBeforeDeath(bool isActivated)
    {
        this.isFlyingBeforeDeath = isActivated;
    }

    public bool GetIsFlyingBeforeDeath()
    {
        return isFlyingBeforeDeath;
    }

    public Aion.GameServer.Model.Team.Alliance.PlayerAlliance GetPlayerAlliance()
    {
        return playerAllianceGroup != null ? playerAllianceGroup.GetAlliance() : null;
    }

    public Aion.GameServer.Model.Team.Alliance.PlayerAllianceGroup GetPlayerAllianceGroup()
    {
        return playerAllianceGroup;
    }

    public bool IsInAlliance()
    {
        return playerAllianceGroup != null;
    }

    public void SetPlayerAllianceGroup(Aion.GameServer.Model.Team.Alliance.PlayerAllianceGroup playerAllianceGroup)
    {
        this.playerAllianceGroup = playerAllianceGroup;
    }

    public bool IsInLeague()
    {
        return IsInAlliance() && GetPlayerAlliance().IsInLeague();
    }

    public bool IsInTeam()
    {
        return IsInGroup() || IsInAlliance();
    }

    /// <summary>current PlayerGroup, PlayerAlliance or null</summary>
    public Aion.GameServer.Model.Team.TemporaryPlayerTeam<Aion.GameServer.Model.Team.TeamMember<Player>> GetCurrentTeam()
    {
        return IsInGroup() ? GetPlayerGroup() : GetPlayerAlliance();
    }

    /// <summary>current PlayerGroup, PlayerAllianceGroup or null</summary>
    public Aion.GameServer.Model.Team.TemporaryPlayerTeam<Aion.GameServer.Model.Team.TeamMember<Player>> GetCurrentGroup()
    {
        return IsInGroup() ? GetPlayerGroup() : GetPlayerAllianceGroup();
    }

    /// <summary>current team id, 0 if not in a team</summary>
    public int GetCurrentTeamId()
    {
        Aion.GameServer.Model.Team.TemporaryPlayerTeam<Aion.GameServer.Model.Team.TeamMember<Player>> team = GetCurrentTeam();
        return team == null ? 0 : team.GetTeamId();
    }

    public Aion.GameServer.Model.GameObjects.Player.PortalCooldownList GetPortalCooldownList()
    {
        return portalCooldownList;
    }

    public Aion.GameServer.Model.GameObjects.Player.Cooldowns GetCraftCooldowns()
    {
        return craftCooldowns;
    }

    public Aion.GameServer.Model.GameObjects.Player.Cooldowns GetHouseObjectCooldowns()
    {
        return houseObjectCooldowns;
    }

    public Npc GetPostman()
    {
        return postman;
    }

    public void SetPostman(Npc postman)
    {
        this.postman = postman;
    }

    public Aion.GameServer.Model.Account.PlayerAccountData GetAccountData()
    {
        return playerAccountData;
    }

    public Aion.GameServer.Model.Account.Account GetAccount()
    {
        return playerAccount;
    }

    public System.DateTime GetCreationDate()
    {
        return playerAccountData.GetCreationDate();
    }

    /// <summary>Quest completion</summary>
    public bool IsCompleteQuest(int questId)
    {
        Aion.GameServer.Questengine.Model.QuestState qs = GetQuestStateList().GetQuestState(questId);
        return qs != null && qs.GetStatus() == Aion.GameServer.Questengine.Model.QuestStatus.COMPLETE;
    }

    public long GetNextSkillUse()
    {
        return nextSkillUse;
    }

    public void SetNextSkillUse(long nextSkillUse)
    {
        this.nextSkillUse = nextSkillUse;
    }

    public override void SetCasting(Skill castingSkill)
    {
        Skill lastSkillObj = GetCastingSkill();
        base.SetCasting(castingSkill);
        if (lastSkillObj != null)
            this.lastSkill = lastSkillObj.GetSkillTemplate();
    }

    public SkillTemplate GetLastSkill()
    {
        return lastSkill;
    }

    public bool IsHitTimeBoosted()
    {
        return IsHitTimeBoosted(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public bool IsHitTimeBoosted(long timeMillis)
    {
        return timeMillis <= hitTimeBoostExpireTimeMillis;
    }

    public float GetHitTimeBoostCastSpeed()
    {
        return hitTimeBoostCastSpeed;
    }

    public void SetHitTimeBoost(long expireTimeMillis, float castSpeed)
    {
        hitTimeBoostExpireTimeMillis = expireTimeMillis;
        hitTimeBoostCastSpeed = castSpeed;
    }

    /// <summary>chain skills</summary>
    public Aion.GameServer.SkillEngine.Model.ChainSkills GetChainSkills()
    {
        if (chainSkills == null)
            chainSkills = new Aion.GameServer.SkillEngine.Model.ChainSkills();
        return chainSkills;
    }

    public void SetLastCounterSkill(Aion.GameServer.SkillEngine.Model.AttackStatus status)
    {
        Aion.GameServer.SkillEngine.Model.AttackStatus result = Aion.GameServer.SkillEngine.Model.AttackStatusExtensions.GetBaseStatus(status);

        switch (result)
        {
            case Aion.GameServer.SkillEngine.Model.AttackStatus.DODGE:
            case Aion.GameServer.SkillEngine.Model.AttackStatus.PARRY:
            case Aion.GameServer.SkillEngine.Model.AttackStatus.BLOCK:
            case Aion.GameServer.SkillEngine.Model.AttackStatus.RESIST:
                lastCounterSkill[result] = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                break;
        }
    }

    public long GetLastCounterSkill(Aion.GameServer.SkillEngine.Model.AttackStatus status)
    {
        if (!lastCounterSkill.TryGetValue(status, out long t))
            return 0;

        return t;
    }
}
