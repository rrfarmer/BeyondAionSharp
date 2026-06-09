using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/PetTemplate (IlBuono).</summary>
[XmlRoot("pet")]
public class PetTemplate : VisibleObjectTemplate, IL10n
{
    [XmlAttribute("id")] private int id;
    [XmlAttribute("name")] private string name;
    [XmlAttribute("nameid")] private int nameId;
    [XmlAttribute("condition_reward")] private int conditionReward;
    [XmlElement("petfunction")] private List<PetFunction> petFunctions;
    [XmlElement("petstats")] private PetStatsTemplate petStats;

    [XmlIgnore] private bool? hasPlayerFuncs = null;

    public override int GetTemplateId()
    {
        return id;
    }

    public override string GetName()
    {
        return name;
    }

    public override int GetL10nId()
    {
        return nameId;
    }

    public List<PetFunction> GetPetFunctions()
    {
        if (hasPlayerFuncs == null)
        {
            hasPlayerFuncs = false;
            if (petFunctions == null)
            {
                List<PetFunction> result = new List<PetFunction>();
                result.Add(PetFunction.CreateEmpty());
                petFunctions = result;
            }
            else
            {
                foreach (PetFunction func in petFunctions)
                {
                    if (func.GetPetFunctionType().IsPlayerFunction())
                    {
                        hasPlayerFuncs = true;
                        break;
                    }
                }
                if (hasPlayerFuncs == false)
                    petFunctions.Add(PetFunction.CreateEmpty());
            }
        }
        return petFunctions;
    }

    public PetFunction GetWarehouseFunction()
    {
        if (petFunctions == null)
            return null;
        foreach (PetFunction pf in petFunctions)
        {
            if (pf.GetPetFunctionType() == PetFunctionType.WAREHOUSE)
                return pf;
        }
        return null;
    }

    /// <summary>Used to write to SM_PET packet, so checks only needed ones.</summary>
    public bool ContainsFunction(PetFunctionType type)
    {
        if (type.GetId() < 0)
            return false;

        foreach (PetFunction t in GetPetFunctions())
        {
            if (t.GetPetFunctionType() == type)
                return true;
        }
        return false;
    }

    /// <summary>Returns function if found, otherwise null.</summary>
    public PetFunction GetPetFunction(PetFunctionType type)
    {
        foreach (PetFunction t in GetPetFunctions())
        {
            if (t.GetPetFunctionType() == type)
                return t;
        }
        return null;
    }

    public PetStatsTemplate GetPetStats()
    {
        return petStats;
    }

    public int GetConditionReward()
    {
        return conditionReward;
    }
}
