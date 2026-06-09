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
    private int npcId;

    [XmlAttribute("level")] private byte level;

    [XmlAttribute("name_id")] private int nameId;

    [XmlAttribute("title_id")] private int titleId;

    [XmlAttribute("name")] private string name;

    [XmlAttribute("group_drop")] private GroupDropType groupDrop;

    [XmlAttribute("height")] private float height = 1;

    [XmlElement("stats")] private StatsTemplate statsTemplate;

    [XmlElement("equipment")] private Aion.GameServer.Model.Items.NpcEquippedGear equipment;

    [XmlElement("kisk_stats")] private KiskStatsTemplate kiskStatsTemplate;

    [XmlElement("ammo_speed")] private int ammoSpeed = 0;

    [XmlAttribute("rank")] private NpcRank rank;

    [XmlAttribute("rating")] private NpcRating rating;

    [XmlAttribute("srange")] private int aggrorange;

    [XmlAttribute("sangle")] private int aggroAngle = 360;

    [XmlAttribute("arange")] private int attackRange;

    [XmlAttribute("attack_speed")] private int attackSpeed = 2000;

    [XmlAttribute("cast_speed")] private int castSpeed = 1000;

    [XmlAttribute("flag_type")] private int flagType;

    [XmlAttribute("war_flag")] private int warFlagGroupId;

    /*
     * [XmlAttribute("item_upgrade")] private int itemUpgrade;
     */

    [XmlAttribute("hpgauge")] private int hpGauge;

    [XmlAttribute("tribe")] private TribeClass tribe;

    [XmlAttribute("ai")] private string ai;

    [XmlAttribute("race")] private Race race = Race.NONE;

    [XmlAttribute("state")] private int state;

    [XmlAttribute("floatcorpse")] private bool floatcorpse;

    [XmlElement("bound_radius")] private BoundRadius boundRadius;

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

    [XmlElement("talk_info")] private TalkInfo talkInfo;

    [XmlElement("massive_loot")] private MassiveLoot massiveLoot;

    // Java parity: afterUnmarshal — invoked post-load by the template loader.
    public void AfterUnmarshal()
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
