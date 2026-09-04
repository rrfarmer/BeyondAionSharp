using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Summons;

/// <summary>Java parity: services/summons/SummonsService (xTz).</summary>
public class SummonsService
{
    /// <summary>create summon</summary>
    public static Summon CreateSummon(Aion.GameServer.Model.GameObjects.Players.Player master, int npcId, int skillId, int skillLevel, int time)
    {
        if (master.GetSummon() != null)
        {
            PacketSendUtility.SendPacket(master, SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_ALREADY_HAVE_A_FOLLOWER());
            return null;
        }
        Summon summon = Aion.GameServer.SpawnEngine.VisibleObjectSpawner.SpawnSummon(master, npcId, skillId, time);
        master.SetSummon(summon);
        PacketSendUtility.SendPacket(master, new SM_SUMMON_PANEL(summon));
        PacketSendUtility.BroadcastPacket(summon, new SM_EMOTION(summon, EmotionType.CHANGE_SPEED));
        PacketSendUtility.BroadcastPacket(summon, new SM_SUMMON_UPDATE(summon));
        return summon;
    }

    /// <summary>
    /// Releases the summon after UnsummonType.GetDelayMillis(), see Summon.RegisterRelease(SummonRelease) for competing releases.
    /// </summary>
    public static void Release(Summon summon, UnsummonType unsummonType)
    {
        SummonRelease release = new SummonRelease(unsummonType);
        if (!summon.RegisterRelease(release))
            return;
        summon.GetController().CancelCurrentSkill((Creature)null);
        summon.SetMode(SummonMode.RELEASE);
        summon.GetObserveController().NotifySummonReleaseObservers();
        new ReleaseSummonTask(summon, release).ScheduleOrRun();
    }

    private class ReleaseSummonTask
    {
        private readonly Summon summon;
        private readonly SummonRelease release;
        private readonly UnsummonType unsummonType;
        private bool addedMasterHate;

        public ReleaseSummonTask(Summon owner, SummonRelease release)
        {
            this.summon = owner;
            this.release = release;
            this.unsummonType = release.GetUnsummonType();
        }

        public void Run()
        {
            if (!summon.StartRelease(release))
                return;
            Aion.GameServer.Model.GameObjects.Players.Player master = summon.GetMaster();
            VisibleObject summonObj = Aion.GameServer.World.World.GetInstance().FindVisibleObject(summon.GetObjectId());
            // transformed npc via SM_TRANSFORM_IN_SUMMON
            if (summonObj is Npc npc)
                npc.GetController().Delete();
            else
                summon.GetController().Delete(); // triggers SummonController.notKnow(master), the resulting DISTANCE release is ignored

            if (summon.Equals(master.GetSummon()))
                master.SetSummon(null);

            SkillTemplate summoningSkill = DataManager.SKILL_DATA.GetSkillTemplate(summon.GetSummonedBySkillId());
            if (summoningSkill != null && summoningSkill.GetCooldown() > 0)
                master.SetSkillCoolDown(summoningSkill.GetCooldownId(), summoningSkill.GetCooldown() * 100 + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            if (unsummonType == UnsummonType.DISTANCE)
                PacketSendUtility.SendPacket(master, SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_UNSUMMON_BY_TOO_DISTANCE());
            else
                PacketSendUtility.SendPacket(master, SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_UNSUMMONED(summon.GetL10n()));
            PacketSendUtility.SendPacket(master, new SM_SUMMON_PANEL_REMOVE(summon.GetSummonedBySkillId()));
            PacketSendUtility.SendPacket(master, new SM_SUMMON_OWNER_REMOVE(summon.GetObjectId()));
            if (!addedMasterHate)
                ScheduleAddMasterHate(summon);
        }

        public void ScheduleOrRun()
        {
            if (UnsummonTypeExtensions.IsInstant(unsummonType))
            {
                Run();
                return;
            }
            release.SetTask(ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                Run();
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(UnsummonTypeExtensions.GetDelayMillis(unsummonType))));
            if (UnsummonTypeExtensions.IsCancelableByMaster(unsummonType)) // master hate is added delayed, he may still take the order back
            {
                PacketSendUtility.SendPacket(summon.GetMaster(), SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_UNSUMMON_FOLLOWER(summon.GetL10n()));
                PacketSendUtility.SendPacket(summon.GetMaster(), new SM_SUMMON_UPDATE(summon));
            }
            else
                ScheduleAddMasterHate(summon);
        }

        private void ScheduleAddMasterHate(Summon summon)
        {
            addedMasterHate = true;
            if (!summon.GetMaster().IsDead() && summon.GetMaster().IsOnline())
            {
                List<AggroList> summonOnlyHaters = FindSummonOnlyHaters(summon);
                if (summonOnlyHaters.Count != 0) // add master hate to every npc which was only attacked by the summon before
                    ThreadPoolManager.GetInstance().Schedule(ct =>
                    {
                        if (!summon.GetMaster().IsDead())
                            summonOnlyHaters.ForEach(aggroList => aggroList.AddHate(summon.GetMaster(), 1));
                        return ValueTask.CompletedTask;
                    }, TimeSpan.FromMilliseconds(1000));
            }
        }

        private List<AggroList> FindSummonOnlyHaters(Summon summon)
        {
            List<AggroList> aggroLists = new List<AggroList>();
            summon.GetMaster().GetKnownList().ForEachObject(@object =>
            {
                if (@object is Creature creature)
                {
                    AggroList aggroList = creature.GetAggroList();
                    if (aggroList.IsHating(summon) && !aggroList.IsHating(summon.GetMaster()))
                        aggroLists.Add(aggroList);
                }
            });
            return aggroLists;
        }
    }

    /// <summary>Change to rest mode</summary>
    public static void RestMode(Summon summon)
    {
        summon.GetController().CancelCurrentSkill((Creature)null);
        summon.SetMode(SummonMode.REST);
        Aion.GameServer.Model.GameObjects.Players.Player master = summon.GetMaster();
        PacketSendUtility.SendPacket(master, SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_REST_MODE(summon.GetL10n()));
        PacketSendUtility.SendPacket(master, new SM_SUMMON_UPDATE(summon));
        summon.GetLifeStats().TriggerRestoreTask();
    }

    public static void SetUnkMode(Summon summon)
    {
        summon.SetMode(SummonMode.UNK);
        Aion.GameServer.Model.GameObjects.Players.Player master = summon.GetMaster();
        PacketSendUtility.SendPacket(master, new SM_SUMMON_UPDATE(summon));
    }

    /// <summary>Change to guard mode</summary>
    public static void GuardMode(Summon summon)
    {
        summon.GetController().CancelCurrentSkill((Creature)null);
        summon.SetMode(SummonMode.GUARD);
        Aion.GameServer.Model.GameObjects.Players.Player master = summon.GetMaster();
        PacketSendUtility.SendPacket(master, SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_GUARD_MODE(summon.GetL10n()));
        PacketSendUtility.SendPacket(master, new SM_SUMMON_UPDATE(summon));
        summon.GetLifeStats().TriggerRestoreTask();
    }

    /// <summary>Change to attackMode</summary>
    public static void AttackMode(Summon summon)
    {
        summon.SetMode(SummonMode.ATTACK);
        Aion.GameServer.Model.GameObjects.Players.Player master = summon.GetMaster();
        PacketSendUtility.SendPacket(master, SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_ATTACK_MODE(summon.GetL10n()));
        PacketSendUtility.SendPacket(master, new SM_SUMMON_UPDATE(summon));
        summon.GetLifeStats().CancelRestoreTask();
    }

    public static void DoMode(SummonMode summonMode, Summon summon)
    {
        DoMode(summonMode, summon, 0, null);
    }

    public static void DoMode(SummonMode summonMode, Summon summon, UnsummonType unsummonType)
    {
        DoMode(summonMode, summon, 0, unsummonType);
    }

    public static void DoMode(SummonMode summonMode, Summon summon, int targetObjId, UnsummonType? unsummonType)
    {
        if (summon.IsDead())
            return;

        if (summon.GetMaster() == null)
            return;

        if (unsummonType == UnsummonType.COMMAND)
        {
            if (summon.IsReleaseUncancelable())
                return;
            if (summonMode == SummonMode.ATTACK && !summon.GetController().CanAttack(targetObjId))
                return; // don't cancel a pending release for an order that won't be carried out
            // UNK leaves the summons mode untouched, so it must not take back a pending release either
            if (summonMode == SummonMode.ATTACK || summonMode == SummonMode.GUARD || summonMode == SummonMode.REST)
                summon.CancelReleaseByMaster();
        }

        switch (summonMode)
        {
            case SummonMode.REST:
                summon.GetController().RestMode();
                break;
            case SummonMode.ATTACK:
                summon.GetController().AttackMode(targetObjId);
                break;
            case SummonMode.GUARD:
                summon.GetController().GuardMode();
                break;
            case SummonMode.RELEASE:
                if (unsummonType != null)
                {
                    summon.GetController().Release(unsummonType.Value);
                }
                break;
            case SummonMode.UNK:
                break;
        }
    }
}
