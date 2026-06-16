using System.Globalization;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>
/// A single spawn-spot coordinate record inside a SpawnGroup.
/// Java parity: model/templates/spawns/SpawnSpotTemplate.
/// </summary>
/// <remarks>
/// Java uses JAXB @XmlAccessorType(FIELD); all fields serialized via field annotations.
/// C# XmlSerializer uses [XmlAttribute]/[XmlElement] on properties.
///
/// Java beforeMarshal / afterMarshal null-out zero integer fields before writing XML (to keep
/// output clean) then restore them after. C# equivalent: ShouldSerialize{Name}() methods which
/// XmlSerializer calls to decide whether to include an attribute.
///
/// Java afterUnmarshal interns the ai string for memory efficiency. C# equivalent: string.Intern()
/// in the Ai property setter.
/// </remarks>
[XmlType("SpawnSpotTemplate")]
public class SpawnSpotTemplate
{
    // Java parity: float x, y, z, byte h — required spawn coordinates
    [XmlAttribute("x")] public float X { get; set; }
    [XmlAttribute("y")] public float Y { get; set; }
    [XmlAttribute("z")] public float Z { get; set; }
    [XmlAttribute("h")] public byte H { get; set; }

    // Java parity: Integer staticId = 0 — omitted from XML when 0 (beforeMarshal nulls it)
    [XmlAttribute("static_id")]
    public int StaticId { get; set; } = 0;

    // Java parity: Integer randomWalk = 0 — omitted when 0
    [XmlAttribute("random_walk")]
    public int RandomWalk { get; set; } = 0;

    // Java parity: String walkerId
    [XmlAttribute("walker_id")]
    public string? WalkerId { get; set; }

    // Java parity: Integer walkerIdx — omitted when 0. XmlSerializer cannot bind a Nullable<int> attribute, so the
    // typed backing property stays faithful ([XmlIgnore]) and a public string proxy carries the wire value.
    [XmlIgnore]
    public int? WalkerIdx { get; set; }

    [XmlAttribute("walker_index")]
    public string? WalkerIdxRaw
    {
        get => WalkerIdx?.ToString(CultureInfo.InvariantCulture);
        set => WalkerIdx = string.IsNullOrEmpty(value) ? null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    // Java parity: String anchor
    [XmlAttribute("anchor")]
    public string? Anchor { get; set; }

    // Java parity: Integer state = 0 — omitted when 0
    [XmlAttribute("state")]
    public int State { get; set; } = 0;

    // Java parity: String ai — interned after unmarshal for memory efficiency
    private string? _ai;
    [XmlAttribute("ai")]
    public string? Ai
    {
        get => _ai;
        set => _ai = value != null ? string.Intern(value) : null; // Java parity: ai.intern() in afterUnmarshal
    }

    // Java parity: @XmlElement(name = "temporary_spawn") TemporarySpawn temporaySpawn
    [XmlElement("temporary_spawn")]
    public TemporarySpawn? TemporarySpawn { get; set; }

    public SpawnSpotTemplate() { }

    // Java parity: SpawnSpotTemplate(float x, float y, float z, byte h, int randomWalk, String walkerId, Integer walkerIndex)
    public SpawnSpotTemplate(float x, float y, float z, byte h, int randomWalk, string? walkerId, int? walkerIndex)
    {
        X = x;
        Y = y;
        Z = z;
        H = h;
        if (randomWalk > 0)
            RandomWalk = randomWalk;
        WalkerId = walkerId;
        WalkerIdx = walkerIndex;
    }

    // Java parity: getX(), getY(), getZ()
    public float GetX() => X;
    public float GetY() => Y;
    public float GetZ() => Z;

    // Java parity: getHeading()
    public byte GetHeading() => H;

    // Java parity: getStaticId() / setStaticId(int)
    public int GetStaticId() => StaticId;
    public void SetStaticId(int staticId) => StaticId = staticId;

    // Java parity: getWalkerId() / setWalkerId(String)
    public string? GetWalkerId() => WalkerId;
    public void SetWalkerId(string? walkerId) => WalkerId = walkerId;

    // Java parity: getWalkerIndex()
    public int? GetWalkerIndex() => WalkerIdx;

    // Java parity: getRandomWalk()
    public int GetRandomWalk() => RandomWalk;

    // Java parity: getAnchor()
    public string? GetAnchor() => Anchor;

    // Java parity: getAi()
    public string? GetAi() => _ai;

    // Java parity: getState() — returns 0 if null (field is never null here, but guard preserved)
    public int GetState() => State;

    // Java parity: getTemporarySpawn()
    public TemporarySpawn? GetTemporarySpawn() => TemporarySpawn;

    // Java parity: beforeMarshal — omit zero-valued integer attributes from XML output.
    // C# XmlSerializer calls ShouldSerialize{Name}() before writing each attribute/element.
    public bool ShouldSerializeStaticId() => StaticId != 0;
    public bool ShouldSerializeRandomWalk() => RandomWalk != 0;
    public bool ShouldSerializeState() => State != 0;
    public bool ShouldSerializeWalkerIdxRaw() => WalkerIdx.HasValue && WalkerIdx.Value != 0;
}
