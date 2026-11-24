using Hazel;
using Reactor.Networking.Attributes;
using Reactor.Networking.Extensions;
using Reactor.Networking.Rpc;
using TownOfPlus.Modules;
using UnityEngine;

namespace TownOfPlus.Networking;

/// <summary>
///     Paquete de input del Huésped: dirección normalizada hacia donde el
///     controlador quiere que se mueva el poseído (y su PlayerId para validar).
/// </summary>
internal readonly struct HuespedInputPacket(byte controlledId, Vector2 direction)
{
    public byte ControlledId { get; } = controlledId;
    public Vector2 Direction { get; } = direction;
}

/// <summary>
///     Canal de input del Huésped hacia el poseído (estilo Puppeteer de TOU Mira):
///     - UN SOLO RPC con <c>RpcLocalHandling.Before</c>: el host lo reenvía a TODOS
///       los clientes y el handler corre en cada uno (poseído incluido). Elimina
///       el salto extra del antiguo "host → todos" (2 RPC) y el ACK del reliable.
///     - <c>SendOption=&gt;(SendOption)1</c> (UNRELIABLE): fire-and-forget, sin
///       retransmisión/ACK, ideal para input de alta frecuencia (0 latencia extra).
///     - Cada cliente valida que el remitente sea de verdad el Controlador actual,
///       así un clien malintencionado no puede mover al poseído.
/// </summary>
[RegisterCustomRpc((uint)TownOfPlusRpcCalls.HuespedPosesionInputUnreliable)]
internal sealed class HuespedInputUnreliableRpc(TownOfPlusPlugin plugin, uint id)
    : PlayerCustomRpc<TownOfPlusPlugin, HuespedInputPacket>(plugin, id)
{
    public override RpcLocalHandling LocalHandling => RpcLocalHandling.Before;

    public override SendOption SendOption => (SendOption)1; // Unreliable

    public override void Write(MessageWriter writer, HuespedInputPacket data)
    {
        writer.Write(data.ControlledId);
        writer.Write(data.Direction);
    }

    public override HuespedInputPacket Read(MessageReader reader)
    {
        var controlledId = reader.ReadByte();
        var dir = reader.ReadVector2();
        return new HuespedInputPacket(controlledId, dir);
    }

    public override void Handle(PlayerControl sender, HuespedInputPacket data)
    {
        // Anti-cheat y consistencia en TODOS los clientes: solo el Controlador
        // real puede dictar la dirección del poseído actual.
        if (sender == null || !HuespedManager.Activa ||
            data.ControlledId != HuespedManager.ObjetivoId ||
            sender.PlayerId != HuespedManager.ControladorId)
        {
            return;
        }

        // Normaliza ANTES de guardar para que el poseído camine a la velocidad
        // real que tenga configurada (no a velocidad constante).
        var dir = data.Direction;
        var dirN = dir.sqrMagnitude > 1f ? dir.normalized : dir;
        HuespedManager.AplicarDireccion(dirN);
    }
}