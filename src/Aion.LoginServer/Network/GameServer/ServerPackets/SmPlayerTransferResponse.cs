using Aion.Commons.Network;
using Aion.LoginServer.Model;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmPlayerTransferResponse : GsServerPacket
{
	private readonly PlayerTransferResultStatus _result;
	private readonly int _taskId;
	private readonly string _reason;
	private readonly PlayerTransferRequest? _request;
	private readonly PlayerTransferTask? _task;

	public SmPlayerTransferResponse(PlayerTransferResultStatus result, int taskId)
	{
		_result = result;
		_taskId = taskId;
		_reason = string.Empty;
	}

	public SmPlayerTransferResponse(PlayerTransferResultStatus result, int taskId, string reason)
	{
		_result = result;
		_taskId = taskId;
		_reason = reason;
	}

	public SmPlayerTransferResponse(PlayerTransferResultStatus result, PlayerTransferRequest request)
	{
		_result = result;
		_request = request;
		_taskId = request.TaskId;
		_reason = string.Empty;
	}

	public SmPlayerTransferResponse(PlayerTransferResultStatus result, PlayerTransferTask task)
	{
		_result = result;
		_task = task;
		_reason = string.Empty;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(12);
		buffer.WriteD((int)_result);
		switch (_result)
		{
			case PlayerTransferResultStatus.SendInfo:
				if (_request == null)
					throw new InvalidOperationException("SEND_INFO requires a transfer request.");
				buffer.WriteD(_request.TargetAccountId);
				buffer.WriteD(_taskId);
				buffer.WriteS(_request.Name);
				buffer.WriteS(_request.TargetAccount?.Name ?? string.Empty);
				buffer.WriteD(_request.Db.Length);
				buffer.WriteB(_request.Db);
				break;
			case PlayerTransferResultStatus.Ok:
				buffer.WriteD(_taskId);
				break;
			case PlayerTransferResultStatus.Error:
				buffer.WriteD(_taskId);
				buffer.WriteS(_reason);
				break;
			case PlayerTransferResultStatus.PerformAction:
				if (_task == null)
					throw new InvalidOperationException("PERFORM_ACTION requires a transfer task.");
				buffer.WriteC(_task.SourceServerId);
				buffer.WriteC(_task.TargetServerId);
				buffer.WriteD(_task.SourceAccountId);
				buffer.WriteD(_task.TargetAccountId);
				buffer.WriteD(_task.PlayerId);
				buffer.WriteD(_task.Id);
				break;
		}
	}
}
