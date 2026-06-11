using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Services.Summons;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/PetOrderUnSummonEffect (Bobobear) : EffectTemplate. applyEffect: effected is Player→summon; summon!=null→SummonsService.doMode(RELEASE, summon, UNSPECIFIED). Summon/SummonsService red-tolerated.</summary>
[XmlType("PetOrderUnSummonEffect")]
public class PetOrderUnSummonEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        if (effected is Player)
        {
            Summon summon = ((Player)effected).GetSummon();
            if (summon != null)
            {
                SummonsService.DoMode(SummonMode.RELEASE, summon, UnsummonType.UNSPECIFIED);
            }
        }
    }
}
