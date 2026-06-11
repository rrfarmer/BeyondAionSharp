using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.DataHolders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.TaskManager.Tasks;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;

namespace Aion.GameServer.Services.Toypet;

/// <summary>Java parity: services/toypet/PetAdoptionService (ATracer). Adopt/add/surrender pets (with validation). DataManager/ExpireTimerTask/IDFactory/SM_PET red-tolerated.</summary>
public class PetAdoptionService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PetAdoptionService));

    /// <summary>Create a pet for player (with validation).</summary>
    public static void AdoptPet(Player player, int eggObjId, int petId, string name, int decorationId)
    {
        int eggId = player.GetInventory().GetItemByObjId(eggObjId).GetItemId();
        ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(eggId);

        if (!ValidateAdoption(player, template, petId))
            return;

        if (!player.GetInventory().DecreaseByObjectId(eggObjId, 1))
            return;

        int expireTime = template.GetActions().GetAdoptPetAction().GetExpireMinutes() != 0
            ? (int)((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) + template.GetActions().GetAdoptPetAction().GetExpireMinutes() * 60) : 0;

        AddPet(player, petId, name, decorationId, expireTime);
    }

    /// <summary>Add pet to player.</summary>
    public static void AddPet(Player player, int petId, string name, int decorationId, int expireTime)
    {
        name = Util.ConvertName(name);
        PetCommonData petCommonData = player.GetPetList().AddPet(player, petId, decorationId, name, expireTime);
        if (petCommonData != null)
        {
            PacketSendUtility.SendPacket(player, new SM_PET(petCommonData, true));
            ExpireTimerTask.GetInstance().RegisterExpirable(petCommonData, player);
        }
    }

    private static bool ValidateAdoption(Player player, ItemTemplate template, int petId)
    {
        if (template == null || template.GetActions() == null || template.GetActions().GetAdoptPetAction() == null
            || template.GetActions().GetAdoptPetAction().GetPetId() != petId)
        {
            return false;
        }
        if (player.GetPetList().HasPet(petId))
        {
            log.LogWarning("Duplicate pet adoption " + player + " (pet: " + petId + ")");
            return false;
        }
        if (DataManager.PET_DATA.GetPetTemplate(petId) == null)
        {
            log.LogWarning("Trying adopt pet without template. PetId:" + petId);
            return false;
        }
        return true;
    }

    /// <summary>Delete pet.</summary>
    public static void SurrenderPet(Player player, int petId)
    {
        PetCommonData petCommonData = player.GetPetList().DeletePet(petId);
        if (petCommonData == null)
            return;
        if (player.GetPet() != null && player.GetPet().GetObjectId() == petCommonData.GetObjectId())
            player.GetPet().GetController().Delete();
        PacketSendUtility.SendPacket(player, new SM_PET(petCommonData, false));
        IDFactory.GetInstance().ReleaseId(petCommonData.GetObjectId());
    }
}
