using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/danuarReliquary/CursedQueenModorAI (@author Yeats).
/// </summary>
[AIName("cursed_queen_modor")]
public class CursedQueenModorAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(100, 81, 77, 61, 50);
    private readonly List<Vector3f> platformLocations = new List<Vector3f>(5);
    private readonly AtomicInteger stage = new AtomicInteger();
    private float multiplier = 1f;
    private Vector3f? nextPosition = null;

    public CursedQueenModorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        AddPlatformLocations();
    }

    public override AttackHandAnimation ModifyAttackHandAnimation(AttackHandAnimation attackHandAnimation)
    {
        return Rnd.Get((AttackHandAnimation[])Enum.GetValues(typeof(AttackHandAnimation)));
    }

    protected override void HandleAttack(Creature creature)
    {
        hpPhases.TryEnterNextPhase(this);
        base.HandleAttack(creature);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 100:
                QueueSkill(21181, 1); // Malevolence
                QueueSkill(21171, 1); // Grendal's Explosive Wrath
                break;
            case 81:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500741);
                QueueSkill(21171, 1); // Grendal's Explosive Wrath
                QueueSkill(21229, 1);  // Dragon Lords Lightning Shock
                break;
            case 77:
                QueueSkill(21165, 2, 4000);
                QueueSkill(21179, 1, 4000); // ice storm when going up 1st time
                break;
            case 61:
                QueueSkill(21165, 3);
                break;
            case 50:
                QueueSkill(21175, 4);
                break;
        }
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage * multiplier;  // fix skill dmg until new calculations are available
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 21171:
                multiplier = 1.2f;
                break;
            case 21165: // teleport
                if (skillLevel != 10)
                {
                    stage.Set(skillLevel);
                    if (skillLevel == 2)
                    {
                        PacketSendUtility.BroadcastMessage(GetOwner(), 1500750);
                    }
                    else if (skillLevel == 3)
                    {
                        nextPosition = GetRandomPosFromCenter(8);
                        Spawn(284385, nextPosition.X, nextPosition.Y, nextPosition.GetZ(), (sbyte)0);
                    }
                }
                break;
            case 21181: // Malevolence, buff
                stage.Set(1);
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500740);
                break;
            case 21175: // Freezing Sphere of Revenge
                if (skillLevel == 4) // use this skill to despawn modor and spawn clones
                {
                    stage.Set(4);
                    PacketSendUtility.BroadcastMessage(GetOwner(), 1500742);
                }
                multiplier = 0.5f;
                break;
            case 21174:
            case 21229:
            case 21173:
                multiplier = 0.5f;
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        int curStage = stage.Get();
        switch (skillTemplate.GetSkillId())
        {
            case 21165: // teleport
                switch (curStage)
                {
                    case 2:
                        if (skillLevel != 10)
                        {
                            SpawnAdds();
                        }
                        else
                        {
                            QueueSkill(21179, 1, Rnd.Get(3000, 5000)); // ice storm when going up 1st time
                        }
                        break;
                    case 3:
                        QueueSkill(21229, 1); //  Dragon Lords Lightning Shock
                        break;
                    default:
                        break;
                }
                UpdatePosition(curStage);
                break;
            case 21174: // frozen domain of rancour
                multiplier = 1f;
                if (skillLevel == 1 && (curStage == 1 || curStage == 5))
                {
                    float rnd = Rnd.Chance();
                    if (rnd < 15)
                    {
                        QueueSkill(21175, 2, 5000); // lv 2 prevent skill loop
                    }
                    else if (rnd < 45)
                    {
                        QueueSkill(21173, 1, 5000); // summon frost storm
                    }
                }
                break;
            case 21175: // frozen domain of revenge
                multiplier = 1f;
                switch (curStage)
                {
                    case 2: // going up 1st time, switch platforms
                        if (ShouldUsePlatformSkills(skillLevel))
                        {
                            QueueSkill(21165, 10, 5000);
                        }
                        break;
                    case 4: // = hp <= 50%
                        SpawnClones();
                        GetOwner().GetController().Delete();
                        break;
                    case 1:
                    case 3: // when she's not on a platform/last stage use aoe skills
                        if (skillLevel == 1)
                        {
                            float rnd = Rnd.Chance();
                            if (rnd < 15)
                            {
                                QueueSkill(21174, 2, 5000); // lv 2 to prevent skill loop
                            }
                            else if (rnd < 45)
                            {
                                QueueSkill(21229, 1, 5000); //  Dragon Lords Lightning Shock
                            }
                        }
                        break;
                }
                break;
            case 21179: // ice storm
                Vector3f pos = GetRandomPosFromCenter(Rnd.NextInt(9));
                Spawn(284528, pos.GetX(), pos.GetY(), pos.GetZ(), (sbyte)10);
                if (curStage == 2 && ShouldUsePlatformSkills(skillLevel))
                {
                    QueueSkill(21174, 1, 5000); // frozen domain of rancour
                    QueueSkill(21175, 1, 5000); // frozen domain of revenge
                }
                break;
            default:
                multiplier = 1f;
                break;
        }
    }

    private void SpawnClones()
    {
        int spawnCase = Rnd.Get(1, 5);
        // spawn real clone
        switch (spawnCase)
        {
            case 1:
                Spawn(GetRealCloneId(), 255.5489f, 293.42154f, 253.78925f, (sbyte)90);
                break;
            case 2:
                Spawn(GetRealCloneId(), 232.5363f, 263.90112f, 248.65384f, unchecked((sbyte)114));
                break;
            case 3:
                Spawn(GetRealCloneId(), 240.11194f, 235.08876f, 251.14906f, (sbyte)17);
                break;
            case 4:
                Spawn(GetRealCloneId(), 271.23627f, 230.30913f, 250.92981f, (sbyte)42);
                break;
            case 5:
                Spawn(GetRealCloneId(), 284.6919f, 262.7201f, 248.75252f, (sbyte)63);
                break;
        }
        // spawn fake clones
        if (spawnCase != 1)
            Spawn(GetFakeCloneId(), 255.5489f, 293.42154f, 253.78925f, (sbyte)90);
        if (spawnCase != 2)
            Spawn(GetFakeCloneId(), 232.5363f, 263.90112f, 248.65384f, unchecked((sbyte)114));
        if (spawnCase != 3)
            Spawn(GetFakeCloneId(), 240.11194f, 235.08876f, 251.14906f, (sbyte)17);
        if (spawnCase != 4)
            Spawn(GetFakeCloneId(), 271.23627f, 230.30913f, 250.92981f, (sbyte)42);
        if (spawnCase != 5)
            Spawn(GetFakeCloneId(), 284.6919f, 262.7201f, 248.75252f, (sbyte)63);
    }

    /// <summary>
    /// The pillar trio, and the order that comes with them.
    /// </summary>
    /// <remarks>
    /// Retail-sourced; see docs/retail-ai-fidelity.md. Every branch of <c>Rune_FrostNmd_N_65_Ah</c>
    /// that places these three follows the spawns with <c>broadcast_message 444</c> to fifty metres,
    /// naming <b>her current target</b>. All six summon patterns answer it by taking a hate point on
    /// that player and attacking whoever is then most-hated — which, on a summon that has just
    /// appeared and holds no hate, is the player she named. She does not call three adds; she calls
    /// three adds <em>onto someone</em>. See <see cref="DanuarSummonOrderAI"/>.
    /// <para>
    /// Sent after the spawns, as retail sends it, so the three are already in the world to hear it.
    /// </para>
    /// </remarks>
    private void SpawnAdds()
    {
        Spawn(284380, 266.26517f, 273.97614f, 241.54623f, (sbyte)83);
        Spawn(284381, 256.57727f, 278.18225f, 241.54623f, (sbyte)90);
        Spawn(284382, 246.65663f, 275.51996f, 241.54623f, (sbyte)96);

        if (GetAggroList().GetTarget(AggroTarget.MOST_HATED) is Creature named)
            NpcMessageBus.Broadcast(GetOwner(), DanuarSummonOrderAI.OrderMessage, named,
                DanuarSummonOrderAI.OrderRange);
    }

    private void QueueSkill(int id, int lvl)
    {
        QueueSkill(id, lvl, 0);
    }

    private void QueueSkill(int id, int lvl, int nextSkillTime)
    {
        GetOwner().QueueSkill(id, lvl, nextSkillTime);
    }

    private void UpdatePosition(int curStage)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!GetOwner().IsDead() && GetOwner().IsSpawned())
            {
                switch (curStage)
                {
                    case 1: // coming down for 1st time
                        World.World.GetInstance().UpdatePosition(GetOwner(), 256.62f, 257.79f, 241.79f, (byte)90);
                        break;
                    case 2: // going up 1st time // switch platform
                        Vector3f loc = platformLocations[0];
                        platformLocations.RemoveAt(0);
                        platformLocations.Add(loc); // re-add to be able to teleport to loc again
                        World.World.GetInstance().UpdatePosition(GetOwner(), loc.GetX(), loc.GetY(), loc.GetZ(),
                            PositionUtil.GetHeadingTowards(loc.GetX(), loc.GetY(), 256.62f, 257.79f)); // heading towards center
                        break;
                    case 3:  // coming down 2nd time: spawn at random loc
                        World.World.GetInstance().UpdatePosition(GetOwner(), nextPosition!.GetX(), nextPosition.GetY(), nextPosition.GetZ(),
                            PositionUtil.GetHeadingTowards(nextPosition.GetX(), nextPosition.GetY(), 256.62f, 257.79f));
                        break;
                    default:
                        break;
                }
                PacketSendUtility.BroadcastPacket(GetOwner(), new SM_HEADING_UPDATE(GetOwner()));
                PacketSendUtility.BroadcastPacket(GetOwner(), new SM_FORCED_MOVE(GetOwner(), GetOwner()));
            }
            return ValueTask.CompletedTask;
        }, 500L);
    }

    private void AddPlatformLocations()
    {
        platformLocations.Clear();
        platformLocations.Add(new Vector3f(255.49063f, 293.35785f, 253.79933f));
        platformLocations.Add(new Vector3f(284.359f, 262.854f, 248.76f));
        platformLocations.Add(new Vector3f(271.169f, 230.531f, 250.955f));
        platformLocations.Add(new Vector3f(240.273f, 235.181f, 251.153f));
        platformLocations.Add(new Vector3f(232.448f, 263.886f, 248.642f));
    }

    public override int ModifyInitialSkillDelay(int delay)
    {
        return 0;
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        stage.Set(0);
        hpPhases.Reset();
        AddPlatformLocations();
        World.World.GetInstance().UpdatePosition(GetOwner(), 256.62f, 257.79f, 241.79f, (byte)90);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_HEADING_UPDATE(GetOwner()));
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_FORCED_MOVE(GetOwner(), GetOwner()));
    }

    private bool ShouldUsePlatformSkills(int skillLevel)
    {
        // if another teleport skill(=21165) is queued with level != 10 -> next stage is ready so stop switching platforms
        return !GetOwner().HasQueuedSkill(skill => skill.GetSkillId() == 21165 && skill.GetSkillLevel() != 10 && skill.GetSkillLevel() != skillLevel);
    }

    private int GetRealCloneId()
    {
        return GetNpcId() == 234690 ? 855244 : 284383;
    }

    private int GetFakeCloneId()
    {
        return GetNpcId() == 234690 ? 855245 : 284384;
    }

    private Vector3f GetRandomPosFromCenter(int distance)
    {
        Vector3f center = new Vector3f(256.62f, 257.79f, 241.8f);
        double angleRadians = Math.PI / 180 * Rnd.NextFloat(360f);
        center.SetX(center.X + (float)(Math.Cos(angleRadians) * distance));
        center.SetY(center.Y + (float)(Math.Sin(angleRadians) * distance));
        return center;
    }
}
