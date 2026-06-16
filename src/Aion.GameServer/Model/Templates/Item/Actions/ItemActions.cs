using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/ItemActions.</summary>
[XmlType("ItemActions")]
public class ItemActions
{
    [XmlElement("skilllearn", typeof(SkillLearnAction))]
    [XmlElement("extract", typeof(ExtractAction))]
    [XmlElement("skilluse", typeof(SkillUseAction))]
    [XmlElement("enchant", typeof(EnchantItemAction))]
    [XmlElement("queststart", typeof(QuestStartAction))]
    [XmlElement("dye", typeof(DyeAction))]
    [XmlElement("craftlearn", typeof(CraftLearnAction))]
    [XmlElement("toypetspawn", typeof(ToyPetSpawnAction))]
    [XmlElement("decompose", typeof(DecomposeAction))]
    [XmlElement("titleadd", typeof(TitleAddAction))]
    [XmlElement("learnemotion", typeof(EmotionLearnAction))]
    [XmlElement("read", typeof(ReadAction))]
    [XmlElement("fireworkact", typeof(FireworksUseAction))]
    [XmlElement("instancetimeclear", typeof(InstanceTimeClear))]
    [XmlElement("expandinventory", typeof(ExpandInventoryAction))]
    [XmlElement("animation", typeof(AnimationAddAction))]
    [XmlElement("cosmetic", typeof(CosmeticItemAction))]
    [XmlElement("charge", typeof(ChargeAction))]
    [XmlElement("ride", typeof(RideAction))]
    [XmlElement("houseobject", typeof(SummonHouseObjectAction))]
    [XmlElement("housedeco", typeof(DecorateAction))]
    [XmlElement("assemble", typeof(AssemblyItemAction))]
    [XmlElement("adoptpet", typeof(AdoptPetAction))]
    [XmlElement("apextract", typeof(ApExtractAction))]
    [XmlElement("remodel", typeof(RemodelAction))]
    [XmlElement("expextract", typeof(ExpExtractAction))]
    [XmlElement("polish", typeof(PolishAction))]
    [XmlElement("composition", typeof(CompositionAction))]
    [XmlElement("tuning", typeof(TuningAction))]
    [XmlElement("megaphone", typeof(MegaphoneAction))]
    [XmlElement("pack", typeof(PackAction))]
    [XmlElement("tampering", typeof(TamperingAction))]
    [XmlElement("multireturn", typeof(MultiReturnAction))]
    // Public so XmlSerializer can populate it (JAXB read the private field via @XmlAccessorType(FIELD)).
    public List<AbstractItemAction> itemActions;

    /// <summary>Gets the value of the itemActions property.</summary>
    public List<AbstractItemAction> GetItemActions()
    {
        return itemActions == null ? new List<AbstractItemAction>() : itemActions;
    }

    public EnchantItemAction GetEnchantAction()
    {
        if (itemActions == null)
            return null;
        foreach (AbstractItemAction action in itemActions)
        {
            if (action is EnchantItemAction a)
                return a;
        }
        return null;
    }

    public SummonHouseObjectAction GetHouseObjectAction()
    {
        if (itemActions == null)
            return null;
        foreach (AbstractItemAction action in itemActions)
        {
            if (action is SummonHouseObjectAction a)
                return a;
        }
        return null;
    }

    public CraftLearnAction GetCraftLearnAction()
    {
        if (itemActions == null)
            return null;
        foreach (AbstractItemAction action in itemActions)
        {
            if (action is CraftLearnAction a)
                return a;
        }
        return null;
    }

    public DecorateAction GetDecorateAction()
    {
        if (itemActions == null)
            return null;
        foreach (AbstractItemAction action in itemActions)
        {
            if (action is DecorateAction a)
                return a;
        }
        return null;
    }

    public DyeAction GetDyeAction()
    {
        if (itemActions == null)
            return null;
        foreach (AbstractItemAction action in itemActions)
        {
            if (action is DyeAction a)
                return a;
        }
        return null;
    }

    public AdoptPetAction GetAdoptPetAction()
    {
        if (itemActions == null)
            return null;
        foreach (AbstractItemAction action in itemActions)
        {
            if (action is AdoptPetAction a)
                return a;
        }
        return null;
    }

    public RemodelAction GetRemodelAction()
    {
        if (itemActions == null)
            return null;
        foreach (AbstractItemAction action in itemActions)
        {
            if (action is RemodelAction a)
                return a;
        }
        return null;
    }

    public PolishAction GetPolishAction()
    {
        if (itemActions == null)
            return null;
        foreach (AbstractItemAction action in itemActions)
        {
            if (action is PolishAction a)
                return a;
        }
        return null;
    }

    public TuningAction GetTuningAction()
    {
        if (itemActions == null)
            return null;
        foreach (AbstractItemAction action in itemActions)
        {
            if (action is TuningAction a)
                return a;
        }
        return null;
    }

    public SkillUseAction GetSkillUseAction()
    {
        if (itemActions == null)
            return null;
        foreach (AbstractItemAction action in itemActions)
        {
            if (action is SkillUseAction a)
                return a;
        }
        return null;
    }

    public RideAction GetRideAction()
    {
        if (itemActions == null)
            return null;
        foreach (AbstractItemAction action in itemActions)
        {
            if (action is RideAction a)
                return a;
        }
        return null;
    }
}
