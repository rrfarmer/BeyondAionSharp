using System;
using Aion.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Model.Templates.Recipe;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Craft;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Task;

/// <summary>
/// Java parity: skillengine/task/CraftingTask (Mr. Poke, synchro2, Yeats) : AbstractCraftTask. Crafting interaction progress + crit
/// combo logic. Math.round(float) -> (int)Math.Floor(x+0.5f); switch fallthrough (ESTATE/PALACE) -> shared case labels; ItemQuality/
/// HouseType SCREAMING (kept). Recipe/Item/House/CraftService/SM_CRAFT_* red-tolerated.
/// </summary>
public class CraftingTask : AbstractCraftTask
{
    private readonly RecipeTemplate recipeTemplate;
    private readonly int maxCritCount;
    private readonly int bonus;
    private ItemTemplate itemTemplate;
    private int critCount;
    private int showBarDelay;
    private int executionSpeed;

    public CraftingTask(Player requester, StaticObject responder, RecipeTemplate recipeTemplate, int skillLvlDiff, int bonus)
        : base(requester, responder, skillLvlDiff)
    {
        this.recipeTemplate = recipeTemplate;
        this.maxCritCount = recipeTemplate.GetComboProductSize();
        this.bonus = bonus;
        this.itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(recipeTemplate.GetProductId());
    }

    protected override void OnFailureFinish()
    {
        PacketSendUtility.SendPacket(requester,
            new SM_CRAFT_UPDATE(recipeTemplate.GetSkillId(), itemTemplate, currentSuccessValue, currentFailureValue, 6, 0, 0));
        PacketSendUtility.BroadcastPacket(requester, new SM_CRAFT_ANIMATION(requester.GetObjectId(), responder.GetObjectId(), 0, 3), true);
    }

    protected override bool OnSuccessFinish()
    {
        if (CalculateCrit())
        {
            OnInteractionStart();
            return false;
        }
        else
        {
            PacketSendUtility.SendPacket(requester,
                new SM_CRAFT_UPDATE(recipeTemplate.GetSkillId(), itemTemplate, currentSuccessValue, currentFailureValue, 5, 0, 0));
            PacketSendUtility.BroadcastPacket(requester, new SM_CRAFT_ANIMATION(requester.GetObjectId(), responder.GetObjectId(), 0, 2), true);
            CraftService.FinishCrafting(requester, recipeTemplate, critCount, bonus);
            return true;
        }
    }

    private bool CalculateCrit()
    {
        if (critCount >= maxCritCount)
            return false;

        if (recipeTemplate.GetComboProduct(critCount + 1) == null)
            return false;

        // first crit uses base rate, subsequent crits use combo rate
        float chance;
        if (critCount == 0)
            chance = Rates.Get(requester, RatesConfig.CRAFT_CRIT_CHANCES);
        else
            chance = Rates.Get(requester, RatesConfig.CRAFT_COMBO_CHANCES);
        House house = requester.GetActiveHouse();
        if (house != null)
            switch (house.GetHouseType())
            {
                case HouseType.ESTATE:
                case HouseType.PALACE:
                    chance += 5;
                    break;
            }

        if (Rnd.Chance() >= chance)
            return false;

        critCount++;
        itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(recipeTemplate.GetComboProduct(critCount));
        return true;
    }

    protected override void SendInteractionUpdate()
    {
        PacketSendUtility.SendPacket(requester, new SM_CRAFT_UPDATE(recipeTemplate.GetSkillId(), itemTemplate, currentSuccessValue, currentFailureValue,
            craftType.GetProgressId(), executionSpeed, showBarDelay));
    }

    protected override void OnInteractionAbort()
    {
        PacketSendUtility.SendPacket(requester, new SM_CRAFT_UPDATE(recipeTemplate.GetSkillId(), itemTemplate, 0, 0, 4, 0, 0));
        PacketSendUtility.BroadcastPacket(requester, new SM_CRAFT_ANIMATION(requester.GetObjectId(), responder.GetObjectId(), 0, 2), true);
        requester.SetCraftingTask(null);
    }

    protected override void OnInteractionFinish()
    {
        requester.SetCraftingTask(null);
    }

    protected override void OnInteractionStart()
    {
        currentSuccessValue = 0;
        currentFailureValue = 0;

        PacketSendUtility.SendPacket(requester,
            new SM_CRAFT_UPDATE(recipeTemplate.GetSkillId(), itemTemplate, fullBarValue, fullBarValue, critCount == 0 ? 0 : 3, 0, 0));
        PacketSendUtility.SendPacket(requester, new SM_CRAFT_UPDATE(recipeTemplate.GetSkillId(), itemTemplate, 0, 0, 1, 0, 0));
        PacketSendUtility.BroadcastPacket(requester,
            new SM_CRAFT_ANIMATION(requester.GetObjectId(), responder.GetObjectId(), recipeTemplate.GetSkillId(), 0), true);
        PacketSendUtility.BroadcastPacket(requester,
            new SM_CRAFT_ANIMATION(requester.GetObjectId(), responder.GetObjectId(), recipeTemplate.GetSkillId(), 1), true);
    }

    protected override void AnalyzeInteraction()
    {
        if (recipeTemplate.GetSkillId() == 40009)
        { // morph
            currentSuccessValue = fullBarValue;
            return;
        }
        else if (skillLvlDiff < 0)
        {
            currentFailureValue = fullBarValue;
            return;
        }

        craftType = CraftType.NORMAL;
        float multi = Rnd.NextFloat(1f, 2f);
        float failReduction = Math.Max(1 - skillLvlDiff * 0.015f, 0.25f); // dynamic fail rate multiplier
        bool success = skillLvlDiff >= 41 || Rnd.Chance() >= CraftConfig.MAX_CRAFT_FAILURE_CHANCE * failReduction;

        float bonusModifier = 1;
        switch (itemTemplate.GetItemQuality())
        {
            case ItemQuality.LEGEND:
                bonusModifier = 0.9f;
                break;
            case ItemQuality.UNIQUE:
                bonusModifier = 0.7f;
                break;
            case ItemQuality.EPIC:
                bonusModifier = 0.5f;
                break;
            case ItemQuality.MYTHIC:
                bonusModifier = 0.3f;
                break;
        }

        if (success)
        {
            if (Rnd.Chance() < (15 + skillLvlDiff / 3f))
                craftType = CraftType.CRIT_BLUE; // LIGHT BLUE + 10-20%

            int minStep = 70;
            int lvlBoni = skillLvlDiff > 10 ? ((skillLvlDiff - 10) * 2) : 0;
            int bonus = (int)(((craftType == CraftType.CRIT_BLUE ? 100 : 0) + (((skillLvlDiff + 1) / 2f) + lvlBoni) * 10) * multi);
            currentSuccessValue += (int)Math.Floor(minStep + (bonus * bonusModifier) + 0.5f);
        }
        else
        {
            int minStep = recipeTemplate.GetMaxProductionCount() != null ? 70 : 120;
            int bonus = (int)(((skillLvlDiff + 1) / 1.5f * 10) * multi);
            currentFailureValue += (int)Math.Floor(minStep + (bonus * bonusModifier) + 0.5f);
        }

        if (currentSuccessValue > fullBarValue)
            currentSuccessValue = fullBarValue;
        else if (currentFailureValue > fullBarValue)
            currentFailureValue = fullBarValue;

        int speed = bonusModifier < 1 ? (int)Math.Floor(900 * (2 - bonusModifier) + 0.5f) : (900 - (skillLvlDiff * 30));
        executionSpeed = Math.Max(speed, 300);
        showBarDelay = bonusModifier < 1 ? 1200 : Math.Max(500, 1200 - (skillLvlDiff * 30));
    }
}
