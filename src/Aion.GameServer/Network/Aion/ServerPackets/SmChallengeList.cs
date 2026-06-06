using Aion.Commons.Network;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmChallengeList : GameServerPacket
{
	public const int PacketOpCode = 280; // Java parity: ServerPacketsOpcodes addPacketOpcode(280, SM_CHALLENGE_LIST.class).
	public const int ActionTaskList = 2;
	public const int ActionTaskInfo = 7;
	public const int LegionOwnerTypeId = 1;

	private readonly int _action;
	private readonly int _ownerId;
	private readonly int _ownerTypeId;
	private readonly int _playerObjectId;
	private readonly int _currentEpochSeconds;
	private readonly IReadOnlyList<ChallengeTaskState> _tasks;
	private readonly ChallengeTaskState? _task;

	private SmChallengeList(
		int action,
		int ownerId,
		int ownerTypeId,
		int playerObjectId,
		int currentEpochSeconds,
		IReadOnlyList<ChallengeTaskState>? tasks = null,
		ChallengeTaskState? task = null)
		: base(PacketOpCode)
	{
		_action = action;
		_ownerId = ownerId;
		_ownerTypeId = ownerTypeId;
		_playerObjectId = playerObjectId;
		_currentEpochSeconds = currentEpochSeconds;
		_tasks = tasks ?? Array.Empty<ChallengeTaskState>();
		_task = task;
	}

	public static SmChallengeList TaskList(
		int ownerId,
		int ownerTypeId,
		int playerObjectId,
		int currentEpochSeconds,
		IReadOnlyList<ChallengeTaskState> tasks)
	{
		return new SmChallengeList(ActionTaskList, ownerId, ownerTypeId, playerObjectId, currentEpochSeconds, tasks: tasks);
	}

	public static SmChallengeList TaskInfo(int ownerId, int ownerTypeId, int playerObjectId, ChallengeTaskState task)
	{
		return new SmChallengeList(ActionTaskInfo, ownerId, ownerTypeId, playerObjectId, currentEpochSeconds: 0, task: task);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_CHALLENGE_LIST.writeImpl.
		buffer.WriteC(_action);
		buffer.WriteD(_ownerId);
		buffer.WriteC(_ownerTypeId);
		buffer.WriteD(_playerObjectId);
		switch (_action)
		{
			case ActionTaskList:
				buffer.WriteD(_currentEpochSeconds);
				buffer.WriteH(_tasks.Count);
				foreach (var task in _tasks)
				{
					buffer.WriteD(32);
					buffer.WriteD(task.TaskId);
					buffer.WriteC(1);
					buffer.WriteC(21);
					buffer.WriteC(0);
					buffer.WriteD(task.CompleteTimeEpochSeconds);
				}
				break;
			case ActionTaskInfo:
				if (_task == null)
					break;
				buffer.WriteD(32);
				buffer.WriteD(_task.TaskId);
				buffer.WriteH(_task.Quests.Count);
				foreach (var quest in _task.Quests)
				{
					buffer.WriteD(quest.QuestId);
					buffer.WriteH(quest.MaxRepeats);
					buffer.WriteD(quest.ScorePerQuest);
					buffer.WriteH(quest.CompleteCount);
				}
				break;
		}
	}
}
