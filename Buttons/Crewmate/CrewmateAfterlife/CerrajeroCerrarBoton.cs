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

public sealed class CerrajeroCerrarBoton : TownOfUsButton
{
    public override string Name => "Cerrar";
    public override Color TextOutlineColor => CerrajeroRole.CerrajeroColor;
    public override ButtonLocation Location => ButtonLocation.BottomRight;
    public override LoadableAsset<Sprite> Sprite => TownofplusAssets.CerrarButton;

    public override float Cooldown =>
        OptionGroupSingleton<CerrajeroOptions>.Instance.CerrarCooldown.Value;

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

        // Comportamiento POR PUERTA: se puede cerrar si hay alguna puerta
        // ABIERTA en rango (la más cercana).
        return CerrajeroManager.ObtenerPuertaCercana(true) != null;
    }

    protected override void OnClick()
    {
        // Bloqueo DURO por comms saboteadas (además de CanUse).
        if (CommsSabotageManager.CommsSabotadas)
        {
            Timer = Cooldown;
            return;
        }

        // Cierra SOLO la puerta ABIERTA más cercana y lo sincroniza en todos
        // los clientes (host aplica SetDoorway(false) replicado por RPC).
        var puerta = CerrajeroManager.ObtenerPuertaCercana(true);
        if (puerta != null)
        {
            Networking.CerrajeroRpc.EnviarCerrarPuerta((byte)puerta.Id);
        }
    }
}
