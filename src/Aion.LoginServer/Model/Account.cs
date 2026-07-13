namespace Aion.LoginServer.Model;

public sealed class Account
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string PasswordHash { get; set; } = string.Empty;

	public DateTime CreationDate { get; set; } = DateTime.UtcNow;

	public byte AccessLevel { get; set; }

	public byte Membership { get; set; }

	public byte Activated { get; set; } = 1;

	public sbyte LastServer { get; set; } = -1;

	public string? LastIp { get; set; }

	public string LastMac { get; set; } = "xx-xx-xx-xx-xx-xx";

	public string? IpForce { get; set; }

	public string? AllowedHddSerial { get; set; }

	public AccountTime AccountTime { get; set; } = new();
}
