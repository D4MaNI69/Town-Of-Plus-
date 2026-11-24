using HarmonyLib;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Player;
using MiraAPI.Roles;
using Reactor.Utilities.Extensions;
using TownOfPlus.Modules;
using TownOfPlus.Roles.Crewmate.CrewmatePower;
using TownOfPlus.Roles.Impostor.ImpostorPower;
using TownOfUs.Modules;
using TownOfUs.Roles;
using UnityEngine;

namespace TownOfPlus.Patches;

public static class ProfesorEventos
{
    [MiraAPI.Events.RegisterEvent]
    public static void AlCompletarTarea(CompleteTaskEvent @event)
    {
        try
        {
            ProfesorManager.AlCompletarTarea(@event.Player);
        }
        catch
        {
        }
    }

    [MiraAPI.Events.RegisterEvent]
    public static void AlEmpezarRonda(RoundStartEvent @event)
    {
        try
        {
            if (!@event.TriggeredByIntro)
            {
                ProfesorManager.AlEmpezarRonda();
            }
            else
            {
                ProfesorManager.ChequearNuevaPartida();
            }
        }
        catch
        {
        }
    }
}

/// <summary>
///     PEDIDO GLOBAL: en modo DISCUSIÓN de cualquier reunión, TODOS los
///     botones de habilidad de Town Of Plus quedan BLOQUEADOS (Reentrenar,
///     Revelar, Sacrificar, etc.). El chat, ajustes y salir de partida
///     siguen libres (no son ActionButtons de habilidad). Al pasar a
///     VOTACIÓN los botones vuelven a su visibilidad normal.
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
public static class DiscusionBloqueaBotonesPatch
{
    public static void Postfix()
    {
        try
        {
            var hud = HudManager.Instance;
            if (hud == null)
            {
                return;
            }

            var enDiscusion = ProfesorManager.DiscusionActiva;

            // ActionButtons de habilidad registrados por MiraAPI (los botones
            // custom de TODOS los roles: Reentrenar, Sacrificar, Engaño...).
            // En discusión se ocultan; al pasar a votación se reactivan.
            // El chat, ajustes y salir NO son CustomButtonManager.Buttons y
            // por eso quedan libres.
            var botones = MiraAPI.Hud.CustomButtonManager.Buttons;
            if (botones != null)
            {
                foreach (var boton in botones.ToArray())
                {
                    if (boton == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (enDiscusion)
                        {
                            boton.SetActive(false, PlayerControl.LocalPlayer?.Data?.Role);
                        }
                    }
                    catch
                    {
                        // Si el hook falla con un botón, seguir con el resto.
                    }
                }
            }

            // Botones de reunión custom de TOU Mira/Divani/TOP (MeetingMenu de
            // habilidad: Reentrenar, Recruit, Reveal...): en discusión los
            // voteareas los ocultan vía su Update; el Click del Profesor
            // además valida ProfesorManager.DiscusionActiva.
        }
        catch
        {
            // Nunca romper el HUD por el bloqueo.
        }
    }
}

[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
public static class ProfesorWrapUpPatch
{
    public static void Postfix()
    {
        try
        {
            ProfesorManager.AlTerminarReunion();
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
public static class ProfesorGameEndPatch
{
    public static void Postfix()
    {
        try
        {
            ProfesorManager.ResetEntrePartidas();
        }
        catch
        {
        }
    }
}

/// <summary>
///     PEDIDO 3 (PROFESOR): el menú "Cambiar Rol" debe mostrar EXACTAMENTE los
///     3 roles de tripulante filtrados y NADA más. El TraitorSelectionMinigame
///     añade SIEMPRE una carta extra "Random" (impostor aleatorio) al final de
///     Begin(). Este Postfix la ELIMINA (Destroy) cuando el menú es el
///     del Profesor (flag MenuCambioRolActivo), dejando solo esas cartas.
/// </summary>
[HarmonyPatch(typeof(TownOfUs.Modules.Components.TraitorSelectionMinigame), "Begin")]
public static class QuitarRandomMenuPatch
{
    public static void Postfix(TownOfUs.Modules.Components.TraitorSelectionMinigame __instance)
    {
        try
        {
            if (!CambioRolProfesor.MenuCambioRolActivo)
            {
                return;
            }

            var holder = __instance.RolesHolder;
            if (holder == null)
            {
                return;
            }

            // La carta "Random" (impostor aleatorio) es SIEMPRE la última
            // creada por Begin(); la destruimos por completo para que no exista.
            Transform? ultima = null;
            foreach (var h in holder.transform)
            {
                ultima = h.Cast<Transform>();
            }

            if (ultima != null)
            {
                ultima.gameObject.Destroy();
            }
        }
        catch
        {
        }
    }
}

/// <summary>
///     PEDIDO 2 (PROFESOR): al CERRAR la interfaz de "Cambiar Rol" —eligiendo un
///     rol o cancelándola— el cambio queda resuelto y el botón "Cambiar Rol"
///     desaparece al instante: no se queda pegado tras cerrar el menú y no
///     vuelve a mostrarse esa ronda/partida para ese jugador.
/// </summary>
[HarmonyPatch(typeof(TownOfUs.Modules.Components.TraitorSelectionMinigame), nameof(TownOfUs.Modules.Components.TraitorSelectionMinigame.Close))]
public static class ProfesorMenuCambioRolCerradoPatch
{
    public static void Postfix()
    {
        try
        {
            CambioRolProfesor.AlCerrarMenuCambioRol();
        }
        catch
        {
            // Cerrar el menú jamás debe romper la partida.
        }
    }
}

/// <summary>
///     PEDIDO 1 (PATRÓN "Politician Reveal"): durante la DISCUSIÓN los botones
///     de reunión del PROFESOR (Reentrenar) y del DICTADOR (Revelar) se ven
///     GRISES y no son clickeables; solo se usan en VOTACIÓN. Cada frame se
///     recalcula el estado real de cada botón (discusión, ya marcado, ya
///     reentrenado, sin cargas) y se pinta gris o blanco según corresponda.
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
public static class ReunionBotonEstadoPatch
{
    private static readonly Color Gris = new Color(0.24f, 0.24f, 0.24f, 1f);

    public static void Postfix()
    {
        if (MeetingHud.Instance == null)
        {
            return;
        }

        var discutiendo = ProfesorManager.DiscusionActiva;

        foreach (var menu in MeetingMenu.Instances)
        {
            if (menu.Owner is ProfesorRole profesor)
            {
                foreach (var par in menu.Buttons)
                {
                    Pintar(par.Value, ProfesorUsable(profesor, par.Key, discutiendo));
                }
            }
            else if (menu.Owner is DictadorRole dictador)
            {
                foreach (var par in menu.Buttons)
                {
                    Pintar(par.Value, !discutiendo && dictador.PuedeRevelarse);
                }
            }
        }
    }

    private static bool ProfesorUsable(ProfesorRole profesor, byte targetId, bool discutiendo)
    {
        if (discutiendo)
        {
            return false;
        }

        if (!ProfesorManager.PuedeMarcar(profesor.Player))
        {
            return false;
        }

        if (ProfesorManager.YaFueReentrenado(targetId))
        {
            return false;
        }

        if (ProfesorManager.MarcadoEstaReunion(profesor.Player.PlayerId, targetId))
        {
            return false;
        }

        if (ProfesorManager.MarcadosEnReunion(profesor.Player.PlayerId) >=
            ProfesorManager.CargasDe(profesor.Player.PlayerId))
        {
            return false;
        }

        return true;
    }

    private static void Pintar(GameObject? boton, bool usable)
    {
        if (boton == null)
        {
            return;
        }

        var renderer = boton.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.color = usable ? Color.white : Gris;
    }
}
/// <summary>
///     AFTERLIFE GLOBAL: los afterlife (IGhostRole o alineación
///     Afterlife/Ghost) nunca entran a menús de cambio de rol.
///     TraitorSelectionMinigame.Open recibe la lista ANTES de crear las
///     cartas: aquí se purga in-place (misma instancia List).
/// </summary>
[HarmonyPatch(typeof(TownOfUs.Modules.Components.TraitorSelectionMinigame), nameof(TownOfUs.Modules.Components.TraitorSelectionMinigame.Open))]
public static class AfterlifeTraitorMenuPatch
{
    public static void Prefix(System.Collections.Generic.List<RoleBehaviour> roles)
    {
        try
        {
            if (roles == null)
            {
                return;
            }

            for (var i = roles.Count - 1; i >= 0; i--)
            {
                if (CambioRolProfesor.EsAfterlife(roles[i]))
                {
                    roles.RemoveAt(i);
                }
            }
        }
        catch
        {
        }
    }
}

/// <summary>
///     AFTERLIFE GLOBAL: mismo filtro para el menú Ambassador
///     (AmbassadorSelectionMinigame.Open).
/// </summary>
[HarmonyPatch(typeof(TownOfUs.Modules.Components.AmbassadorSelectionMinigame), nameof(TownOfUs.Modules.Components.AmbassadorSelectionMinigame.Open))]
public static class AfterlifeAmbassadorMenuPatch
{
    public static void Prefix(System.Collections.Generic.List<RoleBehaviour> roles)
    {
        try
        {
            if (roles == null)
            {
                return;
            }

            for (var i = roles.Count - 1; i >= 0; i--)
            {
                if (CambioRolProfesor.EsAfterlife(roles[i]))
                {
                    roles.RemoveAt(i);
                }
            }
        }
        catch
        {
        }
    }
}

/// <summary>
///     Unicidad Profesor: si ya existió 1 Profesor en esta partida, ningún
///     cambio de rol puede volver a dar Profesor (patrón Amalgama).
///     El cambio vanilla/custom pasa por Extensions.ChangeRole.
/// </summary>
[HarmonyPatch(typeof(TownOfUs.Utilities.Extensions), nameof(TownOfUs.Utilities.Extensions.ChangeRole))]
public static class ProfesorUnicidadPatch
{
    public static bool Prefix(PlayerControl player, ushort newRoleType)
    {
        try
        {
            var registrado = CustomRoleManager.AllRoles.FirstOrDefault(
                x => x != null && (ushort)x.Role == newRoleType);
            if (registrado is Roles.Crewmate.CrewmatePower.ProfesorRole)
            {
                ProfesorManager.ChequearNuevaPartida();
                if (ProfesorManager.ProfesorExistio)
                {
                    var yaEs = player?.Data?.Role is Roles.Crewmate.CrewmatePower.ProfesorRole;
                    if (!yaEs)
                    {
                        return false;
                    }
                }
            }
        }
        catch
        {
        }

        return true;
    }
}
