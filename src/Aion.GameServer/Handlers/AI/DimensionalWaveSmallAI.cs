using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("drakenspire_dimensional_wave_small")]
public class DimensionalWaveSmallAI : UseSkillAndDieAI
{
    public DimensionalWaveSmallAI(Npc owner) : base(owner)
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
            if (!p.IsDead() && IsInRange(p, 22))
                SkillEngine.SkillEngine.GetInstance().ApplyEffect(21874, GetOwner(), p);
        }
    }
}
