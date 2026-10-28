using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Aion.GameServer.Retail;

/// <summary>Complete pinned skill identity/XML, independent of any damage family.</summary>
internal sealed record RetailNpcSkillRecord(int Id, string Name, XElement Root)
{
    internal static RetailNpcSkillRecord Read(ReadOnlySpan<byte> bytes, string sha256)
    {
        if (bytes.Length is <= 0 or > 131072 || bytes.Length % 2 != 0 || sha256.Length != 64
            || Convert.ToHexStringLower(SHA256.HashData(bytes)) != sha256)
            throw new InvalidDataException("Statistics skill source size/hash differs");
        using var reader = XmlReader.Create(new StringReader(new UnicodeEncoding(false, false, true).GetString(bytes)),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
        var root = XDocument.Load(reader, LoadOptions.PreserveWhitespace).Root;
        if (root is null || root.Name != "skill_base" || root.HasAttributes
            || root.Elements().Any(x => x.Name.NamespaceName.Length != 0 || x.HasAttributes)
            || root.Descendants().Any(x => x.Ancestors().Count() > 24))
            throw new InvalidDataException("Statistics skill source root/shape differs");
        string Required(string name)
        {
            var rows = root.Elements(name).ToArray();
            if (rows.Length != 1 || rows[0].HasElements || rows[0].HasAttributes)
                throw new InvalidDataException("Statistics skill identity field missing/duplicated: " + name);
            return rows[0].Value;
        }
        if (!int.TryParse(Required("id"), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
            throw new InvalidDataException("Statistics skill ID invalid");
        string skillName = Required("name");
        if (string.IsNullOrWhiteSpace(skillName) || skillName.Length > 256)
            throw new InvalidDataException("Statistics skill name invalid");
        return new(id, skillName, root);
    }
}
