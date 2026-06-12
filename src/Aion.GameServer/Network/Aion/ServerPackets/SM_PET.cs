using System;
using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PET (M@xx, xTz, Rolandas). Pet load/adopt/surrender/spawn/dismiss/food/rename/mood/special-function. PetAction & PetSpecialFunction are PascalCase C# enums with value==id (getActionId/getId -> (int)); PetFunctionType keeps SCREAMING names + GetId() extension. currentTimeMillis()->DateTimeOffset. Pet/PetCommonData/PetTemplate red-tolerated.</summary>
public class SM_PET : AionServerPacket
{
    private PetAction action;
    private Pet pet;
    private int petObjectId;
    private PetCommonData commonData;
    private string petName;
    private int itemObjectId;
    private ICollection<PetCommonData> pets;
    private int count;
    private int subType;
    private int shuggleEmotion;
    private bool isActing;
    private int lootNpcObjId;
    private int dopeAction;
    private int dopeSlot;
    private byte animationId;

    public SM_PET(int subType, int itemObjectId, int count, Pet pet)
    {
        this.action = PetAction.Food;
        this.subType = subType;
        this.count = count;
        this.itemObjectId = itemObjectId;
        this.commonData = pet.GetCommonData();
    }

    public SM_PET(PetAction action)
    {
        this.action = action;
    }

    public SM_PET(int petObjectId, string petName)
    {
        this.action = PetAction.Rename;
        this.petObjectId = petObjectId;
        this.petName = petName;
    }

    public SM_PET(Pet pet)
    {
        this.action = PetAction.Spawn;
        this.pet = pet;
    }

    public SM_PET(PetCommonData commonData, bool isAdopt)
    {
        this.action = isAdopt ? PetAction.Adopt : PetAction.Surrender;
        this.commonData = commonData;
    }

    public SM_PET(int petId, int petObjectId)
    {
        this.action = PetAction.Surrender;
        this.petObjectId = petObjectId;
    }

    /// <summary>For listing all pets on this character</summary>
    public SM_PET(ICollection<PetCommonData> pets)
    {
        this.action = PetAction.LoadPets;
        this.pets = pets;
    }

    public SM_PET(PetSpecialFunction specialFunction, bool active)
        : this(specialFunction, active, 0)
    {
    }

    public SM_PET(PetSpecialFunction specialFunction, bool active, int npcObjId)
    {
        this.action = PetAction.SpecialFunction;
        this.isActing = active;
        this.subType = (int)specialFunction;
        this.lootNpcObjId = npcObjId;
    }

    public SM_PET(int dopeAction, int itemId, int slot)
    {
        this.action = PetAction.SpecialFunction;
        this.dopeAction = dopeAction;
        this.subType = (int)PetSpecialFunction.DOPING;
        itemObjectId = itemId; // it's template ID, not objectId though. also it's misused as slot2 for slot switch action (dopeAction 2)
        dopeSlot = slot;
    }

    /// <summary>For mood only</summary>
    public SM_PET(Pet pet, int subType, int shuggleEmotion)
    {
        this.action = PetAction.Mood;
        this.shuggleEmotion = shuggleEmotion;
        this.subType = subType;
        this.commonData = pet.GetCommonData();
    }

    /// <summary>For deleting pet visually.</summary>
    public SM_PET(int petObjectId, ObjectDeleteAnimation animation)
    {
        this.action = PetAction.Dismiss;
        this.petObjectId = petObjectId;
        this.animationId = animation.GetId();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH((int)action);
        switch (action)
        {
            case PetAction.LoadPets: // load list on login
                WriteC(0); // unk
                WriteH(pets.Count);
                foreach (PetCommonData commonData in pets)
                {
                    WritePetData(commonData);
                }
                break;
            case PetAction.Adopt:
                WritePetData(commonData);
                break;
            case PetAction.Surrender:
                WriteD(commonData.GetTemplateId());
                WriteD(commonData.GetObjectId());
                WriteD(0); // unk
                WriteD(0); // unk
                break;
            case PetAction.Spawn:
                WriteS(pet.GetName());
                WriteD(pet.GetObjectTemplate().GetTemplateId());
                WriteD(pet.GetObjectId());

                WriteF(pet.GetPosition().GetX());
                WriteF(pet.GetPosition().GetY());
                WriteF(pet.GetPosition().GetZ());
                WriteF(pet.GetMoveController().GetTargetX2());
                WriteF(pet.GetMoveController().GetTargetY2());
                WriteF(pet.GetMoveController().GetTargetZ2());
                WriteC(pet.GetHeading());

                WriteD(pet.GetMaster().GetObjectId());

                WriteAppearance(pet.GetCommonData());
                break;
            case PetAction.Dismiss:
                WriteD(petObjectId);
                WriteC(animationId);
                break;
            case PetAction.Food:
                WriteH(1);
                WriteC(1);
                WriteC(subType);
                switch (subType)
                {
                    case 1: // eat
                        WriteD(commonData.GetFeedProgress().GetDataForPacket());
                        WriteD(0);
                        WriteD(itemObjectId);
                        WriteD(count);
                        break;
                    case 2: // eating successful
                        WriteD(commonData.GetFeedProgress().GetDataForPacket());
                        WriteD(0);
                        WriteD(itemObjectId);
                        WriteD(count);
                        WriteC(0);
                        break;
                    case 3: // not hungry
                    case 4: // cancel feed
                    case 5: // clean feed task
                        WriteD(commonData.GetFeedProgress().GetDataForPacket());
                        WriteD((int)commonData.GetRefeedDelay() / 1000);
                        break;
                    case 6: // give item
                        WriteD(commonData.GetFeedProgress().GetDataForPacket());
                        WriteD(0);
                        WriteD(itemObjectId);
                        WriteC(0);
                        break;
                    case 7: // present notification
                        WriteD(commonData.GetFeedProgress().GetDataForPacket());
                        WriteD((int)commonData.GetRefeedDelay() / 1000); // time
                        WriteD(itemObjectId);
                        WriteD(0);
                        break;
                    case 8: // is full
                        WriteD(commonData.GetFeedProgress().GetDataForPacket());
                        WriteD((int)commonData.GetRefeedDelay() / 1000);
                        WriteD(itemObjectId);
                        WriteD(count);
                        break;
                }
                break;
            case PetAction.Rename:
                WriteD(petObjectId);
                WriteS(petName);
                break;
            case PetAction.Mood:
                switch (subType)
                {
                    case 0: // check pet status
                        WriteC(subType);
                        // desynced feedback data, need to send delta in percents
                        if (commonData.GetLastSentPoints() < commonData.GetMoodPoints(true))
                            WriteD(commonData.GetMoodPoints(true) - commonData.GetLastSentPoints());
                        else
                        {
                            WriteD(0);
                            commonData.SetLastSentPoints(commonData.GetMoodPoints(true));
                        }
                        break;
                    case 2: // emotion sent
                        WriteC(subType);
                        WriteD(0);
                        WriteD(commonData.GetMoodPoints(true));
                        WriteD(shuggleEmotion);
                        commonData.SetLastSentPoints(commonData.GetMoodPoints(true));
                        commonData.SetMoodCdStarted(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        break;
                    case 3: // give gift
                        WriteC(subType);
                        WriteD(DataManager.PET_DATA.GetPetTemplate(commonData.GetTemplateId()).GetConditionReward());
                        commonData.SetGiftCdStarted(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        break;
                    case 4: // periodic update
                        WriteC(subType);
                        WriteD(commonData.GetMoodPoints(true));
                        WriteD(commonData.GetMoodRemainingTime());
                        WriteD(commonData.GetGiftRemainingTime());
                        commonData.SetLastSentPoints(commonData.GetMoodPoints(true));
                        break;
                }
                break;
            case PetAction.SpecialFunction:
                WriteC(subType);
                if (subType == 2)
                {
                    WriteC(dopeAction);
                    switch (dopeAction)
                    {
                        case 0: // add item
                            WriteD(itemObjectId);
                            WriteD(dopeSlot);
                            break;
                        case 1: // remove item
                            WriteD(dopeSlot);
                            break;
                        case 2: // move item from one slot to other
                            WriteD(dopeSlot); // slot 1
                            WriteD(itemObjectId); // slot 2
                            break;
                        case 3: // use item
                            WriteD(itemObjectId);
                            break;
                    }
                }
                else if (subType == 3)
                {
                    // looting NPC
                    if (lootNpcObjId > 0)
                    {
                        WriteC(isActing ? 1 : 2); // 0x02 display looted msg.
                        WriteD(lootNpcObjId);
                    }
                    else
                    {
                        // loot function activation
                        WriteC(0);
                        WriteC(isActing ? 1 : 0);
                    }
                }
                else if (subType == 4)
                {
                    WriteC(0);
                    WriteC(isActing ? 1 : 0);
                }
                break;
        }
    }

    private void WritePetData(PetCommonData petCommonData)
    {
        PetTemplate petTemplate = DataManager.PET_DATA.GetPetTemplate(petCommonData.GetTemplateId());
        WriteS(petCommonData.GetName());
        WriteD(petCommonData.GetTemplateId());
        WriteD(petCommonData.GetObjectId());
        WriteD(petCommonData.GetMasterObjectId());
        WriteD(0);
        WriteD(0);
        WriteD(petCommonData.GetBirthday());
        WriteD(petCommonData.SecondsUntilExpiration()); // accompanying time

        int specialtyCount = 0;
        if (petTemplate.ContainsFunction(PetFunctionType.WAREHOUSE))
        {
            WriteC(PetFunctionType.WAREHOUSE.GetId());
            WriteC(0); // length of following bytes
            specialtyCount++;
        }
        if (petTemplate.ContainsFunction(PetFunctionType.LOOT))
        {
            WriteC(PetFunctionType.LOOT.GetId());
            WriteC(1); // length of following bytes
            WriteC(0);
            specialtyCount++;
        }
        if (petTemplate.ContainsFunction(PetFunctionType.DOPING))
        {
            WriteC(PetFunctionType.DOPING.GetId());
            WriteC(PetDopingBag.MAX_ITEMS * 4); // length of following bytes (always write MAX_ITEMS, otherwise some pets show items of other pets)
            int[] items = petCommonData.GetDopingBag().GetItems();
            for (int i = 0; i < PetDopingBag.MAX_ITEMS; i++)
                WriteD(i < items.Length ? items[i] : 0);
            specialtyCount++;
        }
        if (petTemplate.ContainsFunction(PetFunctionType.FOOD))
        {
            WriteC(PetFunctionType.FOOD.GetId());
            WriteC(8); // length of following bytes
            WriteD(petCommonData.GetFeedProgress().GetDataForPacket());
            WriteD((int)(petCommonData.GetRefeedDelay() / 1000));
            specialtyCount++;
        }

        // Pets have only 2 functions max. If absent filled with NONE
        if (specialtyCount == 0)
        {
            WriteH(PetFunctionType.NONE.GetId());
            WriteH(PetFunctionType.NONE.GetId());
        }
        else if (specialtyCount == 1)
        {
            WriteH(PetFunctionType.NONE.GetId());
        }

        WriteAppearance(petCommonData);
    }

    private void WriteAppearance(PetCommonData petCommonData)
    {
        WriteH(PetFunctionType.APPEARANCE.GetId());
        WriteC(0); // not implemented color R ?
        WriteC(0); // not implemented color G ?
        WriteC(0); // not implemented color B ?
        WriteD(petCommonData.GetDecoration());
        WriteD(0); // wings ID if customize_attach = 1
        WriteD(0); // unk
    }
}
