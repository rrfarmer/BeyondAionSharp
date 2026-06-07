using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Templates;

/// <summary>
/// Implemented by all templates that include a client description ID field (name ID / l10n ID).
/// Java parity: model/templates/L10n.
/// </summary>
public interface IL10n
{
    // Java parity: L10n::getL10nId() — the client string ID
    int GetL10nId();

    // Java parity: L10n::getL10n() — default method; returns the client string via ChatUtil.l10n(id)
    string? GetL10n() => ChatUtil.L10n(GetL10nId());
}
