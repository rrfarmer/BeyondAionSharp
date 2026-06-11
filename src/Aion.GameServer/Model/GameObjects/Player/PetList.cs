using System;
using System.Collections.Generic;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/PetList.</summary>
public class PetList
{
    private int lastUsedPetTemplateId;
    // Java parity: LinkedHashMap — insertion-ordered.
    private readonly Dictionary<int, PetCommonData> pets = new Dictionary<int, PetCommonData>();

    internal PetList(Player player)
    {
        LoadPets(player);
    }

    public void LoadPets(Player player)
    {
        List<PetCommonData> playerPets = Aion.GameServer.Dao.PlayerPetsDAO.GetPlayerPets(player);
        PetCommonData lastUsedPet = null;
        foreach (PetCommonData pet in playerPets)
        {
            Aion.GameServer.Taskmanager.Tasks.ExpireTimerTask.GetInstance().RegisterExpirable(pet, player);
            pets[pet.GetTemplateId()] = pet; // the client only sends template ids for spawn/dismiss, so we cannot support multiple same pets
            if (lastUsedPet == null || pet.GetDespawnTime() > lastUsedPet.GetDespawnTime())
                lastUsedPet = pet;
        }

        if (lastUsedPet != null)
            lastUsedPetTemplateId = lastUsedPet.GetObjectId();
    }

    public ICollection<PetCommonData> GetPets()
    {
        return pets.Values;
    }

    public PetCommonData GetPet(int petId)
    {
        return pets.TryGetValue(petId, out PetCommonData pet) ? pet : null;
    }

    public PetCommonData GetLastUsedPet()
    {
        return GetPet(lastUsedPetTemplateId);
    }

    public void SetLastUsedPetTemplateId(int lastUsedPetTemplateId)
    {
        this.lastUsedPetTemplateId = lastUsedPetTemplateId;
    }

    public PetCommonData AddPet(Player player, int petId, int decorationId, string name, int expireTime)
    {
        return AddPet(player, petId, decorationId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), name, expireTime);
    }

    public PetCommonData AddPet(Player player, int petId, int decorationId, long birthday, string name, int expireTime)
    {
        PetCommonData petCommonData = new PetCommonData(Aion.GameServer.Utils.Idfactory.IDFactory.GetInstance().NextId(), petId, player.GetObjectId(), expireTime);
        petCommonData.SetDecoration(decorationId);
        petCommonData.SetName(name);
        petCommonData.SetBirthday(DateTimeOffset.FromUnixTimeMilliseconds(birthday).UtcDateTime);
        petCommonData.SetDespawnTime(DateTimeOffset.UtcNow.UtcDateTime);
        Aion.GameServer.Dao.PlayerPetsDAO.InsertPlayerPet(player, petCommonData);
        pets[petId] = petCommonData;
        return petCommonData;
    }

    public bool HasPet(int templateId)
    {
        return pets.ContainsKey(templateId);
    }

    public PetCommonData DeletePet(int templateId)
    {
        if (pets.Remove(templateId, out PetCommonData petCommonData))
        {
            Aion.GameServer.Dao.PlayerPetsDAO.RemovePlayerPet(petCommonData.GetObjectId());
            return petCommonData;
        }
        return null;
    }
}
