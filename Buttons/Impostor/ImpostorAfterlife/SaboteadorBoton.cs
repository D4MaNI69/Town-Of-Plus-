using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Networking;
using TownOfPlus.Options.Impostor.ImpostorAfterlife;
using TownOfPlus.Roles.Impostor.ImpostorAfterlife;
using TownOfUs.Buttons;
using UnityEngine;

namespace TownOfPlus.Buttons.Impostor.ImpostorAfterlife;

/// <summary>
///     Botón "Luces Fuera" del Saboteador (fantasma impostor): dispara el
///     SABOTAJE REAL de Electrical/Luces durante el tiempo configurado y el
///     rol lo resuelve automáticamente al acabar.
///
///     FIX de "el botón no funciona": TownOfUsButton oculta y bloquea los
///     botones de jugadores MUERTOS (SetActive → !HasDied y CanUse →
///     UsableInDeath). El Saboteador es un rol afterlife (muerto), así que se
///     sobreescriben ambos puntos y el botón vuelve a mostrarse y ejecutarse.
///     Reglas de uso:
///     - Se puede usar si NO hay sabotaje de luces activo (aunque haya O2,
///       Reactor, Comms u otros sabos).
///     - NO se puede usar solo cuando las luces YA están saboteadas.
/// </summary>
public sealed class SaboteadorBoton : TownOfUsButton
{
    public override string Name => TownOfUs.Modules.Localization.TouLocale.Get(
        "TopRoleSaboteadorLucesFuera", "Luces Fuera");

    public override Color TextOutlineColor => SaboteadorRole.SaboteadorColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.LucesFueraButton;
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;

    /// <summary>El Saboteador es un rol afterlife (muerto): el botón debe
    ///     funcionar en muerte. FIX del botón roto.</summary>
    public override bool UsableInDeath => true;

    public override float Cooldown =>
        OptionGroupSingleton<SaboteadorOptions>.Instance.CooldownLucesFuera.Value;

    public override bool Enabled(RoleBehaviour? role) => role is SaboteadorRole;

    /// <summary>FIX: sin el gate de !HasDied de TOU (los afterlife SÍ pueden
    ///     usar su botón). Solo requiere que el rol esté activo.</summary>
    public override void SetActive(bool visible, RoleBehaviour role)
    {
        Button?.ToggleVisible(visible && Enabled(role));
    }

    public override bool CanUse()
    {
        // base.CanUse() de TOU/Mira: cooldown, reunión/chat, modificadores que
        // bloquean habilidades, etc. Con UsableInDeath=true pasa en muerte.
        if (!base.CanUse() || !SaboteadorManager.EsSaboteadorLocal)
        {
            return false;
        }

        // Regla del rol: NO se puede usar cuando las luces YA están saboteadas
        // (sabo vanilla de alguien más, o el propio efecto aún en curso).
        // Con O2/Reactor/Comms activos SÍ se puede usar.
        return !SaboteadorManager.LucesVanillaSaboteadas;
    }

    protected override void OnClick()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || !SaboteadorManager.EsSaboteadorLocal)
        {
            return;
        }

        var duracion = OptionGroupSingleton<SaboteadorOptions>.Instance.TiempoLucesFuera.Value;
        SaboteadorRpc.EnviarLucesFuera(local, duracion);
        Timer = Cooldown;
    }
}