using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Toypet;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PET (M@xx, xTz). Pet command dispatcher (adopt/surrender/spawn/dismiss/food+doping+loot/rename/mood/extend). PetAction ported PascalCase (PetActionResolver.GetActionById). Pet services/SM_PET red-tolerated.</summary>
public class CM_PET : AionClientPacket
{
    private PetAction action;
    private int templateId;
    private int objectId;
    private string petName;
    private int decorationId;
    private int eggObjId;
    private int count;
    private int subType;
    private int emotionId;
    private int actionType;
    private int dopingItemId;
    private int dopingAction;
    private int dopingSlot1;
    private int dopingSlot2;
    private int activateSpecialFunction;

    private int unk2, unk3, unk5, unk6;

    public CM_PET(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        action = PetActionResolver.GetActionById(ReadUH());
        switch (action)
        {
            case PetAction.Adopt:
                eggObjId = ReadD();
                templateId = ReadD();
                unk2 = ReadUC();
                unk3 = ReadD();
                decorationId = ReadD();
                unk5 = ReadD();
                unk6 = ReadD();
                petName = ReadS();
                break;
            case PetAction.Surrender:
            case PetAction.Spawn:
            case PetAction.Dismiss:
                templateId = ReadD();
                break;
            case PetAction.Food:
                actionType = ReadD();
                if (actionType == 3 || actionType == 4)
                { // auto loot (3), or auto sell items (4)
                    activateSpecialFunction = ReadD();
                    ReadD(); // always 0
                    ReadD(); // always 0
                }
                else if (actionType == 2)
                {
                    dopingAction = ReadD();
                    if (dopingAction == 0)
                    { // add item
                        dopingItemId = ReadD();
                        dopingSlot1 = ReadD();
                    }
                    else if (dopingAction == 1)
                    { // remove item
                        dopingSlot1 = ReadD();
                        dopingItemId = ReadD();
                    }
                    else if (dopingAction == 2)
                    { // switch items in two occupied slots
                        dopingSlot1 = ReadD();
                        dopingSlot2 = ReadD();
                    }
                    else if (dopingAction == 3)
                    { // use doping
                        dopingItemId = ReadD();
                        dopingSlot1 = ReadD();
                    }
                    // TODO: PetBuffs go here.
                    // Commented out now, no crash if handled in else clause
                    // else if (actionType == 5) {
                    // readD(); // cherry count or buff enabled? Read value = 1
                    // }
                }
                else
                {
                    objectId = ReadD();
                    count = ReadD();
                    unk2 = ReadD();
                }
                break;
            case PetAction.Rename:
                objectId = ReadD();
                petName = ReadS();
                break;
            case PetAction.Mood:
                subType = ReadD();
                emotionId = ReadD();
                break;
            case PetAction.ExtendExpiration: // extend expiration date
                eggObjId = ReadD(); // itemObjId
                objectId = ReadD(); // petObjId
                break;
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;

        Pet pet = player.GetPet();
        switch (action)
        {
            case PetAction.Adopt:
                if (!NameRestrictionService.IsValidPetName(petName) || NameRestrictionService.IsForbidden(petName))
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_PET_NOT_AVALIABE_NAME());
                else
                    PetAdoptionService.AdoptPet(player, eggObjId, templateId, petName, decorationId);
                break;
            case PetAction.ExtendExpiration:
                // for now we will do nothing, cause expiration-time is shitty
                break;
            case PetAction.Surrender:
                PetAdoptionService.SurrenderPet(player, templateId);
                break;
            case PetAction.Spawn:
                PetSpawnService.SummonPet(player, templateId);
                break;
            case PetAction.Dismiss:
                if (pet != null)
                    pet.GetController().Delete();
                break;
            case PetAction.Food:
                if (pet == null)
                    return;
                if (actionType == 2)
                { // Pet doping
                    PetService.GetInstance().UseDoping(pet, dopingAction, dopingItemId, dopingSlot1, dopingSlot2);
                }
                else if (actionType == 3)
                { // Pet looting
                    PetService.GetInstance().ActivateLoot(pet, activateSpecialFunction != 0);
                }
                else if (actionType == 4)
                { // Pet auto sell items
                    PetService.GetInstance().ActivateAutoSell(pet, activateSpecialFunction != 0);
                }
                else if (objectId == 0)
                {
                    pet.GetCommonData().SetCancelFeed(true);
                    PacketSendUtility.SendPacket(player, new SM_PET(4, 0, 0, player.GetPet()));
                    PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.EndFeeding, 0, player.GetObjectId()));
                }
                else if (pet.GetCommonData().GetRefeedDelay() > 0)
                { // not hungry yet
                    PacketSendUtility.SendPacket(player, new SM_PET(8, objectId, count, player.GetPet()));
                }
                else
                    PetService.GetInstance().RemoveObject(objectId, count, player);
                break;
            case PetAction.Rename:
                if (!NameRestrictionService.IsValidPetName(petName) || NameRestrictionService.IsForbidden(petName))
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_PET_NOT_AVALIABE_NAME());
                else
                    PetService.GetInstance().RenamePet(player, petName);
                break;
            case PetAction.Mood:
                if (pet != null && (subType == 0 && pet.GetCommonData().GetMoodRemainingTime() == 0
                    || (subType == 3 && pet.GetCommonData().GetGiftRemainingTime() == 0) || emotionId != 0))
                {
                    PetMoodService.CheckMood(pet, subType, emotionId);
                }
                break;
        }
    }
}
