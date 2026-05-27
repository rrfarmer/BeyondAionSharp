using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmPet : GameClientPacket
{
	public CmPet(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ActionId { get; private set; }

	public PetAction Action { get; private set; } = PetAction.Unknown;

	public int TemplateId { get; private set; }

	public int ObjectId { get; private set; }

	public string PetName { get; private set; } = string.Empty;

	public int DecorationId { get; private set; }

	public int EggObjectId { get; private set; }

	public int Count { get; private set; }

	public int SubType { get; private set; }

	public int EmotionId { get; private set; }

	public int ActionType { get; private set; }

	public int DopingItemId { get; private set; }

	public int DopingAction { get; private set; }

	public int DopingSlot1 { get; private set; }

	public int DopingSlot2 { get; private set; }

	public int ActivateSpecialFunction { get; private set; }

	public int Unknown2 { get; private set; }

	public int Unknown3 { get; private set; }

	public int Unknown5 { get; private set; }

	public int Unknown6 { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_PET.readImpl.
		ActionId = buffer.ReadH();
		Action = PetActionResolver.GetActionById(ActionId);

		switch (Action)
		{
			case PetAction.Adopt:
				EggObjectId = buffer.ReadD();
				TemplateId = buffer.ReadD();
				Unknown2 = buffer.ReadC();
				Unknown3 = buffer.ReadD();
				DecorationId = buffer.ReadD();
				Unknown5 = buffer.ReadD();
				Unknown6 = buffer.ReadD();
				PetName = buffer.ReadS();
				break;
			case PetAction.Surrender:
			case PetAction.Spawn:
			case PetAction.Dismiss:
				TemplateId = buffer.ReadD();
				break;
			case PetAction.Food:
				ReadFoodPayload(buffer);
				break;
			case PetAction.Rename:
				ObjectId = buffer.ReadD();
				PetName = buffer.ReadS();
				break;
			case PetAction.Mood:
				SubType = buffer.ReadD();
				EmotionId = buffer.ReadD();
				break;
			// Java also reads two D fields for EXTEND_EXPIRATION, but runImpl is a no-op.
			// Deferred intentionally with the expiration system; see Phase 6 pet dead-branch audit.
		}
	}

	private void ReadFoodPayload(PacketBuffer buffer)
	{
		ActionType = buffer.ReadD();
		if (ActionType == 3 || ActionType == 4)
		{
			ActivateSpecialFunction = buffer.ReadD();
			buffer.ReadD();
			buffer.ReadD();
		}
		else if (ActionType == 2)
		{
			DopingAction = buffer.ReadD();
			switch (DopingAction)
			{
				case 0:
					DopingItemId = buffer.ReadD();
					DopingSlot1 = buffer.ReadD();
					break;
				case 1:
					DopingSlot1 = buffer.ReadD();
					DopingItemId = buffer.ReadD();
					break;
				case 2:
					DopingSlot1 = buffer.ReadD();
					DopingSlot2 = buffer.ReadD();
					break;
				case 3:
					DopingItemId = buffer.ReadD();
					DopingSlot1 = buffer.ReadD();
					break;
			}
		}
		else
		{
			ObjectId = buffer.ReadD();
			Count = buffer.ReadD();
			Unknown2 = buffer.ReadD();
		}
	}
}
