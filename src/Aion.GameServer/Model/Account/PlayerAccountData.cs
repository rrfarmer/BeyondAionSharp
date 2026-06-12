using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Account;

/// <summary>
/// Java parity: model/account/PlayerAccountData (Luno). Holds char-selection info: player commondata, appearance,
/// creation/deletion time. java.sql.Timestamp→DateTimeOffset (getTime()/1000→ToUnixTimeMilliseconds()/1000);
/// Collections.emptyList→new List; record VisibleItem; signed byte→sbyte; Integer color→int?.
/// CharacterBanInfo/PlayerCommonData/PlayerAppearance/BoundRadius/ItemSlot/Item red-tolerated.
/// </summary>
public class PlayerAccountData
{
    private readonly PlayerCommonData playerCommonData;
    private PlayerAppearance appearance;
    private CharacterBanInfo cbi;
    private List<VisibleItem> visibleItems;
    private DateTimeOffset? creationDate;
    private DateTimeOffset? deletionDate;

    public PlayerAccountData(PlayerCommonData playerCommonData, PlayerAppearance appearance)
        : this(playerCommonData, appearance, null, new List<VisibleItem>())
    {
    }

    public PlayerAccountData(PlayerCommonData playerCommonData, PlayerAppearance appearance, CharacterBanInfo cbi, List<VisibleItem> visibleItems)
    {
        this.playerCommonData = playerCommonData;
        this.appearance = appearance;
        this.cbi = cbi;
        this.visibleItems = visibleItems;
        UpdateBoundingRadius();
    }

    public CharacterBanInfo GetCharBanInfo()
    {
        return cbi;
    }

    public void SetCharBanInfo(CharacterBanInfo cbi)
    {
        this.cbi = cbi;
    }

    public DateTimeOffset? GetCreationDate()
    {
        return creationDate;
    }

    /// <summary>Sets deletion date.</summary>
    public void SetDeletionDate(DateTimeOffset? deletionDate)
    {
        this.deletionDate = deletionDate;
    }

    /// <summary>Get deletion date.</summary>
    /// <returns>Timestamp date when char should be deleted.</returns>
    public DateTimeOffset? GetDeletionDate()
    {
        return deletionDate;
    }

    /// <summary>Get time in seconds when this player will be deleted ( 0 if player was not set to be deleted )</summary>
    /// <returns>deletion time in seconds</returns>
    public int GetDeletionTimeInSeconds()
    {
        return deletionDate == null ? 0 : (int)(deletionDate.Value.ToUnixTimeMilliseconds() / 1000);
    }

    /// <returns>the playerCommonData</returns>
    public PlayerCommonData GetPlayerCommonData()
    {
        return playerCommonData;
    }

    public PlayerAppearance GetAppearance()
    {
        return appearance;
    }

    public void SetAppearance(PlayerAppearance appearance)
    {
        this.appearance = appearance;
        UpdateBoundingRadius();
    }

    public void UpdateBoundingRadius()
    {
        playerCommonData.SetBoundingRadius(new BoundRadius(0.25f, 0.25f, appearance.GetBoundHeight()));
    }

    public void SetCreationDate(DateTimeOffset? creationDate)
    {
        this.creationDate = creationDate;
    }

    public List<VisibleItem> GetVisibleItems()
    {
        return visibleItems;
    }

    public void SetVisibleItems(List<Item> equipment)
    {
        List<VisibleItem> items = new();
        foreach (Item item in equipment)
        {
            sbyte slotType = ItemSlotExtensions.GetEquipmentSlotType(item.GetEquipmentSlot());
            if (slotType != 0)
                items.Add(new VisibleItem(slotType, item.GetItemSkinTemplate().GetTemplateId(), item.GetGodStoneId(), item.GetItemColor()));
        }
        this.visibleItems = items;
    }

    public record VisibleItem(sbyte SlotType, int ItemId, int GodStoneId, int? Color);
}
