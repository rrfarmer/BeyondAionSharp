namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>
/// String matching function for NPC name filter rules.
/// Java parity: model/templates/globaldrops/StringFunction.
/// </summary>
public enum StringFunction
{
    StartWith,
    EndWith,
    Contains,
    Equals,
}
