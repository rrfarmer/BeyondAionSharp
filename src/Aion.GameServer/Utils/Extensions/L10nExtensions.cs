using Aion.GameServer.Model.Templates;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Utils.Extensions;

// Java parity: model/templates/L10n default method getL10n() = ChatUtil.l10n(getL10nId()).
// IL10n.GetL10n() is a C# default-interface-method, which is NOT accessible through the implementing
// CONCRETE type (only via an IL10n-typed reference), unlike Java where the default method is callable
// on the object. This extension restores `template.GetL10n()` on the concrete template types; an
// IL10n-typed receiver still binds the interface's own default (more-specific), so there is no ambiguity.
public static class L10nExtensions
{
	public static string? GetL10n(this IL10n l10n) => ChatUtil.L10n(l10n.GetL10nId());
}
