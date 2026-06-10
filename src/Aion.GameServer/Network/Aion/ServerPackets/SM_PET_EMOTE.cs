using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PET_EMOTE (ATracer, Neon). Pet emote/movement packet. Existing C# PetEmote enum uses PascalCase members with value==emoteId, so getEmoteId()->(int)emote; Java MOVE_STOP/MOVETO -> PetEmote.MoveStop/MoveTo. Pet red-tolerated.</summary>
public class SM_PET_EMOTE : AionServerPacket
{
    private Pet pet;
    private PetEmote emote;
    private int emotionId, param1;

    public SM_PET_EMOTE(Pet pet, PetEmote emote)
        : this(pet, emote, 0, 0)
    {
    }

    public SM_PET_EMOTE(Pet pet, PetEmote emote, int emotionId, int param1)
    {
        this.pet = pet;
        this.emote = emote;
        this.emotionId = emotionId;
        this.param1 = param1;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(pet.GetObjectId());
        WriteC((int)emote);
        switch (emote)
        {
            case PetEmote.MoveStop:
                WriteF(pet.GetX());
                WriteF(pet.GetY());
                WriteF(pet.GetZ());
                WriteC(pet.GetHeading());
                break;
            case PetEmote.MoveTo:
                WriteF(pet.GetX());
                WriteF(pet.GetY());
                WriteF(pet.GetZ());
                WriteC(pet.GetHeading());
                WriteF(pet.GetMoveController().GetTargetX2());
                WriteF(pet.GetMoveController().GetTargetY2());
                WriteF(pet.GetMoveController().GetTargetZ2());
                break;
            default:
                WriteC(emotionId);
                WriteC(param1); // happinessAdded?
                break;
        }
    }
}
