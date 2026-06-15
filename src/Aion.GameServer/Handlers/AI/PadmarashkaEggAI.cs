using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/padmarashkasCave/PadmarashkaEggAI (@author Ritsu).</summary>
[AIName("padmarashkaegg")]
public class PadmarashkaEggAI : NpcAI
{
    bool isSmallEggProtectorSpawned = false;
    bool isHugeEggProtectorSpawned = false;
    private Npc protector = null;

    public PadmarashkaEggAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDied()
    {
        if (protector != null && !protector.IsDead())
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(protector, 20176, 55, protector).UseNoAnimationSkill(); // apply wrath buff
        }
        base.HandleDied();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (!isSmallEggProtectorSpawned && this.GetNpcId() == 282613)
        {
            switch (Rnd.Get(1, 6))
            {
                case 1:
                    protector = (Npc)Spawn(282715, 579.415f, 168.109f, 66.000f, (sbyte)0);
                    break;
                case 2:
                    protector = (Npc)Spawn(282715, 581.316f, 157.520f, 66.000f, (sbyte)0);
                    break;
                case 3:
                    protector = (Npc)Spawn(282715, 575.073f, 147.338f, 66.000f, (sbyte)0);
                    break;
                case 4:
                    protector = (Npc)Spawn(282715, 585.119f, 150.989f, 66.000f, (sbyte)0);
                    break;
                case 5:
                    protector = (Npc)Spawn(282716, 581.141f, 148.342f, 66.000f, (sbyte)0);
                    break;
                case 6:
                    protector = (Npc)Spawn(282716, 584.240f, 142.233f, 66.000f, (sbyte)0);
                    break;
            }
            isSmallEggProtectorSpawned = true;
        }
        else if (!isHugeEggProtectorSpawned && this.GetNpcId() == 282614)
        {
            SpawnEliteCommander(); // Random spawn SpawnEliteCommander to protect Egg
            isHugeEggProtectorSpawned = true;
        }
    }

    private void SpawnEliteCommander()
    {
        protector = (Npc)RndSpawnInRange(282712, 5);
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        switch (this.GetNpcId())
        {
            case 282613:
                SmallEggSpawn();
                break;
            case 282614:
                HugeEggSpawn();
                break;
        }
    }

    private void SmallEggSpawn()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead() && GetOwner().IsSpawned())
            {
                AIActions.DeleteOwner(this);
                AttackPlayer((Npc)Spawn(282616, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0));
            }
            return ValueTask.CompletedTask;
        }, 60000L); // TODO: Need right value
    }

    private void HugeEggSpawn()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead() && GetOwner().IsSpawned())
            {
                AIActions.DeleteOwner(this);
                AttackPlayer((Npc)Spawn(282620, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0));
            }
            return ValueTask.CompletedTask;
        }, 120000L); // TODO: Need right value
    }

    private void AttackPlayer(Npc npc)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Npc padma = GetOwner().GetPosition().GetWorldMapInstance().GetNpc(218756);
            if (padma != null)
            {
                npc.SetTarget(padma.GetTarget());
                npc.GetAi().SetStateIfNot(AIState.WALKING);
                npc.SetState(CreatureState.ACTIVE, true);
                npc.GetMoveController().MoveToTargetObject();
                PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
            }
            return ValueTask.CompletedTask;
        }, 1000L);
    }
}
