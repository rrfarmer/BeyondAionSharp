using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Cosmeticitems;

/// <summary>Java parity: model/templates/cosmeticitems/CosmeticItemTemplate (xTz).</summary>
[XmlType("CosmeticItemTemplate")]
public class CosmeticItemTemplate
{
    [XmlAttribute("type")] private string type;
    [XmlAttribute("cosmetic_name")] private string cosmeticName;
    [XmlAttribute("id")] private int id;
    [XmlAttribute("race")] private Race race;
    [XmlAttribute("gender_permitted")] private string genderPermitted;
    [XmlElement("preset")] private Preset preset;

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
        [XmlElement("scale")] private float scale;
        [XmlElement("hair_type")] private int hairType;
        [XmlElement("face_type")] private int faceType;
        [XmlElement("hair_color")] private int hairColor;
        [XmlElement("lip_color")] private int lipColor;
        [XmlElement("eye_color")] private int eyeColor;
        [XmlElement("skin_color")] private int skinColor;

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
