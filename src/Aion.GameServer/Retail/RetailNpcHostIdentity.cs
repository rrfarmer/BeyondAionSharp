using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Retail;

/// <summary>Identity of the complete XML-bound, initialized host graph. It detects
/// reference-version drift before a reviewed cohort reaches any spawned actor.</summary>
internal static class RetailNpcHostIdentity
{
    private static readonly XmlSerializer Npcs = new(typeof(NpcTemplate));
    private static readonly XmlSerializer Skills = new(typeof(SkillTemplate));
    internal static string Template(NpcTemplate template) => Digest(Npcs,template);
    internal static string Skill(SkillTemplate template) => Digest(Skills,template);
    private static string Digest(XmlSerializer serializer,object graph)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        serializer.Serialize(writer,graph);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(writer.ToString())));
    }
}
