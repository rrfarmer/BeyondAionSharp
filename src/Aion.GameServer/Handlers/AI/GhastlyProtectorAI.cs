using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("drakenspire_ghastly_protector")]
public class GhastlyProtectorAI : AggressiveNoLootNpcAI
{
    public GhastlyProtectorAI(Npc owner) : base(owner)
    {
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return ItemAttackType.MAGICAL_WIND;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            AggroPlayer();
            return ValueTask.CompletedTask;
        }, 1000L);
        GetOwner().GetGameStats().SetNextSkillDelay(0);
    }

    private void AggroPlayer()
    {
        var p = GetKnownList().StreamPlayers()
            .FirstOrDefault(p => !p.IsDead() && PositionUtil.IsInRange(p, 152.38f, 518.68f, 1749.6f, 24));
        if (p != null)
            GetAggroList().AddHate(p, 10000);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21883)
            AddHateToRandomTarget();
    }

    private void AddHateToRandomTarget()
    {
        GetAggroList().AddHate(GetAggroList().GetTarget(AggroTarget.RANDOM), 10000);
    }
}
