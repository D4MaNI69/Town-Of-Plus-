using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Options.Impostor.ImpostorConcealing;
using TownOfPlus.Roles.Impostor.ImpostorConcealing;
using TownOfPlus.Utilities;
using TownOfUs.Assets;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using UnityEngine;

namespace TownOfPlus.Buttons.Impostor.ImpostorConcealing;

/// <summary>
///     Botón único "Engaño" del Kamikaze: cada uso ejecuta la habilidad
///     actual del ciclo (1 Cambiaformas → 2 Camuflaje → 3 Ducto Falso →
///     4 Arrastre → 1…) y avanza el ciclo, reiniciando el cooldown.
///     El nombre y el icono muestran la habilidad que se ejecutará AL PULSAR.
/// </summary>
public sealed class KamikazeBoton : TownOfUsButton
{
    private HabilidadKamikaze _ultimaHabilidad = (HabilidadKamikaze)(-1);

    public override string Name => NombreDeHabilidad(KamikazeManager.HabilidadActual);
    public override Color TextOutlineColor => KamikazeRole.KamikazeColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    // Sprite HARDENED (anti-Engano): EnganoButton apunta a Transformar.png
    // (PNG que SÍ existe) y el getter Sprite de ESTE botón usa un
    // LoadableAsset que NUNCA lanza (cae a sprite procedural generado en
    // código si todo lo demás falla). Así el HUD nunca muere por un PNG
    // borrado, aunque alguien borre PNGs del proyecto en el futuro.

    public override LoadableAsset<Sprite> Sprite => IconoSeguro;
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;

    public override float Cooldown =>
        OptionGroupSingleton<KamikazeOptions>.Instance.CooldownEngano.Value;

    public override bool Enabled(RoleBehaviour? role) => role is KamikazeRole;

    public override bool CanUse()
    {
        // Las habilidades del Kamikaze NO se usan en el lobby: los RPC custom
        // en LobbyBehaviour disparan el aviso de "cheating" del anticheat TOU.
        if (LobbyBehaviour.Instance != null)
        {
            return false;
        }

        if (!base.CanUse() || !KamikazeManager.EsLocalKamikaze || MeetingHud.Instance != null)
        {
            return false;
        }

        // Arrastre solo es ejecutable si hay un cuerpo cerca.
        if (KamikazeManager.HabilidadActual == HabilidadKamikaze.Arrastre)
        {
            var local = PlayerControl.LocalPlayer;
            return local != null &&
                   KamikazeManager.CuerpoCercano(local, KamikazeManager.DistanciaArrastre) != null;
        }

        return true;
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        // Al cambiar la habilidad del ciclo: nombre e icono visibles al instante.
        var habilidad = KamikazeManager.HabilidadActual;
        if (habilidad != _ultimaHabilidad)
        {
            _ultimaHabilidad = habilidad;
            OverrideName(NombreDeHabilidad(habilidad));
            AplicarSprite(habilidad);
        }

        base.FixedUpdate(playerControl);
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || !KamikazeManager.EsLocalKamikaze)
        {
            return;
        }

        var habilidad = KamikazeManager.HabilidadActual;
        var ejecutada = false;

        switch (habilidad)
        {
            case HabilidadKamikaze.Cambiaformas:
            {
                var objetivo = KamikazeManager.ElegirObjetivoCambiaformas(local);
                if (objetivo != null)
                {
                    KamikazeRpc.EnviarCambiaformas(local, objetivo);
                    ejecutada = true;
                }

                break;
            }

            case HabilidadKamikaze.Camuflaje:
                KamikazeRpc.EnviarCamuflaje(local);
                ejecutada = true;
                break;

            case HabilidadKamikaze.DuctoFalso:
                KamikazeRpc.EnviarDuctoFalso(local, local.GetTruePosition());
                ejecutada = true;
                break;

            case HabilidadKamikaze.Arrastre:
            {
                var cuerpo = KamikazeManager.CuerpoCercano(local, KamikazeManager.DistanciaArrastre);
                if (cuerpo != null)
                {
                    KamikazeRpc.EnviarArrastre(local, cuerpo.ParentId);
                    ejecutada = true;
                }

                break;
            }
        }

        if (!ejecutada)
        {
            return;
        }

        // Sincroniza el ciclo (1→2→3→4→1) en todos los clientes y reinicia el cooldown.
        KamikazeRpc.EnviarEngano(local, (byte)((KamikazeManager.IndiceCiclo + 1) % 4));
        Timer = Cooldown;
    }

    private static string NombreDeHabilidad(HabilidadKamikaze habilidad) => habilidad switch
    {
        HabilidadKamikaze.Cambiaformas => TouLocale.Get(
            "TopKamikazeMorph", "Morph"),
        HabilidadKamikaze.Camuflaje => TouLocale.Get(
            "TopKamikazeSwoop", "Swoop"),
        HabilidadKamikaze.DuctoFalso => TouLocale.Get(
            "TopKamikazeMine", "Mine"),
        _ => TouLocale.Get("TopKamikazeDrag", "Drag"),
    };

    private void AplicarSprite(HabilidadKamikaze habilidad)
    {
        // Iconos del ciclo de TOU Mira (Morph | Swoop | Mine | Drag);
        // fallback a los assets propios del proyecto si TOU no los carga.
        var spr = habilidad switch
        {
            HabilidadKamikaze.Cambiaformas => CargarSprite(
                TouImpAssets.MorphSprite, TownofplusAssets.EnganoCambiaformasButton),
            HabilidadKamikaze.Camuflaje => CargarSprite(
                TouImpAssets.SwoopSprite, TownofplusAssets.EnganoCamuflajeButton),
            HabilidadKamikaze.DuctoFalso => CargarSprite(
                TouImpAssets.MineSprite, TownofplusAssets.EnganoDuctoButton),
            _ => CargarSprite(
                TouImpAssets.DragSprite, TownofplusAssets.EnganoArrastreButton),
        };

        if (spr != null && Button != null && Button.graphic != null)
        {
            Button.graphic.sprite = spr;
        }
    }

    private static Sprite? CargarSprite(LoadableAsset<Sprite> primario, LoadableAsset<Sprite> secundario)
    {
        try
        {
            return primario.LoadAsset() ?? secundario.LoadAsset();
        }
        catch
        {
            return secundario.LoadAsset();
        }
    }

    /// <summary>
    ///     Sprite SEGURO del botón: NUNCA lanza (anti-Engano). MiraAPI lee el
    ///     getter Sprite DENTRO de su HudManagerPatches.StartPostfix: si el
    ///     asset subyacente lanzaba (PNG viejo inexistente), el error mataba
    ///     HudManager y dejaba Update roto en CADA frame. Por eso este getter
    ///     NO crea el asset duro a mano: usa el fallback procedural propio que
    ///     jamás falla (el FixedUpdate pone el sprite de la habilidad actual).
    /// </summary>
    private static readonly LoadableAsset<Sprite> IconoSeguro = new SpriteSeguroKamikaze();

    private sealed class SpriteSeguroKamikaze : LoadableAsset<Sprite>
    {
        private Sprite? _cache;

        public override Sprite LoadAsset()
        {
            if (_cache != null)
            {
                return _cache;
            }

            try
            {
                // Fallback inicial: las propias TRANS/DESV del ciclo (PNG que
                // SÍ existen). NUNCA el Engano.png viejo inexistente.
                var spr = TownofplusAssets.EnganoButton.LoadAsset() ??
                          TownofplusAssets.KamikazeIcon.LoadAsset() ??
                          SpritesKamikazeIcono();
                _cache = spr ?? SpritesKamikazeIcono();
            }
            catch
            {
                _cache = SpritesKamikazeIcono();
            }

            return _cache;
        }

        /// <summary>
        ///     Fallback procedural ÚLTIMO: siempre genera un sprite (jamás
        ///     lanza, jamás devuelve null), para que MiraAPI pueda crear el
        ///     botón aunque falten los PNG.
        /// </summary>
        private static Sprite SpritesKamikazeIcono()
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var rojo = new Color32(220, 40, 40, 255);
            var pixeles = new Color32[64 * 64];
            for (var i = 0; i < pixeles.Length; i++)
            {
                pixeles[i] = rojo;
            }

            tex.SetPixels32(pixeles);
            tex.Apply(false, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            var salida = UnityEngine.Sprite.Create(
                tex, new Rect(0, 0, 64, 64),
                new UnityEngine.Vector2(0.5f, 0.5f), 100f);
            return salida;
        }
    }
}