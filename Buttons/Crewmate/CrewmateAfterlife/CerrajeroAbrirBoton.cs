using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using TownOfPlus.Assets;
using TownOfPlus.Modules;
using TownOfPlus.Options.Crewmate.CrewmateAfterlife;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using TownOfUs.Buttons;
using UnityEngine;

namespace TownOfPlus.Buttons.Crewmate.CrewmateAfterlife;

public sealed class CerrajeroAbrirBoton : TownOfUsButton
{
    public override string Name => "Abrir";
    public override Color TextOutlineColor => CerrajeroRole.CerrajeroColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.AbrirButton;

    public override float Cooldown =>
        OptionGroupSingleton<CerrajeroOptions>.Instance.AbrirCooldown.Value;

    public override bool Enabled(RoleBehaviour? role) => role is CerrajeroRole;

    public override bool CanUse()
    {
        if (!CerrajeroManager.EsCerrajeroLocal)
        {
            return false;
        }

        // Comms saboteadas: los afterlife tripulantes no pueden usar sus
        // botones hasta que alguien arregle las comunicaciones.
        if (CommsSabotageManager.CommsSabotadas)
        {
            return false;
        }

        // Comportamiento POR PUERTA: se puede abrir si hay alguna puerta
        // CERRADA en rango (la más cercana).
        return CerrajeroManager.ObtenerPuertaCercana(false) != null;
    }

    protected override void OnClick()
    {
        // Bloqueo DURO por comms saboteadas (además de CanUse).
        if (CommsSabotageManager.CommsSabotadas)
        {
            Timer = Cooldown;
            return;
        }

        // Abre SOLO la puerta CERRADA más cercana por el canal OFICIAL del
        // juego (RpcUpdateSystem(Doors)): sync vanilla en todos los clientes.
        var puerta = CerrajeroManager.ObtenerPuertaCercana(false);
        if (puerta != null)
        {
            Networking.CerrajeroRpc.EnviarAbrirPuerta((byte)puerta.Id);
        }
    }
}
