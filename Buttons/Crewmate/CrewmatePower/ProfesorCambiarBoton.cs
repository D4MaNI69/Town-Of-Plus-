using AmongUs.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Roles.Crewmate.CrewmatePower;
using TownOfPlus.Utilities;
using TownOfUs.Buttons;
using TownOfUs.Modules.Components;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Buttons.Crewmate.CrewmatePower;

public sealed class ProfesorCambiarBoton : TownOfUsRoleButton<ProfesorRole>
{
    public override string Name => "Cambiar Rol";
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => ProfesorRole.ProfesorColor;
    public override float Cooldown => 0.01f;
    public override ButtonLocation Location => ButtonLocation.BottomLeft;
    public override LoadableAsset<Sprite> Sprite => SpritesProfesor.BotonCambiarAsset;
    public override bool ZeroIsInfinite { get; set; } = true;

    /// <summary>
    ///     El botón NO es solo del Profesor: aparece para CUALQUIER jugador con
    ///     un reentreno pendiente (el target marcado en reunión). Por eso no se
    ///     llama a base.Enabled (que exigiría tener el rol Profesor).
    /// </summary>
    public override bool Enabled(RoleBehaviour? role)
    {
        var local = PlayerControl.LocalPlayer;
        return !Disabled && local != null && local.Data != null && !local.Data.IsDead &&
               ProfesorManager.TieneCambioPendiente(local.PlayerId);
    }

    public override void ClickHandler()
    {
        if (!CanClick() || Minigame.Instance || PlayerControl.LocalPlayer.HasDied())
        {
            return;
        }

        OnClick();
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null) return;

        var opciones = ProfesorManager.OpcionesDe(local.PlayerId);
        opciones.RemoveAll(r => r == null || !CambioRolProfesor.EsDraftable(r));
        if (opciones.Count > 3)
        {
            opciones = opciones.Take(3).ToList();
        }

        if (opciones.Count == 0)
        {
            MiraAPI.Utilities.Helpers.CreateAndShowNotification(
                "<b>No hay roles disponibles para cambiar.</b>",
                Color.white, new Vector3(0f, 1f, -20f),
                spr: SpritesProfesor.IconoRolAsset.LoadAsset());
            return;
        }

        if (Minigame.Instance)
        {
            return;
        }

        // ── FIX PROFESOR 1 ── Al abrir el botón: borrar el rol actual
        // (queda sin rol / limpio para elegir). Se envía como cambio a
        // Crewmate vanilla para que el jugador no tenga ningún rol custom
        // mientras elige el nuevo.
        ProfesorRpc.EnviarCambioRol(local, (ushort)RoleTypes.Crewmate);

        var menu = TraitorSelectionMinigame.Create();

        // Menú del Profesor: sólo 3 roles crew filtrados, SIN carta de
        // "impostor aleatorio" (el flag evita la carta extra del minigame).
        // PEDIDO 2: se guarda QUIÉN abrió el menú para, al cerrarlo (elija rol
        // o lo cancele), resolver el cambio y ocultar el botón al instante.
        CambioRolProfesor.MenuCambioRolActivo = true;
        CambioRolProfesor.UsuarioMenu = local.PlayerId;
        menu.Open(
            opciones,
            role =>
            {
                // ── FIX PROFESOR 2 ── Al elegir: CONSERVA el rol elegido
                // (no vuelve al anterior; no pierde el nuevo).
                if (role != null)
                {
                    ProfesorRpc.EnviarCambioRol(local, (ushort)role.Role);
                }

                menu.Close();
            },
            opciones[0].Role);
    }
}
