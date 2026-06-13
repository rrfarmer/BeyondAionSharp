using System;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/PetController (ATracer).</summary>
public class PetController : VisibleObjectController<Pet>
{
    public override void OnDelete()
    {
        base.OnDelete();
        Aion.GameServer.Model.GameObjects.Players.PetCommonData commonData = GetOwner().GetCommonData();
        Aion.GameServer.Services.ToyPet.PetFeedProgress progress = commonData.GetFeedProgress();
        commonData.CancelRefeedTask();
        if (progress != null)
        {
            commonData.SetCancelFeed(true);
            Aion.GameServer.Dao.PlayerPetsDAO.SaveFeedStatus(GetOwner().GetObjectId(), progress.GetHungryLevel().GetValue(), progress.GetDataForPacket(), commonData.GetRefeedTime());
        }
        if (commonData.GetDopingBag() != null && commonData.GetDopingBag().IsDirty)
            Aion.GameServer.Dao.PlayerPetsDAO.SaveDopingBag(GetOwner().GetObjectId(), commonData.GetDopingBag());

        GetOwner().GetMaster().GetController().CancelTask(TaskId.PET_UPDATE);
        commonData.SetDespawnTime(DateTime.UtcNow);
        Aion.GameServer.Dao.PlayerPetsDAO.SavePetMoodData(commonData);
        GetOwner().GetMaster().SetPet(null);
    }

    public class PetUpdateTask : Runnable
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private long startTime = 0;

        public PetUpdateTask(Aion.GameServer.Model.GameObjects.Players.Player player)
        {
            this.player = player;
        }

        public void Run()
        {
            if (startTime == 0)
                startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            try
            {
                if (!player.IsSpawned())
                    return;

                Pet pet = player.GetPet();
                if (pet == null)
                    throw new InvalidOperationException("Pet is null");

                int currentPoints = 0;
                bool saved = false;

                if (pet.GetCommonData().GetMoodPoints(false) < 9000)
                {
                    if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime >= 60 * 1000)
                    {
                        currentPoints = pet.GetCommonData().GetMoodPoints(false);
                        if (currentPoints == 9000)
                        {
                            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_PET(pet, 4, 0));
                        }

                        Aion.GameServer.Dao.PlayerPetsDAO.SavePetMoodData(pet.GetCommonData());
                        saved = true;
                        startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }
                }

                if (currentPoints < 9000)
                {
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_PET(pet, 4, 0));
                }
                else
                {
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_PET(pet, 3, 0));
                    // Save if it reaches 100% after player snuggles the pet, not by the scheduler itself
                    if (!saved)
                        Aion.GameServer.Dao.PlayerPetsDAO.SavePetMoodData(pet.GetCommonData());
                }
            }
            catch (Exception)
            {
                player.GetController().CancelTask(TaskId.PET_UPDATE);
            }
        }
    }
}
