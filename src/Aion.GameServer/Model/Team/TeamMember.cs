namespace Aion.GameServer.Model.Team;

/// <summary>A member of a team, wrapping the underlying object. Java parity: model/team/TeamMember&lt;M&gt;.</summary>
public interface ITeamMember<out M>
{
    // Java parity: getObjectId()
    int GetObjectId();

    // Java parity: getName()
    string GetName();

    // Java parity: getObject()
    M GetObject();
}
