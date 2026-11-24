using MiraAPI.MeetingAbilities;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Roles.Impostor.ImpostorSupport;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Buttons.Impostor.ImpostorSupport;

/// <summary>
///     Botón «TERMINAR» del Abogado en la reunión: LO MISMO que el botón de
///     revelación del MAYOR de Town of Us Mira
///     (<c>TownOfUs.MeetingAbilities.Classic.Crewmate.Power.MayorRevealButton</c>):
///     un <see cref="MeetingActionButton"/> de MiraAPI que aparece en la fila
///     de botones de habilidad de la reunión (centrada abajo, gestionada por
///     <c>MeetingButtonManager</c> en local (0f, -2.1f, -10f) del padre del
///     tarjeta de voto. MiraAPI lo registra solo (no abstracto, sin
///     MiraIgnore) y lo crea al empezar cada reunión.
///     <para>Gasta 1 carga (la valida y gasta el host en
///     <see cref="AbogadoRpc.EnviarTerminar"/>) y pone SKIP a TODOS.</para>
/// </summary>
public sealed class AbogadoTerminarMeetingBoton : MeetingActionButton
{
    public override string Name => "Terminar";

    public override Color TextOutlineColor => AbogadoRole.AbogadoColor;

    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.AbogadoTerminarAsset;

    // Sin cooldown real en la API (el coste es el cooldown por reuniones).
    public override float Cooldown => 0.0001f;

    public override float InitialCooldown => 0.0001f;

    // Sin usos en la API (la disponibilidad la decide el cooldown).
    public override int MaxUses => 0;

    // Como el Mayor: se deshabilita al usar (Terminar cierra la reunión) y se
    // oculta al terminar la reunión (WrapUp).
    public override bool DisableUponUse => true;

    public override bool HideUponWrapUp => true;

    public override bool Enabled(RoleBehaviour? role)
    {
        if (PlayerControl.LocalPlayer == null || role is not AbogadoRole)
        {
            return false;
        }

        // Muerto o encarcelado: sin botón (como el Mayor con la cárcel).
        if (PlayerControl.LocalPlayer.HasDied() ||
            PlayerControl.LocalPlayer.Data.IsDead)
        {
            return false;
        }

        return true;
    }

    public override bool CanUse()
    {
        // «Gris en discusión»: durante la DISCUSIÓN (o resultados) el botón
        // queda deshabilitado, igual que el botón de la tarjeta.
        if (!AbogadoManager.EnVotacion)
        {
            return false;
        }

        return base.CanUse() && AbogadoManager.HabilidadesDisponibles;
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || !AbogadoManager.EnVotacion)
        {
            return;
        }

        if (!AbogadoManager.HabilidadesDisponibles)
        {
            return;
        }

        AbogadoRpc.EnviarTerminar(local);
    }
}
