using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Gather;

/// <summary>Java parity: model/templates/gather/GatherableTemplate (ATracer, KID). extends VisibleObjectTemplate.</summary>
[XmlRoot("gatherable_template")]
public class GatherableTemplate : VisibleObjectTemplate
{
    [XmlElement("materials")] protected Materials materials;
    [XmlElement("exmaterials")] protected ExMaterials exmaterials;
    [XmlAttribute("id")] protected int id;
    [XmlAttribute("name")] protected string name;
    [XmlAttribute("nameId")] protected int nameId;
    [XmlAttribute("sourceType")] protected string sourceType;
    [XmlAttribute("harvestCount")] protected int harvestCount;
    [XmlAttribute("skillLevel")] protected int skillLevel;
    [XmlAttribute("harvestSkill")] protected int harvestSkill;
    [XmlAttribute("successAdj")] protected int successAdj;
    [XmlAttribute("failureAdj")] protected int failureAdj;
    [XmlAttribute("aerialAdj")] protected int aerialAdj;
    [XmlAttribute("captcha")] protected int captcha;
    [XmlAttribute("lvlLimit")] protected int lvlLimit;
    [XmlAttribute("reqItem")] protected int reqItem;
    [XmlAttribute("reqItemNameId")] protected int reqItemNameId;
    [XmlAttribute("checkType")] protected int checkType;
    [XmlAttribute("eraseValue")] protected int eraseValue;

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
