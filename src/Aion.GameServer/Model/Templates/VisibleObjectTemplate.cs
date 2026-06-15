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

    // Java parity: L10n::getL10n() default method. Java default methods are callable on the
    // implementing instance; C# default-interface methods are NOT, so provide it concretely here
    // (mirrors the IL10n default body) to keep `template.GetL10n()` callable like the Java original.
    public virtual string? GetL10n() => Aion.GameServer.Utils.ChatUtil.L10n(GetL10nId());
}
