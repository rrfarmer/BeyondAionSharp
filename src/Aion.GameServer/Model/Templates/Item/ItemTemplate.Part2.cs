using System.Collections.Generic;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Java parity: model/templates/item/ItemTemplate — partial #2 (Java ~389-569): enchant/expire, weapon-stats,
/// extra-inventory, mask mutate, area/world restrictions, tune/bonus, improvement/uselimits, activation, robot, group/skills.
/// </summary>
public partial class ItemTemplate
{
    public int GetEnchantType()
    {
        return enchantType;
    }

    public int GetExpireTime()
    {
        return expireTime;
    }

    public WeaponStats GetWeaponStats()
    {
        return weaponStats;
    }

    public int GetActivationCount()
    {
        return activationCount;
    }

    /// <summary>-1 if no id, can be values 0, 1, 2.</summary>
    public int GetExtraInventoryId()
    {
        if (extraInventory == null)
        {
            return -1;
        }
        return extraInventory.GetId();
    }

    public void ModifyMask(bool apply, int filter)
    {
        if (apply)
            mask |= filter;
        else
            mask &= ~filter;
    }

    public bool IsStackable()
    {
        return this.maxStackCount > 1;
    }

    public bool HasAreaRestriction()
    {
        return useLimits.GetUseArea() != null;
    }

    public Aion.GameServer.World.Zone.ZoneName? GetUseArea()
    {
        return useLimits.GetUseArea();
    }

    /// <summary>the tradeinList</summary>
    public TradeinList GetTradeinList()
    {
        return tradeinList;
    }

    /// <summary>the acquisition</summary>
    public Acquisition GetAcquisition()
    {
        return acquisition;
    }

    /// <summary>the rndBonusId, 0 if no bonus exists</summary>
    public int GetStatBonusSetId()
    {
        return rndBonusId;
    }

    /// <summary>the maxTuneCount, 0 if can not be randomized</summary>
    public int GetMaxTuneCount()
    {
        return maxTuneCount;
    }

    public bool CanTune()
    {
        return maxTuneCount != 0;
    }

    /// <summary>the conditioning</summary>
    public Improvement GetImprovement()
    {
        return improvement;
    }

    /// <summary>the useLimits</summary>
    public ItemUseLimits GetUseLimits()
    {
        return useLimits;
    }

    public Disposition GetDisposition()
    {
        return disposition;
    }

    public bool HasWorldRestrictions()
    {
        return useLimits.GetOwnershipWorldIds().Count != 0;
    }

    public bool IsItemRestrictedToWorld(int worldId)
    {
        List<int> ownershipWorldIds = useLimits.GetOwnershipWorldIds();
        if (ownershipWorldIds.Count == 0)
            return false;
        return ownershipWorldIds.Contains(worldId);
    }

    public bool IsCloth()
    {
        // not sure about LT_HEAD and CL_HEAD, check in retail
        return IsArmor() && (itemGroup.GetArmorType() != Aion.GameServer.Model.Templates.Item.Enums.ArmorType.ACCESSORY && GetItemGroup() != Aion.GameServer.Model.Templates.Item.Enums.ItemGroup.BELT || itemGroup == Aion.GameServer.Model.Templates.Item.Enums.ItemGroup.HEAD);
    }

    public bool IsPotion()
    {
        return itemId >= 162000000 && itemId < 163000000;
    }

    public Idian GetIdianAction()
    {
        return idianAction;
    }

    public bool IsCombinationItem()
    {
        return GetItemGroup() == Aion.GameServer.Model.Templates.Item.Enums.ItemGroup.COMBINATION;
    }

    public bool IsEnchantmentStone()
    {
        return GetItemGroup() == Aion.GameServer.Model.Templates.Item.Enums.ItemGroup.ENCHANTMENT;
    }

    public ItemActivationTarget GetActivationTarget()
    {
        if (GetActivationRace() == null)
            return activationTarget;
        return null;
    }

    public Race? GetActivationRace()
    {
        return activationTarget == null ? (Race?)null : activationTarget.GetRace();
    }

    public bool IsCombatActivated()
    {
        return activateCombat;
    }

    public int GetRobotId()
    {
        if (robotId == null)
            return 0;
        return robotId.Value;
    }

    public int GetPackCount()
    {
        return packCount;
    }

    public int GetMaxTampering()
    {
        return maxTampering;
    }

    public Aion.GameServer.Model.Templates.Item.Enums.ItemGroup GetItemGroup()
    {
        return itemGroup;
    }

    public int[] GetRequiredSkills()
    {
        return itemGroup.GetRequiredSkills();
    }

    public string GetTemperingName()
    {
        return temperingName;
    }

    public string GetEnchantName()
    {
        return enchantName;
    }

    public bool CanExceedEnchant()
    {
        return canExceedEnchant;
    }

    public ExceedEnchantSkillSetType GetExceedEnchantSkill()
    {
        return exceedEnchantSkill;
    }
}
