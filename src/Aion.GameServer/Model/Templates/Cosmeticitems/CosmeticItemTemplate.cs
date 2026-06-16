using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Cosmeticitems;

/// <summary>Java parity: model/templates/cosmeticitems/CosmeticItemTemplate (xTz).</summary>
[XmlType("CosmeticItemTemplate")]
public class CosmeticItemTemplate
{
    [XmlAttribute("type")] public string type;
    [XmlAttribute("cosmetic_name")] public string cosmeticName;
    [XmlAttribute("id")] public int id;
    [XmlAttribute("race")] public Race race;
    [XmlAttribute("gender_permitted")] public string genderPermitted;
    [XmlElement("preset")] public Preset preset;

    public string GetType_()
    {
        return type;
    }

    public string GetCosmeticName()
    {
        return cosmeticName;
    }

    public int GetId()
    {
        return id;
    }

    public Race GetRace()
    {
        return race;
    }

    public string GetGenderPermitted()
    {
        return genderPermitted;
    }

    public Preset GetPreset()
    {
        return preset;
    }

    [XmlType("Preset")]
    public class Preset
    {
        [XmlElement("scale")] public float scale;
        [XmlElement("hair_type")] public int hairType;
        [XmlElement("face_type")] public int faceType;
        [XmlElement("hair_color")] public int hairColor;
        [XmlElement("lip_color")] public int lipColor;
        [XmlElement("eye_color")] public int eyeColor;
        [XmlElement("skin_color")] public int skinColor;

        public float GetScale()
        {
            return scale;
        }

        public int GetHairType()
        {
            return hairType;
        }

        public int GetFaceType()
        {
            return faceType;
        }

        public int GetHairColor()
        {
            return hairColor;
        }

        public int GetLipColor()
        {
            return lipColor;
        }

        public int GetEyeColor()
        {
            return eyeColor;
        }

        public int GetSkinColor()
        {
            return skinColor;
        }
    }
}
