namespace Aion.GameServer.Model.GameObjects;

[Flags]
public enum PlayerCreatureState
{
	None = 0,
	Active = 1,
	Flying = 1 << 1,
	Resting = 1 << 2,
	FloatingCorpse = 1 << 3,
	WeaponEquipped = 1 << 5,
	Powershard = 1 << 7,
	Gliding = 1 << 9,
	Chair = Flying | Resting,
}
