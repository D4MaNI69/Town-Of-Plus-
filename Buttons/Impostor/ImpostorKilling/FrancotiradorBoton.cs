using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Options.Impostor.ImpostorKilling;
using TownOfPlus.Roles.Impostor.ImpostorKilling;
using TownOfUs.Buttons;
using UnityEngine;

namespace TownOfPlus.Buttons.Impostor.ImpostorKilling;

/// <summary>
///     Botón cíclico del Francotirador:
///     "Recargando…" → "Recargar" → "Preparando" → "Disparar" → (zoom out) → …
///     Tras matar vuelve a "Recargando…"; si no mata, queda en "Disparar".
/// </summary>
public sealed class FrancotiradorBoton : TownOfUsButton
{
    private FrancotiradorPhase _ultimaFase = (FrancotiradorPhase)(-1);

    public override string Name => NombreDeFase(FrancotiradorManager.Fase);
    public override Color TextOutlineColor => FrancotiradorRole.FrancotiradorColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.RecargarButton;

    public override float Cooldown => FrancotiradorManager.Fase switch
    {
        FrancotiradorPhase.Recargando =>
            OptionGroupSingleton<FrancotiradorOptions>.Instance.TiempoRecarga.Value,
        FrancotiradorPhase.Preparando => FrancotiradorManager.PreparandoSegundos,
        _ => 0f,
    };

    public override bool Enabled(RoleBehaviour? role) =>
        role is FrancotiradorRole &&
        FrancotiradorManager.Fase != FrancotiradorPhase.Apuntando;

    public override bool CanUse()
    {
        if (!FrancotiradorManager.EsLocalFrancotirador || MeetingHud.Instance != null)
        {
            return false;
        }

        return FrancotiradorManager.Fase
            is FrancotiradorPhase.ListoParaRecargar or FrancotiradorPhase.Armado;
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        // El motor del rol (recargas, zoom out, disparo) corre SIEMPRE desde
        // FrancotiradorMotorPatch (HudManager.Update). Antes se llamaba aquí,
        // pero este botón se oculta en fase Apuntando (Enabled=false) y el
        // motor moría: el zoom nunca terminaba y el jugador quedaba congelado.

        // Al cambiar de fase: texto, sprite y temporizador visibles.
        var fase = FrancotiradorManager.Fase;
        if (fase != _ultimaFase)
        {
            _ultimaFase = fase;
            OverrideName(NombreDeFase(fase));
            AplicarSprite(fase);

            switch (fase)
            {
                case FrancotiradorPhase.Recargando:
                    SetTimer(OptionGroupSingleton<FrancotiradorOptions>
                        .Instance.TiempoRecarga.Value);
                    break;
                case FrancotiradorPhase.Preparando:
                    SetTimer(FrancotiradorManager.PreparandoSegundos);
                    break;
                default:
                    SetTimer(0f);
                    break;
            }
        }

        base.FixedUpdate(playerControl);
    }

    protected override void OnClick()
    {
        switch (FrancotiradorManager.Fase)
        {
            case FrancotiradorPhase.ListoParaRecargar:
                FrancotiradorManager.IniciarRecarga();
                break;
            case FrancotiradorPhase.Armado:
                FrancotiradorManager.IniciarApuntado();
                break;
        }
    }

    private static string NombreDeFase(FrancotiradorPhase fase) => fase switch
    {
        FrancotiradorPhase.Recargando => "Recargando...",
        FrancotiradorPhase.ListoParaRecargar => "Recargar",
        FrancotiradorPhase.Preparando => "Preparando",
        _ => "Disparar",
    };

    private void AplicarSprite(FrancotiradorPhase fase)
    {
        var asset = fase is FrancotiradorPhase.Armado or FrancotiradorPhase.Apuntando
            ? TownofplusAssets.DispararButton
            : TownofplusAssets.RecargarButton;

        var spr = asset.LoadAsset();
        if (spr != null && Button != null && Button.graphic != null)
        {
            Button.graphic.sprite = spr;
        }
    }
}