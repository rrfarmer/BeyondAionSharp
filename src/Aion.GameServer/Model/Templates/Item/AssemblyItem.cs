using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Assembled-item recipe: result id + part item ids.
/// Java parity: model/templates/item/AssemblyItem (@XmlType("AssemblyItem")).
/// </summary>
[XmlType("AssemblyItem")]
public class AssemblyItem
{
    [XmlAttribute("parts")] public string? PartsRaw { get; set; }
    [XmlAttribute("id")] public int Id { get; set; }
    private List<int>? _parts;

    // Java parity: getParts() — lazily initialized list (parsed from the space-separated attribute).
    public List<int> GetParts()
    {
        if (_parts == null)
            _parts = string.IsNullOrWhiteSpace(PartsRaw)
                ? new List<int>()
                : PartsRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        return _parts;
    }

    public int GetId() => Id;
    public void SetId(int value) => Id = value;
}
