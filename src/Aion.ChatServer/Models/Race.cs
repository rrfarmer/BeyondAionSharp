namespace Aion.ChatServer.Models;

public enum Race
{
	Elyos = 0,
	Asmodians = 1,
}

public static class RaceExtensions
{
	public static Race? FromId(int id)
	{
		return id switch
		{
			0 => Race.Elyos,
			1 => Race.Asmodians,
			_ => null
		};
	}
}
