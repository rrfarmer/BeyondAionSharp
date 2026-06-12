using System;
using System.Collections.Generic;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Controllers;

/// <summary>
/// This class is for controlling Npc's.
/// Java parity: controllers/NpcController (-Nemesiss-, ATracer, Sarynth, Wakizashi).
/// </summary>
public class NpcController : CreatureController<Npc>
{
    private static readonly ILogger log = NullLogger.Instance;

    public override void See(VisibleObject @object)
    {
        base.See(@object);
        if (@object is Creature creature)
        {
            GetOwner().GetAi().OnCreatureEvent(AiEventType.CreatureSee, creature);
        }
    }

    public override void NotSee(VisibleObject @object, ObjectDeleteAnimation animation)
    {
        if (@object is Creature creature)
        {
            GetOwner().GetAi().OnCreatureEvent(AiEventType.CreatureNotSee, creature);
        }
        base.NotSee(@object, animation);
    }

    public override void OnTargetChanged(VisibleObject oldTarget, VisibleObject newTarget)
    {
        base.OnTargetChanged(oldTarget, newTarget);
        GetOwner().ClearAttackedCount();
        GetOwner().GetGameStats().RenewLastChangeTargetTime();
        if (!GetOwner().IsDead())
        {
            if (newTarget == null && GetOwner().GetObjectTemplate().GetTalkInfo() != null)
            {
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    if (GetOwner().GetTarget() == null)
                        GetOwner().GetAi().Think(); // resume walking or reset heading
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(750));
            }
            else
            {
                if (newTarget != null && !GetOwner().Equals(newTarget))
                    GetOwner().GetPosition().SetH(PositionUtil.GetHeadingTowards(GetOwner(), newTarget));
                PacketSendUtility.BroadcastPacket(GetOwner(), new Aion.GameServer.Network.Aion.ServerPackets.SmLookatObject(GetOwner()));
            }
        }
    }

    public override void OnBeforeSpawn()
    {
        base.OnBeforeSpawn();
        Npc owner = GetOwner();

        // set state from npc templates
        if (owner.GetObjectTemplate().GetState() > 0)
            owner.SetState(owner.GetObjectTemplate().GetState());
        else
            owner.SetState(CreatureState.WALK_MODE);

        owner.GetLifeStats().SetCurrentHpPercent(100);
        owner.GetAi().OnGeneralEvent(AiEventType.BeforeSpawned);

        if (owner.GetSpawn().GetState() > 0)
            owner.SetState(owner.GetSpawn().GetState());
    }

    public override void OnAfterSpawn()
    {
        base.OnAfterSpawn();
        GetOwner().GetAi().OnGeneralEvent(AiEventType.Spawned);
    }

    public override void OnDespawn()
    {
        Npc owner = GetOwner();
        CancelCurrentSkill((Creature)null);
        owner.GetEffectController().RemoveAllEffects();
        if (owner.GetSpawn().HasPool() && !owner.IsDead())
            owner.GetSpawn().ResetPoolSpot(owner.GetInstanceId());
        Aion.GameServer.Services.Drop.DropService.GetInstance().UnregisterDrop(owner);
        owner.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnDespawn(owner);
        owner.GetAi().OnGeneralEvent(AiEventType.Despawned);
        GetOwner().GetObserveController().Clear();
        base.OnDespawn();
    }

    public override void OnDie(Creature lastAttacker)
    {
        Npc owner = GetOwner();
        if (owner.GetSpawn().HasPool())
            owner.GetSpawn().ResetPoolSpot(owner.GetInstanceId());

        if (owner.GetAi().Ask(AIQuestion.ALLOW_RESPAWN))
            Aion.GameServer.Services.RespawnService.ScheduleRespawn(GetOwner()); // schedule respawn before onDie events are fired, so handlers can cancel the respawn task if needed

        bool allowDecay = true;
        bool shouldLoot = true;
        try
        {
            allowDecay = owner.GetAi().Ask(AIQuestion.ALLOW_DECAY);
            shouldLoot = owner.GetAi().Ask(AIQuestion.REWARD_LOOT);
            if (owner.GetAi().Ask(AIQuestion.REWARD_AP_XP_DP_LOOT))
                DoReward();
            owner.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnDie(owner);
            owner.GetAi().OnGeneralEvent(AiEventType.Died);
        }
        catch (Exception e)
        {
            log.LogError(e, "onDie() exception for " + owner + ":");
        }

        base.OnDie(lastAttacker);

        if (allowDecay)
        {
            if (shouldLoot)
                PetLoot(owner);
            Aion.GameServer.Services.RespawnService.ScheduleDecayTask(owner);
            if (GetOwner().GetSpawn() != null && GetOwner().GetSpawn().GetStaticId() > 0)
            {
                Aion.GameServer.World.Geo.GeoService.GetInstance().DespawnPlaceableObject(GetOwner().GetWorldId(), GetOwner().GetInstanceId(), GetOwner().GetSpawn().GetStaticId());
            }
        }
        else // instant despawn (no decay time = no loot)
        {
            Delete();
        }
    }

    private void PetLoot(Npc owner)
    {
        Pet lootingPet = FindPetForLooting(owner);
        if (lootingPet != null && PositionUtil.IsInRange(owner, lootingPet.GetMaster(), 28, false))
        {
            int npcObjId = owner.GetObjectId();
            ISet<Aion.GameServer.Model.Drop.DropItem> drops = Aion.GameServer.Services.Drop.DropRegistrationService.GetInstance().GetCurrentDropMap()[npcObjId];
            if (drops != null && drops.Count != 0)
            {
                PacketSendUtility.SendPacket(lootingPet.GetMaster(), new Aion.GameServer.Network.Aion.ServerPackets.SmPet(PetSpecialFunction.AUTOLOOT, true, npcObjId));
                foreach (Aion.GameServer.Model.Drop.DropItem dropItem in new List<Aion.GameServer.Model.Drop.DropItem>(drops)) // array copy since the drops get removed on retrieval
                    Aion.GameServer.Services.Drop.DropService.GetInstance().RequestDropItem(lootingPet.GetMaster(), npcObjId, dropItem.GetIndex(), true);
                PacketSendUtility.SendPacket(lootingPet.GetMaster(), new Aion.GameServer.Network.Aion.ServerPackets.SmPet(PetSpecialFunction.AUTOLOOT, false, npcObjId));
            }
        }
    }

    private Pet FindPetForLooting(Npc npc)
    {
        Aion.GameServer.Model.GameObjects.DropNpc dropNpc = Aion.GameServer.Services.Drop.DropRegistrationService.GetInstance().GetDropRegistrationMap()[npc.GetObjectId()];
        if (dropNpc == null) // npc didn't drop anything
            return null;
        if (dropNpc.GetAllowedLooters().Count != 1) // auto looting is not available in FFA loot mode
            return null;
        Aion.GameServer.Model.GameObjects.Players.Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(dropNpc.GetAllowedLooters().GetEnumerator().Current);
        if (player == null) // looter got disconnected
            return null;
        Pet pet = player.GetPet();
        return pet != null && pet.GetCommonData().IsLooting() ? pet : null;
    }

    public override void DoReward()
    {
        base.DoReward();
        Aion.GameServer.Controllers.Attack.TeamDamageList finalList = GetOwner().GetAggroList().GetFinalDamageList().ToTeamDamages();
        DamageInfo<AionObject> mostDamage = finalList.GetMostDamage();
        AionObject winner = mostDamage == null ? null : mostDamage.GetAttacker();
        if (winner == null)
            return;

        Aion.GameServer.Instance.Handlers.IInstanceHandler instanceHandler = GetOwner().GetPosition().GetWorldMapInstance().GetInstanceHandler();
        float apMultiplier = instanceHandler.GetApMultiplier();
        foreach (DamageInfo<AionObject> info in finalList.GetCreatureOrTeamDamages())
        {
            AionObject attacker = info.GetAttacker();
            float percentage = info.GetDamage() / (float)finalList.GetTotalDamage();
            if (attacker is Aion.GameServer.Model.Team.TemporaryPlayerTeam<Aion.GameServer.Model.Team.TeamMember<Aion.GameServer.Model.GameObjects.Players.Player>> tmpPlayerTeam)
            {
                Aion.GameServer.Model.Team.Common.Service.PlayerTeamDistributionService.DoReward(tmpPlayerTeam, percentage, GetOwner(), winner, finalList);
            }
            else if (attacker is Aion.GameServer.Model.GameObjects.Players.Player player)
            {
                if (!player.IsDead())
                {
                    // Reward init
                    long rewardXp = StatFunctions.CalculateExperienceReward(player.GetLevel(), GetOwner());
                    int rewardDp = StatFunctions.CalculateDPReward(player, GetOwner());
                    float rewardAp = 1;

                    // Dmg percent correction
                    rewardXp = (long)(rewardXp * percentage);
                    rewardDp = (int)(rewardDp * percentage);
                    rewardAp *= percentage;
                    rewardAp *= apMultiplier;

                    bool shouldNotifyQuestEngine = !(instanceHandler is Aion.GameServer.Custom.Pvpmap.PvpMapHandler); // do not include pvp map
                    if (shouldNotifyQuestEngine)
                        Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnKill(new Aion.GameServer.QuestEngine.Model.QuestEnv(GetOwner(), player, 0));
                    Aion.GameServer.Services.Event.EventService.GetInstance().OnPveKill(player, GetOwner());
                    player.GetCommonData().AddExp(rewardXp, Aion.GameServer.Model.GameObjects.Players.Rates.XP_HUNTING, GetOwner().GetObjectTemplate().GetL10n());
                    player.GetCommonData().AddDp(rewardDp);
                    if (GetOwner().GetAi().Ask(AIQuestion.REWARD_AP))
                    {
                        int calculatedAp = StatFunctions.CalculatePvEApGained(player, GetOwner());
                        rewardAp *= calculatedAp;
                        if (rewardAp >= 1)
                        {
                            Aion.GameServer.Services.Abyss.AbyssPointsService.AddAp(player, GetOwner(), (int)rewardAp);
                        }
                    }
                }
                if (attacker.Equals(winner) && GetOwner().GetAi().Ask(AIQuestion.REWARD_LOOT))
                    Aion.GameServer.Services.Drop.DropRegistrationService.GetInstance().RegisterDrop(GetOwner(), player, player.GetLevel(), null);
            }
        }
    }

    public override void OnDialogRequest(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        // notify npc dialog request observer
        if (!GetOwner().GetObjectTemplate().CanInteract())
            return;
        if (!PositionUtil.IsInTalkRange(player, GetOwner()))
        {
            if (GetOwner().GetObjectTemplate().IsDialogNpc())
                PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DIALOG_TOO_FAR_TO_TALK());
            else
                PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_WAREHOUSE_TOO_FAR_FROM_NPC());
            return;
        }

        GetOwner().GetAi().OnCreatureEvent(AiEventType.DialogStart, player);
    }

    public override void OnDialogSelect(int dialogActionId, int prevDialogId, Aion.GameServer.Model.GameObjects.Players.Player player, int questId, int extendedRewardIndex)
    {
        if (!PositionUtil.IsInTalkRange(player, GetOwner()))
            return;
        if (!GetOwner().GetAi().OnDialogSelect(player, dialogActionId, questId, extendedRewardIndex))
        {
            Aion.GameServer.Services.DialogService.OnDialogSelect(dialogActionId, player, GetOwner(), questId, extendedRewardIndex);
        }
    }

    public override void OnAddHate(Creature attacker, bool isNewInAggroList)
    {
        if (isNewInAggroList && attacker is Aion.GameServer.Model.GameObjects.Players.Player)
        {
            if (((Aion.GameServer.Model.GameObjects.Players.Player)attacker).IsInTeam())
            {
                foreach (Aion.GameServer.Model.GameObjects.Players.Player player in ((Aion.GameServer.Model.GameObjects.Players.Player)attacker).GetCurrentTeam().FilterMembers(m => PositionUtil.IsInRange(GetOwner(), m, 50)))
                    Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnAddAggroList(new Aion.GameServer.QuestEngine.Model.QuestEnv(GetOwner(), player, 0));
            }
            else
            {
                Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnAddAggroList(new Aion.GameServer.QuestEngine.Model.QuestEnv(GetOwner(), (Aion.GameServer.Model.GameObjects.Players.Player)attacker, 0));
            }
        }
        base.OnAddHate(attacker, isNewInAggroList);
    }

    public override void OnAttack(Creature attacker, Effect effect, TYPE type, int damage, bool notifyAttack, LOG logId, AttackStatus attackStatus,
        HopType hopType)
    {
        if (GetOwner().IsDead())
            return;
        Creature actingCreature;

        // summon should gain its own aggro (except if despawned, for example because of a damage over time effect)
        if (attacker is Summon && attacker.IsSpawned())
            actingCreature = attacker;
        else
            actingCreature = attacker.GetActingCreature();

        base.OnAttack(actingCreature, effect, type, damage, notifyAttack, logId, attackStatus, hopType);

        Npc npc = GetOwner();
        Aion.GameServer.Ai.Handler.ShoutEventHandler.OnEnemyAttack((Aion.GameServer.Ai.NpcAI)npc.GetAi(), attacker);
        if (actingCreature is Aion.GameServer.Model.GameObjects.Players.Player)
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnAttack(new Aion.GameServer.QuestEngine.Model.QuestEnv(npc, (Aion.GameServer.Model.GameObjects.Players.Player)actingCreature, 0));
    }

    public override void OnStartMove()
    {
        base.OnStartMove();
        Aion.GameServer.Taskmanager.Tasks.MoveTaskManager.GetInstance().AddCreature(GetOwner());
    }

    public override void OnStopMove()
    {
        base.OnStopMove();
        Aion.GameServer.Taskmanager.Tasks.MoveTaskManager.GetInstance().RemoveCreature(GetOwner());
    }

    public override void OnEnterZone(Aion.GameServer.World.Zone.ZoneInstance zoneInstance)
    {
        if (zoneInstance.GetAreaTemplate().GetZoneName() == null)
        {
            log.LogError("No name found for a Zone in the map " + zoneInstance.GetAreaTemplate().GetWorldId());
        }
    }

    public override bool UseSkill(int skillId, int skillLevel)
    {
        SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        if (!GetOwner().IsSkillDisabled(skillTemplate))
        {
            GetOwner().GetGameStats().RenewLastSkillTime();
            return base.UseSkill(skillId, skillLevel);
        }
        return false;
    }

    public void LoseAggro(bool restoreHp)
    {
        GetOwner().SetTarget(null);
        GetOwner().GetAggroList().Clear();
        if (restoreHp)
            GetOwner().GetLifeStats().TriggerRestoreTask();
    }
}
