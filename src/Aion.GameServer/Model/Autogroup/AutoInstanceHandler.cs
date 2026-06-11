using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Autogroup;

/// <summary>Java parity: model/autogroup/AutoInstanceHandler.</summary>
public interface AutoInstanceHandler
{
    void OnInstanceCreate(WorldMapInstance instance);

    AGQuestion AddLookingForParty(LookingForParty lookingForParty);

    void OnEnterInstance(Player player);

    void OnLeaveInstance(Player player);

    void OnPressEnter(Player player);

    void Unregister(Player player);
}
