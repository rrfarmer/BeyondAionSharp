using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PET_EMOTE (ATracer). Pet movement/emotion packet. PetEmote ported PascalCase (PetEmoteResolver.GetEmoteById). SM_PET_EMOTE/World red-tolerated.</summary>
public class CM_PET_EMOTE : AionClientPacket
{
    private PetEmote emote;
    private float x1, y1, z1, x2, y2, z2;
    private byte h;
    private int emoteId, emotionId;
    private int unk2;

    public CM_PET_EMOTE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        emoteId = ReadUC();
        emote = PetEmoteResolver.GetEmoteById(emoteId);
        switch (emote)
        {
            case PetEmote.MoveStop:
            case PetEmote.MovePositionUpdate:
                x1 = ReadF();
                y1 = ReadF();
                z1 = ReadF();
                h = ReadC();
                break;
            case PetEmote.MoveTo:
                x1 = ReadF();
                y1 = ReadF();
                z1 = ReadF();
                h = ReadC();
                x2 = ReadF();
                y2 = ReadF();
                z2 = ReadF();
                break;
            default:
                emotionId = ReadUC();
                unk2 = ReadUC();
                break;
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        Pet pet = player.GetPet();

        if (pet == null || !pet.IsSpawned()) // client sometimes just doesn't care...
            return;
        if (emote == PetEmote.Unknown)
        {
            NullLoggerFactory.Instance.CreateLogger(GetType_().Name).LogWarning(player + " / " + pet + " sent pet emote " + emoteId + " (emotionId: " + emotionId + ", unk2: " + unk2 + ")");
            return;
        }

        // sometimes client is crazy enough to send -2.4457384E7 as z coordinate
        // TODO (check retail) either its client bug or packet problem somewhere
        // reproducible by flying randomly and falling from long height with fly resume
        if (x1 < 0 || y1 < 0 || z1 < 0)
        {
            NullLoggerFactory.Instance.CreateLogger(GetType_().Name).LogWarning(pet + " of " + player + " sent " + emote + " at x:" + x1 + ", y:" + y1 + ", z:" + z1 + ", h:" + h);
            return;
        }

        switch (emote)
        {
            case PetEmote.MoveStop:
            case PetEmote.MovePositionUpdate:
                if (emote == PetEmote.MovePositionUpdate)
                { // TODO remove once we're sure "MOVE_POSITION_UPDATE" is correct and h is actually h
                    NullLoggerFactory.Instance.CreateLogger(GetType_().Name).LogWarning(pet + " of " + player + " sent " + emote + " at x:" + x1 + ", y:" + y1 + ", z:" + z1 + ", h:" + h);
                }
                World.GetInstance().UpdatePosition(pet, x1, y1, z1, h);
                BroadcastToSightedPlayers(pet, new SM_PET_EMOTE(pet, emote), false);
                break;
            case PetEmote.MoveTo:
                World.GetInstance().UpdatePosition(pet, x1, y1, z1, h);
                pet.GetMoveController().SetNewDirection(x2, y2, z2, h);
                BroadcastToSightedPlayers(pet, new SM_PET_EMOTE(pet, emote), false);
                break;
            default:
                BroadcastToSightedPlayers(pet, new SM_PET_EMOTE(pet, emote, emotionId, unk2), emote == PetEmote.Emotion);
                break;
        }
    }

    private void BroadcastToSightedPlayers(Pet pet, AionServerPacket packet, bool withMaster)
    {
        PacketSendUtility.BroadcastPacket(pet, packet, false, other => (withMaster || !other.Equals(pet.GetMaster())) && other.GetKnownList().Sees(pet));
    }
}
