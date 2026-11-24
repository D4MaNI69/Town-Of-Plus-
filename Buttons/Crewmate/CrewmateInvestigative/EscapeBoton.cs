using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Options.Crewmate.CrewmateInvestigative;
using TownOfPlus.Roles.Crewmate.CrewmateInvestigative;
using TownOfPlus.Utilities;
using TownOfUs.Buttons;
using UnityEngine;

namespace TownOfPlus.Buttons.Crewmate.CrewmateInvestigative;

/// <summary>
///     Botón ESCAPE del Ermitaño (rework total: sustituye a Marcar y Volver).
///
///     Al pulsarlo aparece el PNG del cursor sobre el ratón (70% de opacidad) y
///     el botón entra en "recarga corta" de 5 s (modo efecto, como la Preparación
///     del Francotirador o el Escudo del Aislador): es el CANAL DE TRANSPORTE.
///     Mientras dura, el Ermitaño PUEDE seguir moviéndose y un click izquierdo
///     lo vuelve INVISIBLE al instante y 0.1 s después lo teletransporta a la
///     posición del cursor.
///     Al terminar (por TP o por timeout) se aplica el cooldown LARGO de la
///     option (<c>CooldownEscape</c>); un click inválido NO lo consume.
/// </summary>
public sealed class EscapeBoton : TownOfUsButton
{
    public override string Name => "Escape";
    public override Color TextOutlineColor => ErmitanoRole.ErmitanoColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => SpritesErmitano.BotonEscapeAsset;
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;

    /// <summary>Cooldown LARGO del Escape (option: 40 s por defecto, rango 30-60).</summary>
    public override float Cooldown =>
        OptionGroupSingleton<ErmitanoOptions>.Instance.CooldownEscape.Value;

    /// <summary>
    ///     Duración del CANAL DE TRANSPORTE = recarga corta de 5 s que muestra el
    ///     botón mientras el PNG del cursor está activo.
    /// </summary>
    public override float EffectDuration => ErmitanoManager.DuracionCanalEscape;

    public override bool Enabled(RoleBehaviour? role) => role is ErmitanoRole;

    public override bool CanUse()
    {
        // Sin habilidades en el lobby, en reunión ni fuera del rol.
        if (LobbyBehaviour.Instance != null || MeetingHud.Instance != null ||
            !ErmitanoManager.EsLocalErmitano)
        {
            return false;
        }

        // Canal en curso: el botón está en modo efecto (recarga corta).
        if (ErmitanoManager.CanalEscapeActivo)
        {
            return false;
        }

        return base.CanUse();
    }

    public override void ClickHandler()
    {
        // Con el canal abierto el click no hace nada: la ventana de 5 s solo se
        // cierra con el TP hecho o con el timeout (un click inválido NO la cierra
        // ni consume el cooldown largo).
        if (ErmitanoManager.CanalEscapeActivo)
        {
            return;
        }

        base.ClickHandler();
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || !ErmitanoManager.EnPartida())
        {
            return;
        }

        // Abre el canal: PNG del cursor (70%) + recarga corta de 5 s.
        ErmitanoManager.IniciarCanalEscape(this);
    }
}
