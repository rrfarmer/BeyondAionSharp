namespace Aion.LoginServer.Model;

public sealed class PlayerTransferTask
{
	public const byte StatusWait = 0;
	public const byte StatusActive = 1;
	public const byte StatusDone = 2;
	public const byte StatusError = 3;

	public int SourceAccountId { get; set; }

	public int TargetAccountId { get; set; }

	public int PlayerId { get; set; }

	public byte SourceServerId { get; set; }

	public byte TargetServerId { get; set; }

	public int Id { get; set; }

	public byte Status { get; set; }

	public string? Comment { get; set; }
}
