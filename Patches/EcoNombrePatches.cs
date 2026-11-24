using System;
using HarmonyLib;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     REGLA DE VISIÓN DE ROLES DEL ECO: el Eco NO puede ver el rol de NADIE,
///     ni en ronda ni en reunión.
///
///     TOU Mira decide el color de TODOS los nombres con
///     <c>PlayerNameColor.Get(RoleBehaviour)</c> (MiraAPI.Patches.Roles.NameTagPatch
///     es un Prefix que devuelve el color del rol cuando la vista local puede
///     verlo). Aquí se SOBRESCRIBE el resultado final (Postfix: corre también
///     cuando el Prefix de MiraAPI corta el método original).
///     <para>· Cualquier jugador (incluido su ancla) → nombre en blanco.</para>
///     <para>· Él mismo → se deja igual (conoce su propio rol).</para>
///     Como el HUD, el chat y el resto de la UI piden el color por esta misma
///     función, la regla se aplica en TODOS los sitios.
/// </summary>
[HarmonyPatch(typeof(PlayerNameColor), "Get", new[] { typeof(RoleBehaviour) })]
public static class EcoNombreRolPatch
{
    public static void Postfix([HarmonyArgument(0)] RoleBehaviour otherPlayerRole, ref Color __result)
    {
        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local?.Data == null || otherPlayerRole == null)
            {
                return;
            }

            // Solo aplica al jugador local convertido en Eco.
            if (local.Data.Role is not EcoRole)
            {
                return;
            }

            var dueno = ResolverDueno(otherPlayerRole);

            // Su propio rol: sin cambios.
            if (dueno != null && dueno.PlayerId == local.PlayerId)
            {
                return;
            }

            // Nadie más: rol oculto (nombre blanco, sin color de rol).
            __result = Color.white;
        }
        catch
        {
            // Nunca romper el pintado de nombres.
        }
    }

    internal static PlayerControl? ResolverDueno(RoleBehaviour rol)
    {
        try
        {
            var dueno = rol.Player;
            if (dueno != null)
            {
                return dueno;
            }

            // Respaldo: algunos roles no exponen Player; se busca al dueño
            // comparando la instancia del rol.
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p != null && p.Data != null && p.Data.Role == rol)
                {
                    return p;
                }
            }
        }
        catch
        {
            // Sin dueño resoluble.
        }

        return null;
    }
}

/// <summary>
///     BLOQUEO DEL TEXTO DE ROL DEL ECO: TOU Mira escribe el NOMBRE DEL ROL
///     junto al nombre del jugador en <c>HudManagerHelper.GetRoleNameText</c>
///     (lo usan la ronda Y la reunión). Como un fantasma recibe todos los
///     roles (opción "los muertos saben"), el Eco veía el rol de TRIPULANTES,
///     IMPOSTORES y NEUTRALES.
///     <para>Este Prefix recalcula el texto con <c>localGhost</c> y
///     <c>localImp</c> en FALSE (nadie revela su rol) y sin los chequeos de
///     MiraAPI, devolviendo el texto SIN rol y el color en blanco.</para>
///     <para>La pantalla de FIN DE PARTIDA (removeCod) NO se toca: al terminar
///     la partida todos los roles se revelan con normalidad.</para>
/// </summary>
[HarmonyPatch(typeof(TownOfUs.Modules.Components.HudManagerHelper), "GetRoleNameText")]
public static class EcoOcultaTextoRolPatch
{
    /// <summary>Evita recursión al re-invocar el método original desde el Prefix.</summary>
    private static bool _reentrando;

    public static bool Prefix(object[] __args, ref ValueTuple<Color, string> __result)
    {
        try
        {
            if (_reentrando || __args == null || __args.Length < 12)
            {
                return true;
            }

            // El resumen de fin de partida (removeCod) se deja intacto.
            if (__args.Length > 13 && __args[13] is bool removeCod && removeCod)
            {
                return true;
            }

            var local = PlayerControl.LocalPlayer;
            if (local?.Data?.Role is not EcoRole)
            {
                return true;
            }

            var metodo = AccessTools.Method(
                typeof(TownOfUs.Modules.Components.HudManagerHelper),
                "GetRoleNameText");
            if (metodo == null)
            {
                return true;
            }

            // Mismos argumentos, pero SIN revelación de roles:
            // 7 = localGhost (los muertos saben), 8 = localImp (imps se ven),
            // 10 = useMiraApiChecks (revelaciones de otros mods).
            var args = (object[])__args.Clone();
            args[7] = false;
            args[8] = false;
            args[10] = false;

            object? resultado;
            _reentrando = true;
            try
            {
                resultado = metodo.Invoke(null, args);
            }
            finally
            {
                _reentrando = false;
            }

            if (resultado is ValueTuple<Color, string> tupla)
            {
                __result = tupla;
                return false;
            }

            return true;
        }
        catch
        {
            _reentrando = false;
            return true;
        }
    }
}
