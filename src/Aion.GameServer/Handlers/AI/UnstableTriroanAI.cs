using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/theobomosLab/UnstableTriroanAI (@author Ritsu).
/// </summary>
[AIName("triroan")]
public class UnstableTriroanAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(99, 90, 80, 70, 60, 50, 40, 30, 20, 10, 5);

    public UnstableTriroanAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (hpPhases.GetCurrentPhase() > 0 && GetLifeStats().IsFullyRestoredHp())
            hpPhases.Reset();
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 99:
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 16699, 1, GetOwner()).UseSkill();
                break;
            case 90:
                SpawnFire();
                break;
            case 80:
                SpawnWater();
                break;
            case 70:
                SpawnEarth();
                break;
            case 60:
                SpawnWind();
                break;
            case 50:
                SpawnFire();
                break;
            case 40:
                SpawnFire();
                SpawnWater();
                break;
            case 30:
                SpawnEarth();
                SpawnWind();
                break;
            case 20:
                SpawnWind();
                SpawnFire();
                break;
            case 10:
                SpawnWater();
                SpawnEarth();
                break;
            case 5:
                SpawnWind();
                SpawnFire();
                SpawnWater();
                SpawnEarth();
                break;
        }
    }

    private void SpawnFire()
    {
        StartWalk((Npc)Spawn(280975, 601.966f, 488.853f, 196.019f, (sbyte)0), "3101100002");
    }

    private void SpawnWater()
    {
        StartWalk((Npc)Spawn(280976, 601.966f, 488.853f, 196.019f, (sbyte)0), "3101100003");
    }

    private void SpawnEarth()
    {
        StartWalk((Npc)Spawn(280977, 601.966f, 488.853f, 196.019f, (sbyte)0), "3101100004");
    }

    private void SpawnWind()
    {
        StartWalk((Npc)Spawn(280978, 601.966f, 488.853f, 196.019f, (sbyte)0), "3101100005");
    }

    private void StartWalk(Npc npc, string walkId)
    {
        npc.GetSpawn().SetWalkerId(walkId);
        WalkManager.StartWalking((NpcAI)npc.GetAi());
        npc.SetState(CreatureState.ACTIVE, true);
        PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }
}
