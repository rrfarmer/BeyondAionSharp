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

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/danuarReliquary/EnragedQueenModorAI (@author Yeats).
/// </summary>
[AIName("enraged_queen_modor")]
public class EnragedQueenModorAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(100, 90, 70, 52, 25, 10);
    private readonly AtomicInteger stage = new AtomicInteger();
    private readonly List<Vector3f> platformLocations = new List<Vector3f>(5);
    // weakened reliquary jotun, weakened idean lapilima, weakened idean obscura, weakened modor guardian, weakened danuar ghost, weakened hoarfrost acheron drake,
    private int[] monsterIds = new int[] { 284659, 284660, 284661, 284662, 284663, 284664 };
    private float multiplier = 1f;
    private Vector3f? nextPosition = null;
    private ScheduledTask? positionUpdateTask = null;

    public EnragedQueenModorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        AddPlatformLocations();
        if (IsCrazedModor())
        {
            monsterIds = new int[] { 856493, 856494, 856495, 856496, 856497, 856498 };
        }
    }

    public override AttackHandAnimation ModifyAttackHandAnimation(AttackHandAnimation attackHandAnimation)
    {
        return Rnd.Get((AttackHandAnimation[])Enum.GetValues(typeof(AttackHandAnimation)));
    }

    public override int ModifyInitialSkillDelay(int delay)
    {
        return 0;
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
                QueueSkill(21171, 1); // Grendal's Explosive Wrath
                QueueSkill(21169, 1, 4000); // buff
                break;
            case 90:
                QueueSkill(21165, 1, 4000); // rend space
                QueueSkill(21170, 1, 4000); // Grendal's Rage
                break;
            case 70:
                QueueSkill(21165, 2); // rend space
                QueueSkill(21176, 1); // electric vengeance
                QueueSkill(21229, 1, 4000); // electric wrath
                QueueSkill(21742, 1, 4000); // subzero malice
                QueueSkill(21743, 1, 4000); // rushing subzero malice
                break;
            case 52:
                QueueSkill(21165, 4); // rend space
                QueueSkill(21268, 1); // demented scream
                QueueSkill(21269, 1, 4000); // spiteful roar
                QueueSkill(21742, 1, 4000); // subzero malice
                QueueSkill(21743, 1, 4000); // rushing subzero malice
                QueueSkill(21165, 5, 4000); // rend space
                QueueSkill(21170, 1, 2500); // Grendal's Rage
                break;
            case 25:
                QueueSkill(21165, 6); // rend space
                QueueSkill(21176, 1); // electric vengeance
                QueueSkill(21229, 1); // electric wrath
                QueueSkill(21742, 1); // subzero malice
                QueueSkill(21743, 1); // rushing subzero malice
                QueueSkill(21165, 7, 4000); // rend space
                QueueSkill(21170, 1, 0); // Grendal's Rage
                break;
            case 10:
                QueueSkill(21165, 8); // rend space
                QueueSkill(21176, 1); // electric vengeance
                QueueSkill(21229, 1, 4000); // electric wrath
                QueueSkill(21165, 9, 4000); // rend space
                QueueSkill(21170, 1, 0); // Grendal's Rage
                break;
        }
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage * multiplier;
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        multiplier = 0.5f;
        switch (skillTemplate.GetSkillId())
        {
            case 21171:
                multiplier = 1.2f;
                if (stage.Get() == 0)
                {
                    PacketSendUtility.BroadcastMessage(GetOwner(), 1500743);
                }
                break;
            case 21165:
                if (skillLevel != 10)
                {
                    stage.Set(skillLevel);
                    if (skillLevel == 1 || skillLevel == 5 || skillLevel == 7 || skillLevel == 9)
                    {
                        PacketSendUtility.BroadcastMessage(GetOwner(), 1500750);
                    }
                    else if (skillLevel == 2 || skillLevel == 6 || skillLevel == 8)
                    {
                        nextPosition = GetRandomPosFromCenter(8);
                        Spawn(284385, nextPosition.X, nextPosition.Y, nextPosition.GetZ(), (sbyte)0);
                    }
                    else if (skillLevel == 4)
                    {
                        PacketSendUtility.BroadcastMessage(GetOwner(), 1500751);
                    }
                }
                break;
            case 21176:
                multiplier = 0.35f;
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        multiplier = 1f;
        int curStage = stage.Get();
        switch (skillTemplate.GetSkillId())
        {
            case 21165:
                switch (curStage)
                {
                    case 1:
                    case 3:
                    case 5:
                    case 7:
                    case 9:
                        if (skillLevel != 10 && skillLevel != 3)
                        {
                            SpawnAdds(curStage);
                        }
                        if (ShouldUsePlatformSkills(skillLevel))
                        {
                            QueueSkill(21742, 1, GetNextSkillTimeFor(curStage)); // subzero malice
                            QueueSkill(21743, 1, GetNextSkillTimeFor(curStage)); // rushing subzero malice
                        }
                        break;
                }
                UpdatePosition(curStage);
                break;
            case 21743:
                switch (curStage)
                {
                    case 1:
                    case 3:
                    case 5:
                    case 7:
                    case 9:
                        if (ShouldUsePlatformSkills(skillLevel))
                        {
                            QueueSkill(21165, 10, 5000 - curStage * 350); // rend space
                            QueueSkill(21179, 1, GetNextSkillTimeFor(curStage)); // ice storm
                        }
                        break;
                    case 2:
                        if (GetLifeStats().GetHpPercentage() >= 55)
                        {
                            QueueSkill(21165, 3, 4000); // rend space
                            QueueSkill(21170, 1, 3000); // Grendal's Rage
                        }
                        break;
                }
                break;
            case 21179: // ice storm
                Vector3f pos = GetRandomPosFromCenter(Rnd.NextInt(9));
                Spawn(284528, pos.GetX(), pos.GetY(), pos.GetZ(), (sbyte)10);
                break;
        }
    }

    private void UpdatePosition(int curStage)
    {
        positionUpdateTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!GetOwner().IsDead() && GetOwner().IsSpawned())
            {
                switch (curStage)
                {
                    case 1:
                    case 3: // switch platforms
                    case 5:
                    case 7:
                    case 9:
                        int idx = curStage > 3 ? Rnd.Get(0, 3) : 0;
                        Vector3f loc = platformLocations[idx];
                        platformLocations.RemoveAt(idx);
                        platformLocations.Add(loc); // re-add to be able to teleport to loc again
                        World.World.GetInstance().UpdatePosition(GetOwner(), loc.GetX(), loc.GetY(), loc.GetZ(),
                            PositionUtil.GetHeadingTowards(loc.GetX(), loc.GetY(), 256.62f, 257.79f));
                        break;
                    case 4:
                        byte heading;
                        Creature mostHated = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
                        if (mostHated != null)
                            heading = PositionUtil.GetHeadingTowards(256.62f, 257.79f, mostHated.GetX(), mostHated.GetY());
                        else
                            heading = (byte)Rnd.NextInt(120);
                        World.World.GetInstance().UpdatePosition(GetOwner(), 256.62f, 257.79f, 241.79f, heading);
                        break;
                    case 2:
                    case 6:
                    case 8:
                        World.World.GetInstance().UpdatePosition(GetOwner(), nextPosition!.GetX(), nextPosition.GetY(), nextPosition.GetZ(),
                            PositionUtil.GetHeadingTowards(nextPosition.GetX(), nextPosition.GetY(), 256.62f, 257.79f));
                        break;
                }
                PacketSendUtility.BroadcastPacket(GetOwner(), new SM_HEADING_UPDATE(GetOwner()));
                PacketSendUtility.BroadcastPacket(GetOwner(), new SM_FORCED_MOVE(GetOwner(), GetOwner()));
            }
            return ValueTask.CompletedTask;
        }, 500L);
    }

    private void SpawnAdds(int curStage)
    {
        switch (curStage)
        {
            case 1:
                Spawn(monsterIds[0], 256.44f, 278.59f, 241.55f, (sbyte)90);
                Spawn(monsterIds[1], 268.56f, 272.88f, 241.55f, (sbyte)78);
                Spawn(monsterIds[2], 243.61f, 272.99f, 241.55f, unchecked((sbyte)105));
                Spawn(monsterIds[2], 257.75f, 239.93f, 241.55f, (sbyte)32);
                break;
            case 5:
                Spawn(monsterIds[3], 260.68f, 262.03f, 241.81f, (sbyte)75);
                Spawn(monsterIds[3], 252.21f, 253.48f, 241.8f, (sbyte)15);
                Spawn(monsterIds[4], 246.57f, 273.86f, 241.55f, (sbyte)100);
                Spawn(monsterIds[5], 269.73f, 244.87f, 241.55f, (sbyte)48);
                Spawn(monsterIds[5], 240.87f, 250.35f, 241.55f, (sbyte)10);
                break;
            case 7:
                Spawn(monsterIds[0], 260.68f, 262.03f, 241.81f, (sbyte)75);
                Spawn(monsterIds[0], 252.21f, 253.48f, 241.8f, (sbyte)15);
                Spawn(monsterIds[1], 246.57f, 273.86f, 241.55f, (sbyte)100);
                Spawn(monsterIds[2], 269.73f, 244.87f, 241.55f, (sbyte)48);
                Spawn(monsterIds[2], 240.87f, 250.35f, 241.55f, (sbyte)10);
                break;
            case 9:
                Spawn(monsterIds[0], 260.68f, 262.03f, 241.81f, (sbyte)75);
                Spawn(monsterIds[1], 252.21f, 253.48f, 241.8f, (sbyte)15);
                Spawn(monsterIds[2], 246.57f, 273.86f, 241.55f, (sbyte)100);
                Spawn(monsterIds[3], 269.73f, 244.87f, 241.55f, (sbyte)48);
                Spawn(monsterIds[4], 240.87f, 250.35f, 241.55f, (sbyte)10);
                Spawn(monsterIds[5], 257.75f, 239.93f, 241.55f, (sbyte)32);
                break;
        }
    }

    private int GetNextSkillTimeFor(int stage)
    {
        switch (stage)
        {
            case 1:
            case 2:
            case 3:
                return Rnd.Get(2, 6) * 1000;
            case 4:
            case 5:
            case 6:
            case 7:
                return Rnd.Get(2, 4) * 1000;
            default:
                return Rnd.Get(0, 4) * 1000;
        }
    }

    private Vector3f GetRandomPosFromCenter(int distance)
    {
        Vector3f center = new Vector3f(256.62f, 257.79f, 241.8f);
        double angleRadians = Math.PI / 180 * Rnd.NextFloat(360f);
        center.SetX(center.X + (float)(Math.Cos(angleRadians) * distance));
        center.SetY(center.Y + (float)(Math.Sin(angleRadians) * distance));
        return center;
    }

    private void QueueSkill(int id, int lvl)
    {
        QueueSkill(id, lvl, 0);
    }

    private void QueueSkill(int id, int lvl, int nextSkillTime)
    {
        GetOwner().QueueSkill(id, lvl, nextSkillTime);
    }

    private bool IsCrazedModor()
    {
        return GetNpcId() == 234691; // Crazed Modor (Infernal Danuar Reliquary)
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

    private bool ShouldUsePlatformSkills(int skillLevel)
    {
        // if another teleport skill(=21165) is queued with level != 10 -> next stage is ready so stop switching platforms
        return !GetOwner().HasQueuedSkill(skill => skill.GetSkillId() == 21165 && skill.GetSkillLevel() != 10 && skill.GetSkillLevel() != skillLevel);
    }

    protected override void HandleDied()
    {
        if (positionUpdateTask != null && !positionUpdateTask.IsCancelled)
        {
            positionUpdateTask.Cancel(false);
        }
        base.HandleDied();
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
}
