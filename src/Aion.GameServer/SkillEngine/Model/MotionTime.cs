using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items.Enums;

namespace Aion.GameServer.Skillengine.Model;

/// <summary>Java parity: skillengine/model/MotionTime. @XmlType("motion_time"); @XmlElement race/gender Times lists + robot; @XmlAttribute name; @XmlTransient→[XmlIgnore] per-WeaponTypeWrapper maps; getTimesFor walks id..1 by race/gender/robot; afterUnmarshal→AfterUnmarshal(object) parses lists into maps then nulls lists; parseTimesFrom switch on weapon string→WeaponTypeWrapper, 2weapon adds SWORD/SWORD+MACE/MACE; computeIfAbsent→local PutInto TryGetValue; IllegalArgumentException→ArgumentException; HashMap→Dictionary, Map.get→GetValueOrDefault. Times/WeaponTypeWrapper red-tolerated.</summary>
[XmlType("motion_time")]
public class MotionTime
{
    [XmlElement("asmodian_female")]
    private List<Times> asmodianFemale;
    [XmlElement("asmodian_male")]
    private List<Times> asmodianMale;
    [XmlElement("elyos_female")]
    private List<Times> elyosFemale;
    [XmlElement("elyos_male")]
    private List<Times> elyosMale;
    [XmlElement("robot")]
    private List<Times> robot;

    [XmlAttribute]
    private string name;

    public string GetName()
    {
        return name;
    }

    [XmlIgnore]
    private Dictionary<WeaponTypeWrapper, Dictionary<int, Times>> asmodianFemaleTimeForWeaponType = new Dictionary<WeaponTypeWrapper, Dictionary<int, Times>>();

    [XmlIgnore]
    private Dictionary<WeaponTypeWrapper, Dictionary<int, Times>> asmodianMaleTimeForWeaponType = new Dictionary<WeaponTypeWrapper, Dictionary<int, Times>>();

    [XmlIgnore]
    private Dictionary<WeaponTypeWrapper, Dictionary<int, Times>> elyosFemaleTimeForWeaponType = new Dictionary<WeaponTypeWrapper, Dictionary<int, Times>>();

    [XmlIgnore]
    private Dictionary<WeaponTypeWrapper, Dictionary<int, Times>> elyosMaleTimeForWeaponType = new Dictionary<WeaponTypeWrapper, Dictionary<int, Times>>();

    [XmlIgnore]
    internal Dictionary<int, Times> robotTimes = new Dictionary<int, Times>();

    public Times GetTimesFor(Player player, int id)
    {
        WeaponTypeWrapper weapons = player.IsInRobotMode() ? null : new WeaponTypeWrapper(player.GetEquipment().GetMainHandWeaponType(), player.GetEquipment().GetOffHandWeaponType());
        for (int i = id; i > 0; i--)
        {
            if (player.IsInRobotMode())
            {
                if (robotTimes.GetValueOrDefault(i) != null)
                {
                    return robotTimes.GetValueOrDefault(i);
                }
            }
            else
            {
                Dictionary<int, Times> times = null;
                switch (player.GetRace())
                {
                    case Race.ASMODIANS:
                        if (player.GetGender() == Gender.FEMALE)
                        {
                            times = asmodianFemaleTimeForWeaponType.GetValueOrDefault(weapons);
                        }
                        else
                        {
                            times = asmodianMaleTimeForWeaponType.GetValueOrDefault(weapons);
                        }
                        break;
                    case Race.ELYOS:
                        if (player.GetGender() == Gender.FEMALE)
                        {
                            times = elyosFemaleTimeForWeaponType.GetValueOrDefault(weapons);
                        }
                        else
                        {
                            times = elyosMaleTimeForWeaponType.GetValueOrDefault(weapons);
                        }
                        break;
                }
                if (times != null)
                {
                    return times.GetValueOrDefault(i);
                }
            }
        }
        return null;
    }

    public void AfterUnmarshal(object parent)
    {
        ParseTimesFrom(asmodianFemale, asmodianFemaleTimeForWeaponType);
        ParseTimesFrom(asmodianMale, asmodianMaleTimeForWeaponType);
        ParseTimesFrom(elyosFemale, elyosFemaleTimeForWeaponType);
        ParseTimesFrom(elyosMale, elyosMaleTimeForWeaponType);
        if (robot != null)
        {
            foreach (Times time in robot)
            {
                robotTimes[time.GetId()] = time;
            }
        }

        asmodianFemale = null;
        asmodianMale = null;
        elyosFemale = null;
        elyosMale = null;
        robot = null;
    }

    private void ParseTimesFrom(List<Times> times, Dictionary<WeaponTypeWrapper, Dictionary<int, Times>> map)
    {
        if (times == null)
        {
            return;
        }
        foreach (Times t in times)
        {
            void PutInto(WeaponTypeWrapper w)
            {
                if (!map.TryGetValue(w, out Dictionary<int, Times> inner))
                {
                    inner = new Dictionary<int, Times>();
                    map[w] = inner;
                }
                inner[t.GetId()] = t;
            }

            WeaponTypeWrapper wrapper;
            switch (t.GetWeapon())
            {
                case "1hand":
                    wrapper = new WeaponTypeWrapper(ItemGroup.SWORD, null);
                    break;
                case "2hand":
                    wrapper = new WeaponTypeWrapper(ItemGroup.GREATSWORD, null);
                    break;
                case "keyblade":
                    wrapper = new WeaponTypeWrapper(ItemGroup.KEYBLADE, null);
                    break;
                case "polearm":
                    wrapper = new WeaponTypeWrapper(ItemGroup.POLEARM, null);
                    break;
                case "dagger":
                    wrapper = new WeaponTypeWrapper(ItemGroup.DAGGER, null);
                    break;
                case "mace":
                    wrapper = new WeaponTypeWrapper(ItemGroup.MACE, null);
                    break;
                case "staff":
                    wrapper = new WeaponTypeWrapper(ItemGroup.STAFF, null);
                    break;
                case "2weapon":
                    wrapper = new WeaponTypeWrapper(ItemGroup.DAGGER, ItemGroup.DAGGER);
                    PutInto(new WeaponTypeWrapper(ItemGroup.SWORD, ItemGroup.SWORD));
                    PutInto(new WeaponTypeWrapper(ItemGroup.MACE, ItemGroup.MACE));
                    // other combinations don't need to be added, as the WeaponTypeWrapper constructor already limits them
                    break;
                case "noweapon":
                    wrapper = new WeaponTypeWrapper(null, null);
                    break;
                case "book":
                    wrapper = new WeaponTypeWrapper(ItemGroup.SPELLBOOK, null);
                    break;
                case "orb":
                    wrapper = new WeaponTypeWrapper(ItemGroup.ORB, null);
                    break;
                case "1gun":
                    wrapper = new WeaponTypeWrapper(ItemGroup.GUN, null);
                    break;
                case "2gun":
                    wrapper = new WeaponTypeWrapper(ItemGroup.GUN, ItemGroup.GUN);
                    break;
                case "cannon":
                    wrapper = new WeaponTypeWrapper(ItemGroup.CANNON, null);
                    break;
                case "bow":
                    wrapper = new WeaponTypeWrapper(ItemGroup.BOW, null);
                    break;
                case "harp":
                    wrapper = new WeaponTypeWrapper(ItemGroup.HARP, null);
                    break;
                default:
                    throw new ArgumentException(t.GetWeapon() + " is not implemented");
            }
            PutInto(wrapper);
        }
    }
}
