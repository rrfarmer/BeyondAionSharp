using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("drakenspire_dimensional_wave")]
public class DimensionalWaveAI : UseSkillAndDieAI
{
    public DimensionalWaveAI(Npc owner) : base(owner)
    {
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21620)
        {
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                CalculateAndApplyDamage();
                return ValueTask.CompletedTask;
            }, 1200L); // Aligns visual hit and damage
        }
    }

    private void CalculateAndApplyDamage()
    {
        foreach (var p in GetKnownList().StreamPlayers())
        {
            if (p.IsDead() || !IsInRange(p, 29))
                continue;

            int headingTowardsPlayer = PositionUtil.GetHeadingTowards(GetPosition().GetX(), GetPosition().GetY(), p.GetX(), p.GetY());
            int headingMax = GetPosition().GetHeading(); // 30 or 90
            int headingMin = headingMax - 60;
            if (headingMin < 0)
            {
                headingMin += 120;
            }

            bool isHit;
            if (headingMin <= headingMax)
            {
                isHit = headingTowardsPlayer >= headingMin && headingTowardsPlayer <= headingMax;
            }
            else
            {
                isHit = headingTowardsPlayer >= headingMin || headingTowardsPlayer <= headingMax;
            }

            if (isHit)
            {
                SkillEngine.SkillEngine.GetInstance().ApplyEffect(21874, GetOwner(), p);
            }
        }
    }
}
