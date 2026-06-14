using System;
using System.Collections.Generic;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/PlayerController extends CreatureController&lt;Player&gt;.</summary>
public class PlayerController : CreatureController<Player>
{
    private static readonly ILogger log = NullLogger.Instance;
    private long lastAttackMillis = 0;
    private long lastAttackedMillis = 0;
    private StanceObserver stanceObserver;

    private static long CurrentTimeMillis() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public override void See(VisibleObject obj)
    {
        base.See(obj);
        if (obj is Creature creature)
        {
            if (creature is Npc npc)
            {
                PacketSendUtility.SendPacket(GetOwner(), new SM_NPC_INFO(npc, GetOwner()));
                if (npc is Aion.GameServer.Model.GameObjects.Kisk)
                {
                    if (GetOwner().GetRace() == ((Aion.GameServer.Model.GameObjects.Kisk)npc).GetOwnerRace())
                        PacketSendUtility.SendPacket(GetOwner(), new SM_KISK_UPDATE((Aion.GameServer.Model.GameObjects.Kisk)npc));
                }
                else
                {
                    Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnAtDistance(new Aion.GameServer.QuestEngine.Model.QuestEnv(npc, GetOwner(), 0));
                }
                Aion.GameServer.Services.Drop.DropService.GetInstance().See(GetOwner(), npc);
            }
            else if (creature is Player player)
            {
                SendPlayerInfoPackets(player);
            }
            else if (creature is Summon)
            {
                PacketSendUtility.SendPacket(GetOwner(), new SM_NPC_INFO((Summon)creature, GetOwner()));
            }
            if (!creature.GetEffectController().IsEmpty())
                PacketSendUtility.SendPacket(GetOwner(), new SM_ABNORMAL_EFFECT(creature));
        }
        else if (obj is Gatherable || obj is StaticObject)
        {
            PacketSendUtility.SendPacket(GetOwner(), new SM_GATHERABLE_INFO(obj));
        }
        else if (obj is Pet pet)
        {
            PacketSendUtility.SendPacket(GetOwner(), new SM_PET(pet));
            if (pet.GetMaster().IsInFlyingState())
                PacketSendUtility.SendPacket(GetOwner(), new SM_PET_EMOTE(pet, Aion.GameServer.Model.GameObjects.PetEmote.FLY_START));
        }
        else if (obj is Aion.GameServer.Model.House.House)
        {
            PacketSendUtility.SendPacket(GetOwner(), new SM_HOUSE_RENDER((Aion.GameServer.Model.House.House)obj));
        }
        else if (obj is Aion.GameServer.Model.GameObjects.HouseObject)
        {
            PacketSendUtility.SendPacket(GetOwner(), new SM_HOUSE_OBJECT((Aion.GameServer.Model.GameObjects.HouseObject)obj));
        }
    }

    private void SendPlayerInfoPackets(Player player)
    {
        PacketSendUtility.SendPacket(GetOwner(), new SM_PLAYER_INFO(player, !player.Equals(GetOwner()) && GetOwner().IsAggroIconTo(player)));
        PacketSendUtility.SendPacket(GetOwner(), new SM_MOTION(player.GetObjectId(), player.GetMotions().GetActiveMotions()));
        if (player.IsInPlayerMode(Aion.GameServer.Model.Actions.PlayerMode.RIDE))
            PacketSendUtility.SendPacket(GetOwner(), new SM_EMOTION(player, EmotionType.RIDE, 0, player.ride.GetNpcId()));
        if (player.GetController().IsUnderStance())
            PacketSendUtility.SendPacket(GetOwner(), new SM_PLAYER_STANCE(player, 1));
    }

    public override void NotSee(VisibleObject obj, Aion.GameServer.Model.Animations.ObjectDeleteAnimation animation)
    {
        base.NotSee(obj, animation);
        if (!GetOwner().IsSpawned()) // player is teleporting, no need to send deletion packets
            return;
        if (obj is Pet)
        {
            PacketSendUtility.SendPacket(GetOwner(), new SM_PET(obj.GetObjectId(), animation));
        }
        else if (obj is Aion.GameServer.Model.House.House)
        {
            PacketSendUtility.SendPacket(GetOwner(), new SM_DELETE_HOUSE(((Aion.GameServer.Model.House.House)obj).GetAddress().GetId()));
        }
        else if (obj is Aion.GameServer.Model.GameObjects.HouseObject)
        {
            PacketSendUtility.SendPacket(GetOwner(), new SM_DELETE_HOUSE_OBJECT(obj.GetObjectId()));
        }
        else if (obj is Npc && ((Npc)obj).IsFlag())
        {
            PacketSendUtility.SendPacket(GetOwner(), new SM_DELETE(obj, Aion.GameServer.Model.Animations.ObjectDeleteAnimation.DELAYED));
        }
        else
        {
            PacketSendUtility.SendPacket(GetOwner(), new SM_DELETE(obj, animation));
        }
    }

    public override void OnTargetChanged(VisibleObject oldTarget, VisibleObject newTarget)
    {
        base.OnTargetChanged(oldTarget, newTarget);
        PacketSendUtility.SendPacket(GetOwner(), new SM_TARGET_SELECTED(newTarget));
        PacketSendUtility.BroadcastToSightedPlayers(GetOwner(), new SM_TARGET_UPDATE(GetOwner()));
    }

    public override void OnHide()
    {
        base.OnHide();
        Aion.GameServer.Services.DuelService.GetInstance().FixTeamVisibility(GetOwner());
    }

    public override void OnHideEnd()
    {
        Pet pet = GetOwner().GetPet();
        if (pet != null && !PositionUtil.IsInRange(GetOwner(), pet, 3)) // client sends pet position only every 50m...
            pet.GetPosition().SetXYZH(GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), GetOwner().GetHeading());
        base.OnHideEnd();
    }

    public void UpdateNearbyQuests()
    {
        Dictionary<int, int> nearbyQuestList = new Dictionary<int, int>();
        foreach (int questId in GetOwner().GetPosition().GetMapRegion().GetParent().GetQuestIds())
        {
            if (Aion.GameServer.Services.QuestService.CheckStartConditions(GetOwner(), questId, false, 2, false, false, false))
                nearbyQuestList[questId] = Aion.GameServer.Services.QuestService.GetLevelRequirementDiff(questId, GetOwner().GetCommonData().GetLevel());
        }
        PacketSendUtility.SendPacket(GetOwner(), new SM_NEARBY_QUESTS(nearbyQuestList));
    }

    public void UpdateRepeatableQuests()
    {
        List<int> reapeatQuestList = new List<int>();
        foreach (int questId in GetOwner().GetPosition().GetMapRegion().GetParent().GetQuestIds())
        {
            Aion.GameServer.Model.Templates.QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
            if (!template.IsTimeBased())
                continue;
            if (Aion.GameServer.Services.QuestService.CheckStartConditions(GetOwner(), questId, false))
                reapeatQuestList.Add(questId);
        }
        if (reapeatQuestList.Count > 0)
            PacketSendUtility.SendPacket(GetOwner(), new SM_QUEST_REPEAT(reapeatQuestList));
    }

    public override void OnEnterZone(Aion.GameServer.World.Zone.ZoneInstance zone)
    {
        Player player = GetOwner();
        if (!zone.CanRide() && player.IsInPlayerMode(Aion.GameServer.Model.Actions.PlayerMode.RIDE))
            player.UnsetPlayerMode(Aion.GameServer.Model.Actions.PlayerMode.RIDE);
        Aion.GameServer.Services.ConquerorAndProtectorSystem.ConquerorAndProtectorService.GetInstance().OnEnterZone(player, zone);
        Aion.GameServer.Services.Instance.InstanceService.OnEnterZone(player, zone);
        Aion.GameServer.World.Zone.ZoneName zoneName = zone.GetAreaTemplate().GetZoneName();
        if (zoneName == null)
            log.LogWarning("No name found for a zone in map " + zone.GetAreaTemplate().GetWorldId() + " with xml name " + zone.GetZoneTemplate().GetXmlName());
        else
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnEnterZone(new Aion.GameServer.QuestEngine.Model.QuestEnv(null, player, 0), zoneName);
    }

    public override void OnLeaveZone(Aion.GameServer.World.Zone.ZoneInstance zone)
    {
        Player player = GetOwner();
        Aion.GameServer.Services.ConquerorAndProtectorSystem.ConquerorAndProtectorService.GetInstance().OnLeaveZone(player, zone);
        Aion.GameServer.Services.Instance.InstanceService.OnLeaveZone(player, zone);
        Aion.GameServer.World.Zone.ZoneName zoneName = zone.GetAreaTemplate().GetZoneName();
        if (zoneName == null)
            log.LogWarning("No name found for a zone in map " + zone.GetAreaTemplate().GetWorldId() + " with xml name " + zone.GetZoneTemplate().GetXmlName());
        else
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnLeaveZone(new Aion.GameServer.QuestEngine.Model.QuestEnv(null, player, 0), zoneName);
    }

    /// <summary>Called when leaving a fly zone or a fly map.</summary>
    public void OnLeaveFlyArea()
    {
        Player player = GetOwner();
        if (!player.HasAccess(Aion.GameServer.Configs.Administration.AdminConfig.FREE_FLIGHT))
        {
            if (player.IsInFlyingState())
            {
                if (player.IsInGlidingState())
                {
                    player.UnsetFlyState(Aion.GameServer.Model.GameObjects.State.FlyState.FLYING);
                    player.UnsetState(CreatureState.FLYING);
                    player.GetLifeStats().TriggerFpReduce();
                    player.GetGameStats().UpdateStatsAndSpeedVisually();
                    PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.STOP_FLY), true);
                }
                else
                {
                    player.GetFlyController().EndFly(true);
                    if (player.IsSpawned() && !player.IsInsideZoneType(Aion.GameServer.Model.Templates.Zone.ZoneType.FLY))
                        AuditLogger.Log(player, "left fly zone in fly state at " + player.GetPosition());
                }
            }
            else if (player.IsInGlidingState())
            {
                player.GetLifeStats().TriggerFpReduce();
            }
        }
    }

    public void OnEnterFlyArea()
    {
        GetOwner().GetLifeStats().TriggerFpReduce();
    }

    /// <summary>Should only be triggered from one place (life stats).</summary>
    public void OnEnterWorld()
    {
        if (GetOwner().GetPosition().GetWorldMapInstance().GetParent().IsExceptBuff())
        {
            if (!Aion.GameServer.Custom.Pvpmap.PvpMapService.GetInstance().IsOnPvPMap(GetOwner()))
                GetOwner().GetEffectController().RemoveAllEffects();
        }

        foreach (Effect ef in GetOwner().GetEffectController().GetAbnormalEffects())
        {
            if (ef.IsDeityAvatar())
            {
                if (GetOwner().GetWorldType() != WorldType.ABYSS && GetOwner().GetWorldType() != WorldType.BALAUREA
                    && GetOwner().GetWorldType() != WorldType.PANESTERRA || GetOwner().IsInInstance())
                {
                    ef.EndEffect();
                }
            }
        }
    }

    public override void OnDie(Creature lastAttacker)
    {
        Player player = GetOwner();
        player.GetController().CancelCurrentSkill(null);
        SetRebirthReviveInfo();
        Creature master = lastAttacker.GetMaster();

        if (Aion.GameServer.Services.DuelService.GetInstance().IsDueling(player))
        {
            bool killedByOpponent = player.IsDueling(master);
            Aion.GameServer.Services.DuelService.GetInstance().LoseDuel(player);
            if (killedByOpponent)
            {
                if (player.GetLifeStats().GetHpPercentage() < 33)
                    player.GetLifeStats().SetCurrentHpPercent(33);
                if (player.GetLifeStats().GetMpPercentage() < 33)
                    player.GetLifeStats().SetCurrentMpPercent(33);
                if (master.GetLifeStats().GetHpPercentage() < 33)
                    master.GetLifeStats().SetCurrentHpPercent(33);
                if (master.GetLifeStats().GetMpPercentage() < 33)
                    master.GetLifeStats().SetCurrentMpPercent(33);
                return;
            }
        }

        // Release summon
        Summon summon = player.GetSummon();
        if (summon != null)
            Aion.GameServer.Services.Summons.SummonsService.DoMode(Aion.GameServer.Model.Summons.SummonMode.RELEASE, summon, Aion.GameServer.Model.Summons.UnsummonType.UNSPECIFIED);

        if (player.IsInState(CreatureState.FLYING))
            player.SetIsFlyingBeforeDeath(true);

        player.SetPlayerMode(Aion.GameServer.Model.Actions.PlayerMode.RIDE, null);
        player.UnsetState(CreatureState.RESTING);
        player.UnsetState(CreatureState.FLOATING_CORPSE);

        player.UnsetState(CreatureState.FLYING);
        player.UnsetState(CreatureState.GLIDING);
        player.UnsetFlyState(Aion.GameServer.Model.GameObjects.State.FlyState.FLYING);
        player.UnsetFlyState(Aion.GameServer.Model.GameObjects.State.FlyState.GLIDING);

        // Effects removed with base.OnDie()
        base.OnDie(lastAttacker);

        ScheduleShowResurrectionOptions();

        if (player.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnDie(player, lastAttacker))
            return;

        Aion.GameServer.World.MapRegion mapRegion = player.GetPosition().GetMapRegion();
        if (mapRegion != null && mapRegion.OnDie(lastAttacker, player))
            return;

        DoReward();

        if (master is Npc || master.Equals(player))
        {
            if (player.GetLevel() > 4 && !player.GetEffectController().HasAbnormalEffect(e => e.IsNoDeathPenalty()))
                player.GetCommonData().CalculateExpLoss();
        }

        Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnDie(new Aion.GameServer.QuestEngine.Model.QuestEnv(null, player, 0));
    }

    private void SetRebirthReviveInfo()
    {
        Player player = GetOwner();
        List<Effect> effects = player.GetEffectController().GetAbnormalEffects();
        foreach (Effect effect in effects)
        {
            foreach (Aion.GameServer.SkillEngine.Effects.EffectTemplate template in effect.GetEffectTemplates())
            {
                if (template.GetEffectId() == 160 && template is Aion.GameServer.SkillEngine.Effects.RebirthEffect)
                {
                    player.SetRebirthEffect((Aion.GameServer.SkillEngine.Effects.RebirthEffect)template);
                    return;
                }
            }
        }
        player.SetRebirthEffect(null);
    }

    public override void OnDespawn()
    {
        if (GetOwner().IsLooting())
            Aion.GameServer.Services.Drop.DropService.GetInstance().CloseDropList(GetOwner(), GetOwner().GetLootingNpcOid());
        base.OnDespawn();
    }

    public void ScheduleShowResurrectionOptions()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (GetOwner().IsDead() && !HasTask(Aion.GameServer.Model.TaskId.TELEPORT))
                ShowResurrectionOptions();
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(500));
    }

    public void ShowResurrectionOptions()
    {
        PacketSendUtility.SendPacket(GetOwner(), new SM_DIE(GetOwner()));
    }

    private bool IsInvader(Player player)
    {
        if (player.GetRace().Equals(Race.ASMODIANS))
        {
            return player.GetWorldId() == 210060000;
        }
        else
        {
            return player.GetWorldId() == 220050000;
        }
    }

    public override void DoReward()
    {
        Aion.GameServer.Services.PvpService.GetInstance().DoReward(GetOwner());
    }

    public override void OnBeforeSpawn()
    {
        base.OnBeforeSpawn();
        if (!GetOwner().IsDead())
        {
            if (GetOwner().GetIsFlyingBeforeDeath())
                GetOwner().UnsetState(CreatureState.FLOATING_CORPSE);
            else if (GetOwner().IsInState(CreatureState.DEAD))
                GetOwner().UnsetState(CreatureState.DEAD);
            GetOwner().SetState(CreatureState.ACTIVE);
        }
        GetOwner().SetHitTimeBoost(0, 0);
        if (GetOwner().GetPanesterraFaction() != null && !Aion.GameServer.World.WorldMapTypeExtensions.IsPanesterraMap(GetOwner().GetWorldId()))
            GetOwner().SetPanesterraFaction(null);
    }

    public override void AttackTarget(Creature target, int time, bool skipChecks)
    {
        if (!Aion.GameServer.Restrictions.PlayerRestrictions.CanAttack(GetOwner(), target))
            return;

        PlayerGameStats gameStats = GetOwner().GetGameStats();
        float attackRange = 1 + gameStats.GetAttackRange().GetCurrent() / 1000f;
        if (!target.GetAggroList().IsHating(GetOwner()))
            attackRange += PositionUtil.CalculateMaxCoveredDistance(GetOwner(), 100);
        if (!PositionUtil.IsInAttackRange(GetOwner(), target, attackRange))
        {
            PacketSendUtility.SendPacket(GetOwner(), SM_ATTACK_RESPONSE.TARGET_TOO_FAR_AWAY(gameStats.GetAttackCounter()));
            return;
        }

        if (!GeoService.GetInstance().CanSee(GetOwner(), target))
        {
            PacketSendUtility.SendPacket(GetOwner(), SM_ATTACK_RESPONSE.STOP_OBSTACLE_IN_THE_WAY(gameStats.GetAttackCounter()));
            return;
        }

        if (target is Npc)
        {
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnAttack(new Aion.GameServer.QuestEngine.Model.QuestEnv(target, GetOwner(), 0));
        }

        int attackSpeed = gameStats.GetAttackSpeed().GetCurrent();

        long milis = CurrentTimeMillis();
        if (milis - lastAttackMillis + 300 < attackSpeed)
        {
            PacketSendUtility.SendPacket(GetOwner(), SM_ATTACK_RESPONSE.STOP_WITHOUT_MESSAGE(gameStats.GetAttackCounter()));
            return;
        }
        lastAttackMillis = milis;

        base.AttackTarget(target, time, true);
    }

    public override void OnAttack(Creature attacker, Effect effect, TYPE type, int damage, bool notifyAttack, LOG logId, AttackStatus? attackStatus, HopType? hopType)
    {
        if (GetOwner().IsDead())
            return;

        if (GetOwner().IsProtectionActive())
            return;

        // avoid killing players after duel
        if (!GetOwner().Equals(attacker) && attacker.GetActingCreature() is Player && !GetOwner().IsEnemy(attacker))
            return;

        CancelUseItem();
        base.OnAttack(attacker, effect, type, damage, notifyAttack, logId, attackStatus, hopType);

        if (attacker is Npc)
        {
            Aion.GameServer.Ai.Handler.ShoutEventHandler.OnAttack((Aion.GameServer.Ai.NpcAI)attacker.GetAi(), GetOwner());
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnAttack(new Aion.GameServer.QuestEngine.Model.QuestEnv(attacker, GetOwner(), 0));
        }

        lastAttackedMillis = CurrentTimeMillis();
    }

    public void UseSkill(SkillTemplate template, int targetType, float x, float y, float z, int clientHitTime, int skillLevel)
    {
        Player player = GetOwner();
        Skill skill = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkillFor(player, template, player.GetTarget());
        if (skill == null && player.IsTransformed())
        {
            Aion.GameServer.Model.Templates.Panels.SkillPanel panel = DataManager.PANEL_SKILL_DATA.GetSkillPanel(player.GetTransformModel().GetPanelId());
            if (panel != null && panel.CanUseSkill(template.GetSkillId(), skillLevel))
            {
                skill = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkillFor(player, template, player.GetTarget(), skillLevel);
            }
        }

        if (skill != null)
        {
            if (!Aion.GameServer.Restrictions.PlayerRestrictions.CanUseSkill(player, skill))
                return;

            skill.SetTargetType(targetType, x, y, z);
            skill.SetClientHitTime(clientHitTime);
            skill.UseSkill();
        }
    }

    public override void OnStartMove()
    {
        base.OnStartMove();
        Aion.GameServer.Taskmanager.Tasks.PlayerMoveTaskManager.GetInstance().AddPlayer(GetOwner());
        CancelUseItem();
        CancelCurrentSkill(null);
    }

    public override void OnMove()
    {
        base.OnMove();
        if (GetOwner().IsInTeam())
            Aion.GameServer.Taskmanager.Tasks.TeamMoveUpdater.GetInstance().Add(GetOwner());
    }

    public override void OnStopMove()
    {
        base.OnStopMove();
        Aion.GameServer.Taskmanager.Tasks.PlayerMoveTaskManager.GetInstance().RemovePlayer(GetOwner());
        CancelCurrentSkill(null);
        UpdateZone();
    }

    protected override void NotifyAIOnMove()
    {
        if (GetOwner().IsUsingFlightTransporterOrWindstream())
            return;
        base.NotifyAIOnMove();
    }

    public override void CancelCurrentSkill(Creature lastAttacker)
    {
        CancelCurrentSkill(lastAttacker, SM_SYSTEM_MESSAGE.STR_SKILL_CANCELED());
    }

    public override void CancelCurrentSkill(Creature lastAttacker, SM_SYSTEM_MESSAGE message)
    {
        if (GetOwner().GetCastingSkill() == null)
        {
            return;
        }

        Player player = GetOwner();
        Skill castingSkill = player.GetCastingSkill();
        castingSkill.CancelCast();
        player.SetCasting(null);
        if (castingSkill.AllowAnimationBoostByCastSpeed())
            player.SetHitTimeBoost(long.MaxValue, castingSkill.GetCastSpeedForAnimationBoostAndChargeSkills()); // yes, this is retail client behavior
        else
            player.SetHitTimeBoost(0, 0);
        if (castingSkill.GetSkillMethod() == Skill.SkillMethod.CAST)
        {
            PacketSendUtility.BroadcastPacket(player, new SM_SKILL_CANCEL(player, castingSkill.GetSkillTemplate().GetSkillId()), true);
            if (message != null)
                PacketSendUtility.SendPacket(player, message);
        }
        else if (castingSkill.GetSkillMethod() == Skill.SkillMethod.ITEM)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_CANCELED());
            player.RemoveItemCoolDown(castingSkill.GetItemTemplate().GetUseLimits().GetDelayId());
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), castingSkill.GetFirstTarget().GetObjectId(),
                castingSkill.GetItemObjectId(), castingSkill.GetItemTemplate().GetTemplateId(), 0, 3, 0), true);
        }

        if (lastAttacker is Player && !lastAttacker.Equals(GetOwner()))
        {
            PacketSendUtility.SendPacket((Player)lastAttacker, SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_SKILL_CANCELED());
        }
    }

    public override void CancelUseItem()
    {
        Player player = GetOwner();
        Item usingItem = player.GetUsingItem();
        player.SetUsingItem(null);
        if (HasTask(Aion.GameServer.Model.TaskId.ITEM_USE))
        {
            CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), usingItem == null ? 0 : usingItem.GetObjectId(),
                usingItem == null ? 0 : usingItem.GetItemTemplate().GetTemplateId(), 0, 3, 0), true);
        }
    }

    public override void OnDialogSelect(int dialogActionId, int prevDialogId, Player player, int questId, int extendedRewardIndex)
    {
        switch (dialogActionId)
        {
            case (int)Aion.GameServer.Model.DialogAction.BUY:
                PacketSendUtility.SendPacket(player, new SM_PRIVATE_STORE(GetOwner().GetStore(), player));
                break;
            case (int)Aion.GameServer.Model.DialogAction.QUEST_ACCEPT_1:
            case (int)Aion.GameServer.Model.DialogAction.QUEST_ACCEPT_SIMPLE:
                if (!GetOwner().Equals(player) && PositionUtil.IsInRange(GetOwner(), player, 100))
                {
                    if (!DataManager.QUEST_DATA.GetQuestById(questId).IsCannotShare())
                        Aion.GameServer.Services.QuestService.StartQuest(new Aion.GameServer.QuestEngine.Model.QuestEnv(null, player, questId, dialogActionId));
                }
                break;
        }
    }

    public void OnLevelChange(int oldLevel, int newLevel)
    {
        if (oldLevel == newLevel)
            return;

        Player player = GetOwner();
        int minNewLevel = oldLevel < newLevel ? oldLevel + 1 : oldLevel - 1;

        if (Aion.GameServer.Configs.Main.GSConfig.ENABLE_RATIO_LIMITATION
            && (player.GetAccount().GetNumberOf(player.GetRace()) == 1 || player.GetAccount().GetMaxPlayerLevel() == newLevel))
        {
            if (oldLevel < Aion.GameServer.Configs.Main.GSConfig.RATIO_MIN_REQUIRED_LEVEL && newLevel >= Aion.GameServer.Configs.Main.GSConfig.RATIO_MIN_REQUIRED_LEVEL)
                Aion.GameServer.GameServer.UpdateRatio(player.GetRace(), 1);
            else if (oldLevel >= Aion.GameServer.Configs.Main.GSConfig.RATIO_MIN_REQUIRED_LEVEL && newLevel < Aion.GameServer.Configs.Main.GSConfig.RATIO_MIN_REQUIRED_LEVEL)
                Aion.GameServer.GameServer.UpdateRatio(player.GetRace(), -1);
        }

        player.GetGameStats().UpdateStatsTemplate();
        player.GetCommonData().UpdateMaxRepose();
        player.GetCommonData().ResetSalvationPoints();
        UpgradePlayer();
        PacketSendUtility.BroadcastPacket(player, new SM_ACTION_ANIMATION(player.GetObjectId(), Aion.GameServer.Model.Animations.ActionAnimation.LEVEL_UP, newLevel), true);

        player.GetNpcFactions().OnLevelUp();
        Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnLevelChanged(player);
        UpdateNearbyQuests();
        if (Aion.GameServer.Configs.Main.HTMLConfig.ENABLE_GUIDES && player.IsSpawned())
            Aion.GameServer.Services.HTMLService.SendGuideHtml(player, minNewLevel, newLevel);
        Aion.GameServer.Services.SkillLearnService.LearnNewSkills(player, minNewLevel, newLevel);
        Aion.GameServer.Services.BonusPackService.GetInstance().AddPlayerCustomReward(player);
        Aion.GameServer.Services.FactionPackService.GetInstance().AddPlayerCustomReward(player);
        if (Aion.GameServer.Configs.Main.CustomConfig.ENABLE_STARTER_KIT)
            Aion.GameServer.Services.Reward.StarterKitService.GetInstance().OnLevelUp(player, minNewLevel, newLevel);
    }

    public void UpgradePlayer()
    {
        Player player = GetOwner();
        player.GetLifeStats().SynchronizeWithMaxStats();
        player.GetGameStats().UpdateStatsVisually();

        if (player.IsInTeam())
            Aion.GameServer.Taskmanager.Tasks.TeamStatUpdater.GetInstance().Add(player);

        if (player.IsLegionMember())
            Aion.GameServer.Services.LegionService.GetInstance().UpdateMemberInfo(player);
    }

    public void OnChangedPlayerAttributes()
    {
        GetOwner().ClearKnownlist();
        SendPlayerInfoPackets(GetOwner());
        if (GetOwner().GetSeeState() != 0)
            PacketSendUtility.SendPacket(GetOwner(), new SM_PLAYER_STATE(GetOwner()));
        GetOwner().GetEffectController().UpdatePlayerEffectIcons(null);
        GetOwner().UpdateKnownlist();
    }

    /// <summary>Starts protection-active and schedules task to end protection.</summary>
    public void StartProtectionActiveTask()
    {
        if (!GetOwner().IsProtectionActive())
        {
            GetOwner().SetVisualState(CreatureVisualState.BLINKING);
            AttackUtil.CancelCastOn(GetOwner());
            AttackUtil.RemoveTargetFrom(GetOwner());
            PacketSendUtility.BroadcastToSightedPlayers(GetOwner(), new SM_PLAYER_STATE(GetOwner()), true);
            AddTask(Aion.GameServer.Model.TaskId.PROTECTION_ACTIVE, ThreadPoolManager.GetInstance().Schedule(_ => { StopProtectionActiveTask(); return System.Threading.Tasks.ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(60000)));
        }
    }

    /// <summary>Stops protection-active task after first move or use skill.</summary>
    public void StopProtectionActiveTask()
    {
        CancelTask(Aion.GameServer.Model.TaskId.PROTECTION_ACTIVE);
        Player player = GetOwner();
        if (player.IsSpawned())
        {
            player.UnsetVisualState(CreatureVisualState.BLINKING);
            PacketSendUtility.BroadcastToSightedPlayers(player, new SM_PLAYER_STATE(player), true);
            NotifyAIOnMove();
        }
    }

    /// <summary>When player arrives at destination point of flying teleport.</summary>
    public void OnFlyTeleportEnd()
    {
        Player player = GetOwner();
        if (player.IsUsingFlightPath(Aion.GameServer.Model.Templates.Flypath.FlightPath.Type.WINDSTREAM))
        {
            player.UnsetState(CreatureState.FLYING);
            player.UnsetFlyState(Aion.GameServer.Model.GameObjects.State.FlyState.FLYING);
            player.SetFlyState(Aion.GameServer.Model.GameObjects.State.FlyState.GLIDING);
            player.SetState(CreatureState.ACTIVE);
            player.SetState(CreatureState.GLIDING);
            player.GetLifeStats().TriggerFpReduce();
            player.GetGameStats().UpdateStatsAndSpeedVisually();
        }
        else
        {
            player.UnsetState(CreatureState.FLYING);
            if (Aion.GameServer.Configs.Main.SecurityConfig.ENABLE_FLYPATH_VALIDATOR)
            {
                long diff = (CurrentTimeMillis() - player.GetFlyStartTime());
                Aion.GameServer.Model.Templates.Flypath.FlyPathEntry path = player.GetCurrentFlyPath();

                if (player.GetWorldId() != path.GetEndWorldId())
                {
                    AuditLogger.Log(player, "tried to use flyPath #" + path.GetId() + " from not native start world " + player.GetWorldId() + " (expected "
                        + path.GetEndWorldId() + ")");
                }

                if (diff < path.GetTimeInMs())
                {
                    AuditLogger.Log(player, "ended fly path too early: Fly duration " + diff + "ms instead of " + path.GetTimeInMs() + "ms");
                }

                player.SetCurrentFlypath(null);
            }
            player.SetState(CreatureState.ACTIVE);
            UpdateZone();
        }
        player.SetFlightPath(null);
    }

    public void StartStance(int skillId)
    {
        StopStance();
        stanceObserver = new StanceObserver(GetOwner(), skillId);
        GetOwner().GetObserveController().AddObserver(stanceObserver);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_PLAYER_STANCE(GetOwner(), 1), true);
    }

    public void StopStance()
    {
        if (stanceObserver != null)
        {
            GetOwner().GetObserveController().RemoveObserver(stanceObserver);
            GetOwner().GetEffectController().RemoveEffect(stanceObserver.GetStanceSkillId());
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_PLAYER_STANCE(GetOwner(), 0), true);
            stanceObserver = null;
        }
    }

    public int GetStanceSkillId()
    {
        return stanceObserver == null ? 0 : stanceObserver.GetStanceSkillId();
    }

    public bool IsUnderStance()
    {
        return stanceObserver != null;
    }

    public void UpdateSoulSickness(int skillId)
    {
        Player player = GetOwner();
        Aion.GameServer.Model.House.House house = player.GetActiveHouse();
        if (house != null)
            switch (house.GetHouseType())
            {
                case Aion.GameServer.Model.Templates.Housing.HouseType.MANSION:
                case Aion.GameServer.Model.Templates.Housing.HouseType.ESTATE:
                case Aion.GameServer.Model.Templates.Housing.HouseType.PALACE:
                    return;
            }

        if (!player.HasPermission(Aion.GameServer.Configs.Main.MembershipConfig.DISABLE_SOULSICKNESS))
        {
            int deathCount = player.GetCommonData().GetDeathCount();
            if (deathCount < 10)
            {
                deathCount++;
                player.GetCommonData().SetDeathCount(deathCount);
            }

            if (skillId == 0)
                skillId = 8291;
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(player, skillId, deathCount, player).UseSkill();
        }
    }

    /// <summary>True if the player is actively in combat (attacked/been attacked within 10s).</summary>
    public bool IsInCombat()
    {
        return CurrentTimeMillis() - GetLastCombatTime() <= 10000;
    }

    /// <summary>The last time the player attacked someone or got attacked.</summary>
    public long GetLastCombatTime()
    {
        return Math.Max(lastAttackedMillis, lastAttackMillis);
    }
}
