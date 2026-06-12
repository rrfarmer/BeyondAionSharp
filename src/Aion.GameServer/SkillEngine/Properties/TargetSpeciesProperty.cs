using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.SkillEngine.Properties;

/// <summary>Java parity: skillengine/properties/TargetSpeciesProperty. Java List.removeIf → C# List.RemoveAll.</summary>
public class TargetSpeciesProperty
{
    public static bool Set(Properties properties, Properties.ValidationResult result)
    {
        switch (properties.GetTargetSpecies())
        {
            case TargetSpeciesAttribute.NPC:
                result.GetTargets().RemoveAll(effected => !(effected is Npc));
                break;
            case TargetSpeciesAttribute.PC:
                result.GetTargets().RemoveAll(effected => !(effected is Player));
                break;
        }
        return true;
    }
}
