namespace Aion.GameServer.Model.Team;

/// <summary>A deferred team event with a guard condition. Java parity: model/team/TeamEvent.</summary>
public interface ITeamEvent
{
    // Java parity: handleEvent()
    void HandleEvent();

    // Java parity: checkCondition()
    bool CheckCondition();
}
