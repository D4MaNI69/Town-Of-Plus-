using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Options.Crewmate.CrewmateSupport;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;
using TownOfUs.Buttons;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Buttons.Crewmate.CrewmateSupport;

/// <summary>
///     Botones del Aislador (Town Of Plus):
///     - Soplar: marca a un jugador VIVO cercano con el contorno #109FB9
///       (solo lo ve el Aislador). Un marcado no puede volver a marcarse.
///     - Burbuja: sin cooldown real (no existe option); al usarse entra en
///       EFECTO 3 s (cooldown = duración de la burbuja, botón en modo efecto)
///       y al terminar vuelve a estar disponible. Requiere ≥1 marcado.
///     - Escudo: al usarse entra en EFECTO 4 s (cooldown = duración del escudo,
///       botón en modo efecto); al terminar aplica el cooldown real (option).
/// </summary>
public sealed class SoplarBoton : TownOfUsTargetButton<PlayerControl>
{
    /// <summary>Rango fijo para soplar (unidades del mapa).</summary>
    private const float RangoSoplar = 2.5f;

    public override string Name => "Soplar";
    public override Color TextOutlineColor => AisladorRole.AisladorColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.AisladorSoplarButton;
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override float Distance => RangoSoplar;
    public override float EffectDuration => 0f;

    public override float Cooldown =>
        OptionGroupSingleton<AisladorOptions>.Instance.CooldownSoplar.Value;

    public override bool Enabled(RoleBehaviour? role) =>
        role is AisladorRole && !AisladorManager.HabilidadesBloqueadas;

    public override PlayerControl? GetTarget() =>
        PlayerControl.LocalPlayer.GetClosestPlayer(true, Distance, false);

    public override void SetOutline(bool active)
    {
        if (Target == null)
        {
            return;
        }

        Target.cosmetics.SetOutline(active,
            new Il2CppSystem.Nullable<Color>(AisladorRole.AisladorColor));
    }

    public override bool IsTargetValid(PlayerControl? target)
    {
        if (target == null || target.Data == null || target.Data.IsDead || target.Data.Disconnected)
        {
            return false;
        }

        // No puede marcarse a sí mismo ni a un jugador ya marcado.
        return target != PlayerControl.LocalPlayer &&
               !AisladorManager.EstaMarcado(target);
    }

    public override bool CanUse()
    {
        // Con el escudo activo NO se puede usar Soplar.
        if (AisladorManager.HabilidadesBloqueadas)
        {
            return false;
        }

        return base.CanUse();
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || Target == null)
        {
            return;
        }

        AisladorRpc.EnviarMarcar(local, Target);
        ResetTarget();
        Timer = Cooldown;
    }
}

/// <summary>
///     Burbuja: sin cooldown por diseño (no existe option); siempre disponible
///     si hay al menos un marcado. Encierra a todos los marcados 3 segundos
///     y al acabar limpia las marcas.
/// </summary>
public sealed class BurbujaBoton : TownOfUsRoleButton<AisladorRole>
{
    public override string Name => "Burbuja";
    public override Color TextOutlineColor => AisladorRole.AisladorColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.AisladorBurbujaButton;
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;

    /// <summary>Sin cooldown real posterior (por diseño, no hay option).</summary>
    public override float Cooldown => 0.01f;
    public override bool ZeroIsInfinite { get; set; } = false;

    /// <summary>Duración del efecto = duración de la burbuja (3 s).</summary>
    public override float EffectDuration => AisladorManager.DuracionBurbuja;

    public override bool Enabled(RoleBehaviour? role) =>
        role is AisladorRole && !AisladorManager.HabilidadesBloqueadas;

    public override bool CanUse()
    {
        if (AisladorManager.HabilidadesBloqueadas || AisladorManager.Marcados.Count == 0)
        {
            return false;
        }

        return base.CanUse();
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || AisladorManager.Marcados.Count == 0)
        {
            return;
        }

        // El propio modo efecto pone Timer = EffectDuration (3 s, con animación).
        AisladorRpc.EnviarBurbuja(local);
    }

    /// <summary>Al acabar la burbuja (3 s), vuelve a estar disponible.</summary>
    public override void OnEffectEnd()
    {
        Timer = Cooldown;
    }
}

/// <summary>
///     Escudo: burbuja que sigue al Aislador en tiempo real (4 s, fijado por
///     diseño); inmune a kills durante la duración, pero sin Soplar ni Burbuja.
/// </summary>
public sealed class EscudoBoton : TownOfUsRoleButton<AisladorRole>
{
    public override string Name => "Escudo";
    public override Color TextOutlineColor => AisladorRole.AisladorColor;
    public override ButtonLocation Location => ButtonLocation.BottomLeft;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.AisladorEscudoButton;
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;

    public override float Cooldown =>
        OptionGroupSingleton<AisladorOptions>.Instance.CooldownEscudo.Value;

    public override bool Enabled(RoleBehaviour? role) => role is AisladorRole;

    /// <summary>Duración del efecto = duración del escudo (4 s).</summary>
    public override float EffectDuration => AisladorManager.DuracionEscudo;

    public override bool CanUse()
    {
        // El escudo no puede encadenarse consigo mismo.
        if (AisladorManager.EscudoActivo)
        {
            return false;
        }

        return base.CanUse();
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null)
        {
            return;
        }

        // El propio modo efecto pone Timer = EffectDuration (4 s, con animación).
        AisladorRpc.EnviarEscudo(local);
    }

    /// <summary>Al acabar el escudo (4 s), arranca el cooldown real (option).</summary>
    public override void OnEffectEnd()
    {
        Timer = Cooldown;
    }
}