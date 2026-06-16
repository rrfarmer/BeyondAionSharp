using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>
/// String matching function for NPC name filter rules.
/// Java parity: model/templates/globaldrops/StringFunction (@XmlEnum SCREAMING_SNAKE wire names).
/// XmlSerializer matches enum members to the XML attribute value by name; the JAXB wire names are
/// SCREAMING_SNAKE (START_WITH/END_WITH/CONTAINS/EQUALS) so each PascalCase member carries an [XmlEnum] alias.
/// </summary>
public enum StringFunction
{
    [XmlEnum("START_WITH")] StartWith,
    [XmlEnum("END_WITH")] EndWith,
    [XmlEnum("CONTAINS")] Contains,
    [XmlEnum("EQUALS")] Equals,
}
