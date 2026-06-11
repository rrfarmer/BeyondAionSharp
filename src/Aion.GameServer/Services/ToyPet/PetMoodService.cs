using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Services.Toypet;

/// <summary>Java parity: services/toypet/PetMoodService (ATracer). Pet mood interactions (start/interact/request-present). ItemService/SM_PET red-tolerated.</summary>
public class PetMoodService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PetMoodService));

    public static void CheckMood(Pet pet, int type, int shuggleEmotion)
    {
        switch (type)
        {
            case 0:
                StartCheckingMood(pet);
                break;
            case 1:
                InteractWithPet(pet, shuggleEmotion);
                break;
            case 3:
                RequestPresent(pet);
                break;
        }
    }

    private static void RequestPresent(Pet pet)
    {
        if (pet.GetCommonData().GetMoodPoints(false) < 9000)
        {
            log.LogWarning("Requested present before mood fill up: {Master}", pet.GetMaster().GetName());
            return;
        }

        if (pet.GetCommonData().GetGiftRemainingTime() > 0)
        {
            AuditLogger.Log(pet.GetMaster(), "tried to get gift of pet " + pet.GetObjectId() + " during CD");
            return;
        }

        if (pet.GetMaster().GetInventory().IsFull())
        {
            PacketSendUtility.SendPacket(pet.GetMaster(), SM_SYSTEM_MESSAGE.STR_WAREHOUSE_FULL_INVENTORY());
            return;
        }

        pet.GetCommonData().ClearMoodStatistics();
        PacketSendUtility.SendPacket(pet.GetMaster(), new SM_PET(pet, 4, 0));
        PacketSendUtility.SendPacket(pet.GetMaster(), new SM_PET(pet, 3, 0));
        int itemId = pet.GetObjectTemplate().GetConditionReward();
        if (itemId != 0)
        {
            ItemService.AddItem(pet.GetMaster(), pet.GetObjectTemplate().GetConditionReward(), 1);
        }
    }

    private static void InteractWithPet(Pet pet, int shuggleEmotion)
    {
        if (pet.GetCommonData() != null)
        {
            if (pet.GetCommonData().IncreaseShuggleCounter())
            {
                PacketSendUtility.SendPacket(pet.GetMaster(), new SM_PET(pet, 2, shuggleEmotion));
                PacketSendUtility.SendPacket(pet.GetMaster(), new SM_PET(pet, 4, 0)); // Update progress immediately
            }
        }
    }

    private static void StartCheckingMood(Pet pet)
    {
        PacketSendUtility.SendPacket(pet.GetMaster(), new SM_PET(pet, 0, 0));
    }
}
