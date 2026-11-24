using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Roles;
using TownOfPlus.Buttons.Crewmate.CrewmatePower;
using TownOfPlus.Roles.Crewmate.CrewmatePower;
using TownOfUs.Modules;
using TownOfUs.Roles;
using TownOfUs.Utilities;

namespace TownOfPlus.Modules;

public static class CambioRolProfesor
{
    /// <summary>
    ///     Indica que el menú de selección actual es el de cambio de rol del
    ///     Profesor. Se usa para que el TraitorSelectionMinigame NO dibuje la
    ///     carta extra de "impostor aleatorio" (ver patch AfterlifeTraitorMenuPatch
    ///     / QuitarRandomMenuPatch en ProfesorPatches.cs).
    /// </summary>
    public static bool MenuCambioRolActivo;

    /// <summary>
    ///     PEDIDO 2 (PROFESOR): jugador que tiene abierto el menú de "Cambiar
    ///     Rol" (null si no hay ninguno). Se usa al cerrar la interfaz para
    ///     resolver y ocultar el botón de ESE jugador.
    /// </summary>
    public static byte? UsuarioMenu { get; set; }

    /// <summary>
    ///     PEDIDO 2 (PROFESOR): al CERRAR el menú de cambio de rol (eligiendo un
    ///     rol o cancelando) el cambio queda RESUELTO en el dueño
    ///     (<see cref="ProfesorManager.MarcarCambioResuelto"/>) y el botón
    ///     "Cambiar Rol" se OCULTA al instante: no se queda pegado tras cerrar el
    ///     menú y no vuelve a mostrarse esa ronda/partida.
    /// </summary>
    public static void AlCerrarMenuCambioRol()
    {
        if (!MenuCambioRolActivo)
        {
            return;
        }

        MenuCambioRolActivo = false;

        var usuario = UsuarioMenu;
        UsuarioMenu = null;
        if (usuario == null)
        {
            return;
        }

        ProfesorManager.MarcarCambioResuelto(usuario.Value);

        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local != null && local.PlayerId == usuario.Value && local.Data?.Role != null)
            {
                // Refresco inmediato (MiraAPI recalcula la visibilidad en los
                // refrescos del HUD; aquí se fuerza al cerrar el menú).
                CustomButtonSingleton<ProfesorCambiarBoton>.Instance.SetActive(false, local.Data.Role);
            }
        }
        catch
        {
            // Si el botón todavía no existe, Enabled ya lo mantiene oculto.
        }
    }

    public static bool EsAfterlife(RoleBehaviour? rol)
    {
        if (rol == null)
        {
            return false;
        }

        try
        {
            if (rol is IGhostRole)
            {
                return true;
            }
        }
        catch
        {
        }

        if (rol is ITownOfUsRole tou)
        {
            return tou.RoleAlignment == RoleAlignment.CrewmateAfterlife ||
                   tou.RoleAlignment == RoleAlignment.ImpostorAfterlife ||
                   tou.RoleAlignment == RoleAlignment.NeutralAfterlife ||
                   tou.RoleAlignment == RoleAlignment.CrewmateGhost ||
                   tou.RoleAlignment == RoleAlignment.ImpostorGhost ||
                   tou.RoleAlignment == RoleAlignment.NeutralGhost;
        }

        return false;
    }

    /// <summary>
    ///     Filtro DRAFT de TOU Mira (réplica de DraftRolePool.IsUsableRole):
    ///     devuelve true SOLO para roles que Draft ofertaría. Con esto, "Cambiar
    ///     Rol" ofrece únicamente roles de tripulante "pickables" en Draft:
    ///     excluye Mayor/Politician y equivalentes de promoción (IsDraftable
    ///     = false), afterlife/ghosts (alineación &gt; GameOutlier), impostores
    ///     y neutrales, y roles no habilitados o sin spawn en el gamemode.
    /// </summary>
    public static bool EsDraftable(RoleBehaviour rol)
    {
        if (rol == null)
        {
            return false;
        }

        try
        {
            if (rol.IsDead)
            {
                return false;
            }

            if (rol.Role == RoleTypes.Crewmate || rol.Role == RoleTypes.Impostor)
            {
                return false;
            }

            if (rol is ITownOfUsRole tou && (!tou.IsDraftable || tou.RoleAlignment > RoleAlignment.GameOutlier))
            {
                return false;
            }

            if (!CustomRoleUtils.CanSpawnOnCurrentMode(rol))
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        return EsRolHabilitado(rol);
    }

    /// <summary>
    ///     Réplica de DraftRolePool.IsRoleEnabled: el rol debe estar HABILITADO
    ///     (cantidad &gt; 0) en la partida actual. Los roles de promoción como
    ///     Mayor tienen cantidad 0 y nunca son realmente "pickables". Usa el
    ///     mismo patrón que AmalgamaManager (GetAssignData / GetNumPerGame).
    /// </summary>
    public static bool EsRolHabilitado(RoleBehaviour rol)
    {
        if (rol == null)
        {
            return false;
        }

        try
        {
            if (rol is ICustomRole customRole && customRole.Configuration.MaxRoleCount != 0)
            {
                var asign = CustomRoleUtils.GetAssignData(rol.Role);
                return asign != null && asign.Count > 0;
            }

            var roleOptions = GameOptionsManager.Instance.currentNormalGameOptions.roleOptions;
            if (roleOptions != null)
            {
                return roleOptions.GetNumPerGame(rol.Role) > 0;
            }
        }
        catch
        {
        }

        return false;
    }

    public static RoleAlignment? AlineacionDe(RoleBehaviour? rol)
    {
        if (rol is ITownOfUsRole tou)
        {
            return tou.RoleAlignment;
        }

        return null;
    }

    public static bool DestinoPermitido(RoleAlignment? origen, RoleAlignment? destino)
    {
        if (origen == RoleAlignment.CrewmateKilling)
        {
            return destino != RoleAlignment.CrewmatePower;
        }

        if (origen == RoleAlignment.CrewmateSupport)
        {
            return destino != RoleAlignment.CrewmateKilling &&
                   destino != RoleAlignment.CrewmatePower;
        }

        return true;
    }

    public static List<RoleBehaviour> ConstruirOpciones(PlayerControl target, bool esAutoReentreno)
    {
        var salida = new List<RoleBehaviour>();
        if (target?.Data == null)
        {
            return salida;
        }

        RoleBehaviour? rolActual = null;
        try
        {
            rolActual = target.Data.Role;
            if (target.Data.IsDead)
            {
                rolActual = target.GetRoleWhenAlive() ?? rolActual;
            }
        }
        catch
        {
            rolActual = target.Data.Role;
        }

        var origen = AlineacionDe(rolActual);
        var candidatos = MiscUtils.GetPotentialRoles()
            .Where(r => r != null && r.IsCrewmate())
            .Where(r => !EsAfterlife(r))
            .Where(r => EsDraftable(r))
            .Where(r => r is not ProfesorRole)
            .Where(r => r is ITownOfUsRole tou &&
                        (tou.RoleAlignment == RoleAlignment.CrewmateKilling ||
                         tou.RoleAlignment == RoleAlignment.CrewmatePower ||
                         tou.RoleAlignment == RoleAlignment.CrewmateProtective ||
                         tou.RoleAlignment == RoleAlignment.CrewmateSupport ||
                         tou.RoleAlignment == RoleAlignment.CrewmateInvestigative ||
                         tou.RoleAlignment == RoleAlignment.Crewmate))
            .Where(r => DestinoPermitido(origen, AlineacionDe(r)))
            .Where(r => r is not ICustomRole custom ||
                        custom.Configuration.MaxRoleCount <= 0 ||
                        ContarRolEnPartida(r) < custom.Configuration.MaxRoleCount)
            .ToList();

        candidatos.Shuffle();
        foreach (var c in candidatos.Take(3))
        {
            salida.Add(c);
        }

        return salida;
    }

    private static int ContarRolEnPartida(RoleBehaviour rol)
    {
        var n = 0;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p?.Data == null)
            {
                continue;
            }

            RoleBehaviour? r = p.Data.Role;
            try
            {
                if (p.Data.IsDead)
                {
                    r = p.GetRoleWhenAlive() ?? r;
                }
            }
            catch
            {
            }

            if (r != null && (ushort)r.Role == (ushort)rol.Role)
            {
                n++;
            }
        }

        return n;
    }
}
