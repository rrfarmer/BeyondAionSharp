using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.SkillEngine.Effect.Modifier;

/// <summary>
/// Java parity: skillengine/effect/modifier/TargetRaceDamageModifier (ATracer, Sippolo, kecimis).
/// </summary>
public class TargetRaceDamageModifier : ActionModifier
{
    [XmlAttribute("race")]
    public Race skillTargetRace;

    public override int Analyze(SkillEngine.Model.Effect effect)
    {
        Creature effected = effect.GetEffected();

        int newValue = Value + effect.GetSkillLevel() * Delta;
        if (effected is Player)
        {
            Player player = (Player)effected;
            switch (skillTargetRace)
            {
                case Race.ASMODIANS:
                    if (player.GetRace() == Race.ASMODIANS)
                        return newValue;
                    break;
                case Race.ELYOS:
                    if (player.GetRace() == Race.ELYOS)
                        return newValue;
                    break;
            }
        }
        else if (effected is Npc)
        {
            Npc npc = (Npc)effected;
            if (npc.GetObjectTemplate().GetRace().ToString() == skillTargetRace.ToString())
                return newValue;
            else
                return 0;
        }

        return 0;
    }

    public override bool Check(SkillEngine.Model.Effect effect)
    {
        Creature effected = effect.GetEffected();
        if (effected is Player)
        {
            Player player = (Player)effected;
            Race race = player.GetRace();
            return (race == Race.ASMODIANS && skillTargetRace == Race.ASMODIANS) || (race == Race.ELYOS && skillTargetRace == Race.ELYOS);
        }
        else if (effected is Npc)
        {
            Npc npc = (Npc)effected;

            Race race = npc.GetObjectTemplate().GetRace();
            // Java: race.toString().equals(...) — race is the template enum value
            return race.ToString() == skillTargetRace.ToString();
        }

        return false;
    }
}
