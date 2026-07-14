using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Model.Templates.Npc;

/// <summary>Java parity: model/templates/npc/NpcTemplate (Luno). extends CreatureTemplate.</summary>
[XmlRoot("npc_template")]
[XmlType("npc_template")]
public class NpcTemplate : CreatureTemplate
{
    // Public so XmlSerializer can populate these (JAXB read private fields via @XmlAccessorType(FIELD));
    // faithful Java field names + [Xml*] attribute/element names unchanged.
    [XmlIgnore] public int npcId;

    [XmlAttribute("level")] public byte level;

    [XmlAttribute("name_id")] public int nameId;

    [XmlAttribute("title_id")] public int titleId;

    [XmlAttribute("name")] public string name;

    [XmlAttribute("group_drop")] public GroupDropType groupDrop;

    [XmlAttribute("height")] public float height = 1;

    [XmlElement("stats")] public StatsTemplate statsTemplate;

    // Java parity: @XmlElement("equipment") @XmlJavaTypeAdapter(NpcEquippedGearAdapter) NpcEquippedGear.
    // XmlSerializer cannot bind NpcEquippedGear (no parameterless ctor, no public bound members) nor resolve
    // the @XmlIDREF <item> ids (ItemData is loaded separately); bind the raw <equipment> element into a
    // NpcEquipmentList proxy that captures the item ids, then build the gear lazily (matches Java's
    // adapter.unmarshal → NpcEquippedGear.Init() deferred resolution).
    [XmlElement("equipment")] public Aion.GameServer.Dataholders.LoadingUtils.Adapters.NpcEquipmentList equipmentList;

    [XmlIgnore] private Aion.GameServer.Model.Items.NpcEquippedGear equipment;

    [XmlElement("kisk_stats")] public KiskStatsTemplate kiskStatsTemplate;

    [XmlElement("ammo_speed")] public int ammoSpeed = 0;

    [XmlAttribute("rank")] public NpcRank rank;

    [XmlAttribute("rating")] public NpcRating rating;

    [XmlAttribute("srange")] public int aggrorange;

    [XmlAttribute("sangle")] public int aggroAngle = 360;

    [XmlAttribute("arange")] public int attackRange;

    [XmlAttribute("attack_speed")] public int attackSpeed = 2000;

    [XmlAttribute("cast_speed")] public int castSpeed = 1000;

    [XmlAttribute("flag_type")] public int flagType;

    [XmlAttribute("war_flag")] public int warFlagGroupId;

    /*
     * [XmlAttribute("item_upgrade")] private int itemUpgrade;
     */

    [XmlAttribute("hpgauge")] public int hpGauge;

    // Java's TribeClass reference is null when the XML attribute is absent. Keep a nullable backing
    // value so null-sensitive callers do not accidentally see the ordinal-zero C# enum member.
    [XmlIgnore] private TribeClass? tribe;

    [XmlAttribute("tribe")]
    public string? TribeRaw
    {
        get => tribe?.ToString();
        set => tribe = value == null ? null : System.Enum.Parse<TribeClass>(value);
    }

    [XmlAttribute("ai")] public string ai;

    [XmlAttribute("race")] public Race race = Race.NONE;

    [XmlAttribute("state")] public int state;

    [XmlAttribute("floatcorpse")] public bool floatcorpse;

    [XmlElement("bound_radius")] public BoundRadius boundRadius;

    // Java parity: nullable enum attribute (default null → getter returns NONE).
    [XmlIgnore] private NpcTemplateType? npcTemplateType;

    [XmlAttribute("type")]
    public string NpcTemplateTypeXml
    {
        get => npcTemplateType?.ToString();
        set => npcTemplateType = value == null ? (NpcTemplateType?)null : (NpcTemplateType)System.Enum.Parse(typeof(NpcTemplateType), value);
    }

    // Java parity: nullable enum attribute (default null → getter returns NONE).
    [XmlIgnore] private AbyssNpcType? abyssNpcType;

    [XmlAttribute("abyss_type")]
    public string AbyssNpcTypeXml
    {
        get => abyssNpcType?.ToString();
        set => abyssNpcType = value == null ? (AbyssNpcType?)null : (AbyssNpcType)System.Enum.Parse(typeof(AbyssNpcType), value);
    }

    [XmlElement("talk_info")] public TalkInfo talkInfo;

    [XmlElement("massive_loot")] public MassiveLoot massiveLoot;

    // Java parity: afterUnmarshal(Unmarshaller, Object) — invoked per-element post-load. XmlSerializer does not
    // call it automatically, so NpcData.Init invokes it on each template (object parent overload for the loader).
    public void AfterUnmarshal(object parent)
    {
        if (level > 1 && !"noaction".Equals(ai) && GetAbyssNpcType().Equals(AbyssNpcType.TELEPORTER)) // TODO: reparse npc_template
            ai = "siege_teleporter";
        if (ai != null)
            ai = string.Intern(ai);
    }

    public override int GetTemplateId()
    {
        return npcId;
    }

    public override int GetL10nId()
    {
        return nameId;
    }

    public int GetTitleId()
    {
        return titleId;
    }

    public override string GetName()
    {
        return name;
    }

    public float GetHeight()
    {
        return height;
    }

    public Aion.GameServer.Model.Items.NpcEquippedGear GetEquipment()
    {
        // Java parity: the NpcEquippedGearAdapter unmarshals <equipment> into a NpcEquippedGear wrapping the
        // NpcEquipmentList; build it lazily here from the bound proxy (item resolution stays deferred in Init()).
        if (equipment == null && equipmentList != null)
            equipment = new Aion.GameServer.Model.Items.NpcEquippedGear(equipmentList);
        return equipment;
    }

    public byte GetLevel()
    {
        return level;
    }

    public StatsTemplate GetStatsTemplate()
    {
        return statsTemplate;
    }

    public KiskStatsTemplate GetKiskStatsTemplate()
    {
        return kiskStatsTemplate;
    }

    public TribeClass GetTribe()
    {
        return tribe ?? default;
    }

    public TribeClass? GetNullableTribe()
    {
        return tribe;
    }

    public override string GetAiName()
    {
        return ai;
    }

    public override string ToString()
    {
        return "Npc Template id: " + npcId + " name: " + name;
    }

    // Java parity: @XmlID @XmlAttribute("npc_id") setXmlUid(String) — id must arrive as a string.
    [XmlAttribute("npc_id")]
    public string XmlUid
    {
        get => npcId.ToString();
        set => npcId = int.Parse(value);
    }

    public NpcRank GetRank()
    {
        return rank;
    }

    public NpcRating GetRating()
    {
        return rating;
    }

    public int GetAggroRange()
    {
        return aggrorange;
    }

    public int GetAggroAngle()
    {
        return aggroAngle;
    }

    public int GetMinimumShoutRange()
    {
        if (aggrorange < 10)
            return 10;
        return aggrorange;
    }

    public int GetAttackRange()
    {
        return attackRange;
    }

    public int GetCastSpeed()
    {
        return castSpeed;
    }

    public int GetAttackSpeed()
    {
        return attackSpeed;
    }

    public int GetFlagType()
    {
        return flagType;
    }

    public int GetWarFlag()
    {
        return warFlagGroupId;
    }

    /*
     * public int getItemUpgrade() { return itemUpgrade; }
     */

    public int GetHpGauge()
    {
        return hpGauge;
    }

    public Race GetRace()
    {
        return race;
    }

    public int GetState()
    {
        return state;
    }

    public override BoundRadius GetBoundRadius()
    {
        // TODO all npcs should have BR in xml
        return boundRadius != null ? boundRadius : base.GetBoundRadius();
    }

    public NpcTemplateType GetNpcTemplateType()
    {
        return npcTemplateType != null ? npcTemplateType.Value : NpcTemplateType.NONE;
    }

    public AbyssNpcType GetAbyssNpcType()
    {
        return abyssNpcType != null ? abyssNpcType.Value : AbyssNpcType.NONE;
    }

    public int GetTalkDistance()
    {
        return talkInfo == null ? 2 : talkInfo.GetDistance();
    }

    public int GetTalkDelay()
    {
        return talkInfo == null ? 0 : talkInfo.GetDelay();
    }

    public List<int> GetFuncDialogIds()
    {
        return talkInfo == null ? null : talkInfo.GetFuncDialogIds();
    }

    /// <param name="dialogActionId">action</param>
    /// <returns>True if the npc supports this function/action.</returns>
    public bool SupportsAction(int dialogActionId)
    {
        List<int> dialogIds = GetFuncDialogIds();
        return dialogIds != null && dialogIds.Contains(dialogActionId);
    }

    public int GetMassiveLootCount()
    {
        return massiveLoot.GetMLootCount();
    }

    public int GetMassiveLootItem()
    {
        return massiveLoot.GetMLootItem();
    }

    public int GetMassiveLootMinLevel()
    {
        return massiveLoot.GetMLootMinLevel();
    }

    public int GetMassiveLootMaxLevel()
    {
        return massiveLoot.GetMLootMaxLevel();
    }

    /// <returns>if no data is present for the talk</returns>
    public bool CanInteract()
    {
        return talkInfo != null;
    }

    /// <returns>the hasDialog</returns>
    public bool IsDialogNpc()
    {
        return talkInfo != null && talkInfo.IsDialogNpc();
    }

    public TalkInfo GetTalkInfo()
    {
        return talkInfo;
    }

    public MassiveLoot GetMassiveLoot()
    {
        return massiveLoot;
    }

    /// <returns>the floatcorpse</returns>
    public bool IsFloatCorpse()
    {
        return floatcorpse;
    }

    public GroupDropType GetGroupDrop()
    {
        return groupDrop;
    }
}
