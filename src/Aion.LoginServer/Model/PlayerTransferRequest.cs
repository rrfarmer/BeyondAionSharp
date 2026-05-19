namespace Aion.LoginServer.Model;

public sealed class PlayerTransferRequest
{
	public byte ServerId { get; set; }

	public byte TargetServerId { get; set; }

	public Account? TargetAccount { get; set; }

	public byte[] Db { get; set; } = Array.Empty<byte>();

	public string Name { get; set; } = string.Empty;

	public int TargetAccountId { get; set; }

	public Account? Account { get; set; }

	public Account? SourceAccount { get; set; }

	public int TaskId { get; set; }
}
