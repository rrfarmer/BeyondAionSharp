using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/unstableSplinterpath/UnstableKaluvaSpawnAI.</summary>
[AIName("unstablekaluvaspawn")]
public class UnstableKaluvaSpawnAI : NpcAI
{
    private ScheduledTask task;

    public UnstableKaluvaSpawnAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        if (task != null && !task.IsDone())
            task.Cancel(true);
        CheckKaluva();
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        OnMoved(creature);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        OnMoved(creature);
    }

    private void OnMoved(Creature creature)
    {
        if (task == null && creature is Npc && ((Npc)creature).GetNpcId() == 219553)
        {
            if (PositionUtil.IsInRange(GetOwner(), creature, 7))
            {
                creature.GetEffectController().RemoveEffect(19152);
                ScheduleHatch();
            }
        }
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19222, 55, GetOwner()).UseNoAnimationSkill();
    }

    private void CheckKaluva()
    {
        Npc kaluva = GetPosition().GetWorldMapInstance().GetNpc(219553);
        if (kaluva != null && !kaluva.IsDead())
        {
            kaluva.GetEffectController().RemoveEffect(19152);
        }
        AIActions.DeleteOwner(this);
    }

    private void ScheduleHatch()
    {
        task = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                HatchAdds();
                CheckKaluva();
            }
            return ValueTask.CompletedTask;
        }, 28000L); // schedule hatch when debuff ends(20s)
    }

    private void HatchAdds() // 4 different spawn-formations; See Powerwiki for more information
    {
        WorldPosition p = GetPosition();
        switch (Rnd.Get(1, 4))
        {
            case 1:
                Spawn(219572, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                Spawn(219572, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                break;
            case 2:
                for (int i = 0; i < 12; i++)
                {
                    Spawn(219573, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                }
                break;
            case 3:
                Spawn(219584, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                break;
            case 4:
                Spawn(219572, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                Spawn(219573, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                Spawn(219573, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                Spawn(219573, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                break;
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}
