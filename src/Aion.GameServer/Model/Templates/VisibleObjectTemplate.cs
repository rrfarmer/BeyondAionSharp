namespace Aion.GameServer.Model.Templates;

/// <summary>
/// Base template for all visible in-game objects. Holds static data loaded from XML.
/// Java parity: model/templates/VisibleObjectTemplate.
/// </summary>
public abstract class VisibleObjectTemplate : IL10n
{
    // Java parity: abstract int getTemplateId() — for NPCs returns the NPC id from templates XML
    public abstract int GetTemplateId();

    // Java parity: abstract String getName() — for NPCs returns the name from templates XML
    public abstract string GetName();

    // Java parity: BoundRadius getBoundRadius() — returns BoundRadius.DEFAULT if not overridden
    public virtual BoundRadius GetBoundRadius() => BoundRadius.Default;

    // Java parity: L10n::getL10nId() — abstract; must be provided by each concrete template
    public abstract int GetL10nId();
}
