using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using MiraAPI.Roles;
using TownOfPlus.Modules;
using TownOfUs.Modules;
using TownOfUs.Roles;
using TownOfUs.Utilities;

namespace TownOfPlus.Patches;

/// <summary>
///     EXCLUSIÓN DEFINITIVA de Jester y Kamikaze de los roles afterlife:
///     intercepta MiscUtils.RpcChangeRole de TOU Mira (el RPC que usan los
///     afterlife de TODOS los mods: Haunter/Spectre de TOU Mira, Cerrajero/
///     Saboteador de Town Of Plus, etc.). Si el jugador objetivo era Jester
///     o Kamikaze cuando estaba vivo y el rol NUEVO es un rol fantasma
///     custom (IGhostRole), el cambio se CANCELA antes de enviarse, así que
///     su victoria al ser expulsados (Jester exiliado, Kamikaze detonado)
///     nunca puede perderse por una conversión afterlife. Como es un RPC,
///     bloquearlo en el host basta: nunca llega al resto de clientes.
/// </summary>
[HarmonyPatch(typeof(TownOfUs.Utilities.Extensions), nameof(TownOfUs.Utilities.Extensions.RpcChangeRole),
    new[] { typeof(PlayerControl), typeof(ushort), typeof(bool) })]
public static class BloqueoAfterlifeRolesExpulsionPatch
{
    public static bool Prefix(PlayerControl player, ushort newRoleType)
    {
        try
        {
            // ¿El rol NUEVO es un afterlife custom (ghost role registrado)?
            var registrado = CustomRoleManager.AllRoles.FirstOrDefault(
                x => x.Role == (RoleTypes)newRoleType);
            if (registrado is not IGhostRole)
            {
                return true; // No es un afterlife → cambio de rol normal.
            }

            // ¿El jugador era Jester o Kamikaze cuando estaba vivo?
            var rolVivo = player.GetRoleWhenAlive();
            if (AfterlifeManager.EsRolDeExpulsion(rolVivo))
            {
                TownOfPlusPlugin.Instance?.Log.LogInfo(
                    $"[Afterlife] Bloqueado cambio a afterlife ({registrado.GetType().Name}) " +
                    $"para {player.Data?.PlayerName}: su rol era " +
                    $"{rolVivo?.GetType().Name} (gana por expulsión).");
                return false; // CANCELAR: el RPC nunca se envía.
            }
        }
        catch
        {
            // Ante cualquier error, no bloquear el cambio de rol.
        }

        return true;
    }
}

/// <summary>
///     Excluye a los roles Afterlife de Town Of Plus de la asignación INICIAL
///     de roles (que los mantendría vivos al inicio de la partida). MiraAPI
///     filtra el pool con "!RoleManager.IsGhostRole(role.Role)": al devolver
///     true para nuestros afterlife, MiraAPI nunca los reparte al empezar,
///     PERO sus sliders de ajustes (# DE ROL y PROBABILIDAD %) siguen activos
///     para que AfterlifeManager los lea en la conversión post-reunión.
/// </summary>
[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.IsGhostRole))]
public static class AfterlifeGhostRolePatch
{
    public static void Postfix(RoleTypes role, ref bool __result)
    {
        if (__result || role == RoleTypes.CrewmateGhost || role == RoleTypes.ImpostorGhost)
        {
            return;
        }

        var registrado = CustomRoleManager.AllRoles.FirstOrDefault(x => x.Role == role);
        if (registrado is IGhostRole)
        {
            __result = true;
        }
    }
}