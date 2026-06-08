using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Base template for all creatures (NPCs). Adds the AI name lookup on top of
/// <see cref="VisibleObjectTemplate"/>.
/// Java parity: model/gameobjects/CreatureTemplate.
/// </summary>
public abstract class CreatureTemplate : VisibleObjectTemplate
{
    // Java parity: getAiName() — returns null unless overridden by a concrete template.
    public virtual string? GetAiName() => null;
}
