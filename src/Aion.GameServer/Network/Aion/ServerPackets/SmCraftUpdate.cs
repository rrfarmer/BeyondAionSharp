using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmCraftUpdate : GameServerPacket
{
	public const int PacketOpCode = 181;
	public const int MorphSkillId = 40009;
	public const int MorphDelayMilliseconds = 1000;

	private readonly int _skillId;
	private readonly int _itemId;
	private readonly int _action;
	private readonly int _success;
	private readonly int _failure;
	private readonly string? _itemNameL10n;
	private readonly int _executionSpeed;
	private readonly int _delay;

	public SmCraftUpdate(
		int skillId,
		ItemTemplateSummary itemTemplate,
		int success,
		int failure,
		int action,
		int executionSpeed,
		int delay)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_CRAFT_UPDATE.
		_skillId = skillId;
		_itemId = itemTemplate.TemplateId;
		_action = action;
		_success = success;
		_failure = failure;
		_itemNameL10n = itemTemplate.GetClientName();
		_executionSpeed = executionSpeed;
		_delay = skillId == MorphSkillId ? MorphDelayMilliseconds : delay;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteH(_skillId);
		buffer.WriteC(_action);
		buffer.WriteD(_itemId);
		buffer.WriteD(_success);
		buffer.WriteD(_failure);
		buffer.WriteD(_executionSpeed);
		buffer.WriteD(_delay);

		switch (_action)
		{
			case 0:
			case 3:
				buffer.WriteD(1330048);
				buffer.WriteS(_itemNameL10n);
				break;
			case 1:
			case 2:
				buffer.WriteD(0);
				buffer.WriteS(null);
				break;
			case 4:
				buffer.WriteD(1330051);
				buffer.WriteS(null);
				break;
			case 5:
				buffer.WriteD(1330049);
				buffer.WriteS(_itemNameL10n);
				break;
			case 6:
			case 7:
				buffer.WriteD(1330050);
				buffer.WriteS(_itemNameL10n);
				break;
			default:
				buffer.WriteD(0);
				buffer.WriteS(null);
				break;
		}
	}
}
