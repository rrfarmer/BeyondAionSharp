using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/infinityShard/HyperionAI (@author Cheatkiller, Yeats, Estrayl).
/// </summary>
[AIName("hyperion")]
public class HyperionAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(100, 80, 75, 67, 65, 55, 50, 45, 40, 30, 25, 20, 17, 10);
    private readonly List<int> possibleSummons = new List<int> { 231096, 231097, 231098, 231099, 231100, 231101 };
    private readonly WorldPosition northernSpawnPos;
    private readonly WorldPosition southernSpawnPos;
    private ScheduledTask? spawnTask;
    private byte stage = 0;
    private double dist;

    public HyperionAI(Npc owner)
        : base(owner)
    {
        northernSpawnPos = new WorldPosition(GetPosition().GetMapId(), 112.006f, 122.894f, 123.303f, (byte)0);
        southernSpawnPos = new WorldPosition(GetPosition().GetMapId(), 148.127f, 150.346f, 123.729f, (byte)0);
    }

    public override AttackTypeAnimation GetAttackTypeAnimation(Creature target)
    {
        dist = PositionUtil.GetDistance(GetOwner(), target) - GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide() - target.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide();
        if (dist > 4)
        {
            return AttackTypeAnimation.RANGED;
        }
        return AttackTypeAnimation.MELEE;
    }

    protected override void HandleAttack(Creature creature)
    {
        hpPhases.TryEnterNextPhase(this);
        base.HandleAttack(creature);
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        if (effect == null)
        {
            if (dist > 10)
            {
                return damage * 4.5f;
            }
            else if (dist > 4)
            {
                return damage * 1.5f;
            }
        }
        return damage;
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        if (dist > 4)
        {
            return ItemAttackType.MAGICAL_EARTH;
        }
        return ItemAttackType.PHYSICAL;
    }

    public override AttackHandAnimation ModifyAttackHandAnimation(AttackHandAnimation attackHandAnimation)
    {
        return Rnd.Get((AttackHandAnimation[])Enum.GetValues(typeof(AttackHandAnimation)));
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 100:
                QueuePowerfulEnergyBlast();
                break;
            case 75:
                SpawnSummons(++stage);
                SpawnAncientTyrhund(2);
                break;
            case 80:
            case 67:
                SpawnAncientTyrhund(2);
                break;
            case 55:
                SpawnAncientTyrhund(3);
                break;
            case 45:
            case 30:
            case 17:
                SpawnAncientTyrhund(4);
                break;
            case 65:
            case 50:
            case 25:
            case 20:
                stage++;
                GetOwner().QueueSkill(21253, 56, 0);
                GetOwner().QueueSkill(21244, 56, 5000);
                break;
            case 40:
                SpawnSummons(++stage);
                break;
            case 10:
                GetOwner().QueueSkill(21246, 56);
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 21253:
                switch (stage)
                {
                    case 1:
                    case 2:
                        SpawnSummons(1);
                        break;
                    case 3:
                    case 4:
                        SpawnSummons(2);
                        break;
                    case 5:
                        SpawnSummons(3);
                        break;
                    case 6:
                        SpawnSummons(4);
                        break;
                }
                break;
            case 21246:
                ScheduleLastPhase();
                break;
        }
    }

    private void ScheduleLastPhase()
    {
        spawnTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (!IsDead())
            {
                SpawnSummons(5);
                SpawnAncientTyrhund(4);
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(35000), TimeSpan.FromMilliseconds(60000));
    }

    private void SpawnAncientTyrhund(int count)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnWithWalker(231103, GetRndPos(GetPosition(), 12), null);
        }
    }

    /// <summary>
    /// <para>spawnCase - 1 for one add per side</para>
    /// <para>2 for two adds per side</para>
    /// <para>3 for one add north + two adds south</para>
    /// <para>4 for one add south + two adds south</para>
    /// <para>5 for four adds per side (last phase)</para>
    /// </summary>
    private void SpawnSummons(int spawnCase)
    {
        switch (spawnCase)
        {
            case 1:
                SpawnWithWalker(0, GetRndPos(northernSpawnPos, 3), "hyperionGuards1");
                SpawnWithWalker(0, GetRndPos(southernSpawnPos, 3), "hyperionGuards2");
                break;
            case 2:
                SpawnWithWalker(0, GetRndPos(northernSpawnPos, 3), "hyperionGuards1");
                SpawnWithWalker(0, GetRndPos(northernSpawnPos, 3), "hyperionGuards1");
                SpawnWithWalker(0, GetRndPos(southernSpawnPos, 3), "hyperionGuards2");
                SpawnWithWalker(0, GetRndPos(southernSpawnPos, 3), "hyperionGuards2");
                break;
            case 3:
                SpawnWithWalker(0, GetRndPos(northernSpawnPos, 3), "hyperionGuards1");
                SpawnWithWalker(0, GetRndPos(southernSpawnPos, 3), "hyperionGuards2");
                SpawnWithWalker(0, GetRndPos(southernSpawnPos, 3), "hyperionGuards2");
                break;
            case 4:
                SpawnWithWalker(0, GetRndPos(northernSpawnPos, 3), "hyperionGuards1");
                SpawnWithWalker(0, GetRndPos(northernSpawnPos, 3), "hyperionGuards1");
                SpawnWithWalker(0, GetRndPos(southernSpawnPos, 3), "hyperionGuards2");
                break;
            case 5:
                SpawnWithWalker(0, GetRndPos(northernSpawnPos, 3), "hyperionGuards1");
                SpawnWithWalker(0, GetRndPos(northernSpawnPos, 3), "hyperionGuards1");
                SpawnWithWalker(0, GetRndPos(northernSpawnPos, 3), "hyperionGuards1");
                SpawnWithWalker(0, GetRndPos(northernSpawnPos, 3), "hyperionGuards1");
                SpawnWithWalker(0, GetRndPos(southernSpawnPos, 3), "hyperionGuards2");
                SpawnWithWalker(0, GetRndPos(southernSpawnPos, 3), "hyperionGuards2");
                SpawnWithWalker(0, GetRndPos(southernSpawnPos, 3), "hyperionGuards2");
                SpawnWithWalker(0, GetRndPos(southernSpawnPos, 3), "hyperionGuards2");
                break;
        }
    }

    private void SpawnWithWalker(int npcId, Point3D p, string? walkerId)
    {
        Npc npc = (Npc)Spawn(npcId == 0 ? Rnd.Get(possibleSummons) : npcId, p.GetX(), p.GetY(), p.GetZ(), (sbyte)0);
        if (walkerId != null)
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                if (npc.IsDead())
                    return ValueTask.CompletedTask;
                npc.GetSpawn().SetWalkerId(walkerId);
                WalkManager.StartWalking((NpcAI)npc.GetAi());
                npc.SetState(CreatureState.ACTIVE, true);
                PacketSendUtility.BroadcastToMap(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.RUN));
                return ValueTask.CompletedTask;
            }, 2500L);
        }
    }

    private Point3D GetRndPos(WorldPosition p, float distanceMod)
    {
        double angleRadians = Math.PI / 180 * Rnd.NextFloat(360f);
        float distance = Rnd.NextFloat(distanceMod);
        float x1 = (float)(Math.Cos(angleRadians) * distance);
        float y1 = (float)(Math.Sin(angleRadians) * distance);
        return new Point3D(p.GetX() + x1, p.GetY() + y1, p.GetZ());
    }

    private void QueuePowerfulEnergyBlast()
    {
        GetOwner().QueueSkill(21241, 56, 0);
        GetOwner().QueueSkill(21241, 56, 0);
        GetOwner().QueueSkill(21241, 56, 8000);
    }

    private void CancelSpawnTask()
    {
        if (spawnTask != null && !spawnTask.IsCancelled)
            spawnTask.Cancel(true);
    }

    public override int ModifyInitialSkillDelay(int delay)
    {
        return 0;
    }

    protected override void HandleBackHome()
    {
        CancelSpawnTask();
        base.HandleBackHome();
    }

    protected override void HandleDespawned()
    {
        CancelSpawnTask();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelSpawnTask();
        base.HandleDied();
    }
}
