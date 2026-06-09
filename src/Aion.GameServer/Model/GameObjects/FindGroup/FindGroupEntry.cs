namespace Aion.GameServer.Model.GameObjects.FindGroup;

/// <summary>
/// Java parity: model/gameobjects/findGroup/FindGroupEntry. Java sealed interface `permits GroupRecruitment,
/// GroupApplication, ServerWideGroup`; C# has no sealed-interface permits clause → plain marker interface
/// (the permit constraint is not enforceable in C#).
/// </summary>
public interface FindGroupEntry
{
}
