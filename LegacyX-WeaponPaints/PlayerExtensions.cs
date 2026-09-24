using CounterStrikeSharp.API.Core;
using LegacyX.Shared.Configuration;
using System.Text;

namespace WeaponPaints;

public static class PlayerExtensions
{
	public static void Print(this CCSPlayerController controller, string message)
	{
		if (WeaponPaints._localizer == null) return;

			controller.PrintToChat(LegacyXChat.System(message));
	}
}
