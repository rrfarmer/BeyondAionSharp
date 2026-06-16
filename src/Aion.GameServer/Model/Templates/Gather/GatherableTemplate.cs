using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Gather;

/// <summary>Java parity: model/templates/gather/GatherableTemplate (ATracer, KID). extends VisibleObjectTemplate.</summary>
[XmlRoot("gatherable_template")]
public class GatherableTemplate : VisibleObjectTemplate
{
    [XmlElement("materials")] public Materials materials;
    [XmlElement("exmaterials")] public ExMaterials exmaterials;
    [XmlAttribute("id")] public int id;
    [XmlAttribute("name")] public string name;
    [XmlAttribute("nameId")] public int nameId;
    [XmlAttribute("sourceType")] public string sourceType;
    [XmlAttribute("harvestCount")] public int harvestCount;
    [XmlAttribute("skillLevel")] public int skillLevel;
    [XmlAttribute("harvestSkill")] public int harvestSkill;
    [XmlAttribute("successAdj")] public int successAdj;
    [XmlAttribute("failureAdj")] public int failureAdj;
    [XmlAttribute("aerialAdj")] public int aerialAdj;
    [XmlAttribute("captcha")] public int captcha;
    [XmlAttribute("lvlLimit")] public int lvlLimit;
    [XmlAttribute("reqItem")] public int reqItem;
    [XmlAttribute("reqItemNameId")] public int reqItemNameId;
    [XmlAttribute("checkType")] public int checkType;
    [XmlAttribute("eraseValue")] public int eraseValue;

    public Materials GetMaterials()
    {
        return materials;
    }

    public ExMaterials GetExtraMaterials()
    {
        return exmaterials;
    }

    public override int GetTemplateId()
    {
        return id;
    }

    public int GetAerialAdj()
    {
        return aerialAdj;
    }

    public int GetFailureAdj()
    {
        return failureAdj;
    }

    public int GetSuccessAdj()
    {
        return successAdj;
    }

    public int GetHarvestSkill()
    {
        return harvestSkill;
    }

    public int GetSkillLevel()
    {
        return skillLevel;
    }

    public int GetHarvestCount()
    {
        return harvestCount;
    }

    public string GetSourceType()
    {
        return sourceType;
    }

    public override string GetName()
    {
        return name;
    }

    public override int GetL10nId()
    {
        return nameId;
    }

    public int GetCaptchaRate()
    {
        return captcha;
    }

    public int GetLevelLimit()
    {
        return lvlLimit;
    }

    public int GetRequiredItemId()
    {
        return reqItem;
    }

    public int GetRequiredItemNameId()
    {
        return reqItemNameId;
    }

    public int GetCheckType()
    {
        return checkType;
    }

    public int GetEraseValue()
    {
        return eraseValue;
    }
}
