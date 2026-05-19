namespace Aion.LoginServer.Model;

public sealed class BannedIp
{
	public int? Id { get; set; }

	public string Mask { get; set; } = string.Empty;

	public DateTime? TimeEnd { get; set; }

	public bool IsActive(DateTime utcNow)
	{
		return TimeEnd == null || TimeEnd.Value > utcNow;
	}
}
