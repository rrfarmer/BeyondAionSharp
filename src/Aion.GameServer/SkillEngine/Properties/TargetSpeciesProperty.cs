using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.SkillEngine.Properties;

/// <summary>Java parity: skillengine/properties/TargetSpeciesProperty. Java List.removeIf → C# List.RemoveAll.</summary>
public class TargetSpeciesProperty
{
    public static bool Set(Properties properties, Properties.ValidationResult result)
    {
        switch (properties.GetTargetSpecies())
        {
            case TargetSpecies.NPC:
                result.GetTargets().RemoveAll(effected => !(effected is Npc));
                break;
            case TargetSpecies.PC:
                result.GetTargets().RemoveAll(effected => !(effected is Player));
                break;
        }
        return true;
    }
}
