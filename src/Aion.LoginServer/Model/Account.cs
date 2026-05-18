namespace Aion.LoginServer.Model;

public sealed class Account
{
	public int Id { get; init; }

	public string Name { get; init; } = string.Empty;

	public byte AccessLevel { get; init; }

	public byte Membership { get; init; }

	public byte LastServer { get; set; }

	public long Toll { get; init; }

	public long CreationDate { get; init; }

	public string AllowedHddSerial { get; init; } = string.Empty;

	public AccountTime AccountTime { get; init; } = new();
}
