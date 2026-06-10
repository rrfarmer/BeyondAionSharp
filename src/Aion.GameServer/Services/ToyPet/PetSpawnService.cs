using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Services.Toypet;

/// <summary>Java parity: services/toypet/PetSpawnService (ATracer). Summons a pet, scheduling its update task and handling mood/refeed/loot state. PetController/ThreadPoolManager/VisibleObjectSpawner red-tolerated.</summary>
public class PetSpawnService
{
    public static void SummonPet(Player player, int templateId)
    {
        PetCommonData lastPetCommonData;

        if (player.GetPet() != null)
        {
            if (player.GetPet().GetObjectTemplate().GetTemplateId() == templateId)
                return;
            lastPetCommonData = player.GetPet().GetCommonData();
            player.GetPet().GetController().Delete();
        }
        else
        {
            lastPetCommonData = player.GetPetList().GetLastUsedPet();
        }

        if (lastPetCommonData != null && lastPetCommonData.GetTemplateId() != templateId) // reset mood if other pet is spawned
            lastPetCommonData.ClearMoodStatistics();

        player.GetController().AddTask(TaskId.PET_UPDATE, ThreadPoolManager.GetInstance().ScheduleAtFixedRate(new PetController.PetUpdateTask(player),
            PeriodicSaveConfig.PLAYER_PETS * 1000, PeriodicSaveConfig.PLAYER_PETS * 1000));

        Pet pet = VisibleObjectSpawner.SpawnPet(player, templateId);
        if (pet == null)
        {
            AuditLogger.Log(player, "tried to spawn invalid pet with id " + templateId);
            return;
        }
        PetCommonData petCommonData = pet.GetCommonData();
        if (petCommonData.GetRefeedDelay() > 0)
        {
            petCommonData.ScheduleRefeed(petCommonData.GetRefeedDelay());
        }
        else if (petCommonData.GetFeedProgress() != null)
            petCommonData.GetFeedProgress().SetHungryLevel(PetHungryLevel.Hungry);
        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - petCommonData.GetDespawnTime().ToUnixTimeMilliseconds() > 10 * 60 * 1000) // reset mood if pet was despawned for > 10 minutes
            petCommonData.ClearMoodStatistics();
        player.GetPetList().SetLastUsedPetTemplateId(templateId);
        if (petCommonData.IsLooting())
            PacketSendUtility.SendPacket(player, new SM_PET(PetSpecialFunction.AutoLoot, true));
        if (petCommonData.IsSelling())
            PacketSendUtility.SendPacket(player, new SM_PET(PetSpecialFunction.AutoSell, true));
    }
}
