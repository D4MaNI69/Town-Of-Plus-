using System;
using System.Runtime.CompilerServices;
using PerfectComms.Api;

namespace TownOfPlus.Modules;

/// <summary>
///     Puente OPCIONAL con Perfect Comms (soft dependency, GUID
///     "com.edgetel.perfectcomms"): mientras el Huésped controla a un jugador,
///     el controlado queda con EL MICRÓFONO SILENCIADO (no se le oye en el chat
///     de voz) y recupera su voz en cuanto la posesión termina.
///     <para>MECANISMO (el mismo que usan otros roles de otros mods para
///     silenciar temporalmente a un tripulante): se registra UNA regla de voz
///     por jugador con <c>PerfectCommsApi.RegisterVoiceRule</c> que devuelve
///     <c>VoiceRuleResult.Mute(reason)</c> para el jugador evaluado. Perfect
///     Comms la evalúa en cada cliente (~20 veces/segundo) y aplica el mute en
///     todas las fases de voz.</para>
///     <para>La posesión está sincronizada por RPC (HuespedManager.Activa /
///     ObjetivoId), así que la regla silencia al poseído en TODOS los clientes.
///     El Huésped (fantasma) y el resto de jugadores siguen hablando normal.</para>
///     <para>SOLO se llama si el plugin está instalado (TownofplusPlugin.Load
///     comprueba el GUID). PerfectComms.dll es una dependencia de COMPILACIÓN
///     (PrivateAssets=all): no se distribuye con el mod y, si no está instalado,
///     este código nunca se ejecuta.</para>
/// </summary>
internal static class PerfectCommsHuespedBridge
{
    private const string ModId = "com.d4mani.townofplus";

    /// <summary>¿La regla de voz ya quedó registrada?</summary>
    internal static bool Registrado { get; private set; }

    /// <summary>
    ///     Registra la regla de voz por jugador. Si Perfect Comms no está
    ///     instalado o su API cambió, no hace nada (el rol sigue funcionando sin
    ///     mute) y lo deja anotado en el log.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Register()
    {
        if (Registrado)
        {
            return;
        }

        try
        {
            // El cuerpo tipado vive en otro método [NoInlining]: los tipos de
            // Perfect Comms solo se resuelven si esta llamada llega a ejecutarse.
            RegistrarRegla();

            Registrado = true;
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                "[PerfectComms] Regla de voz registrada: el POSEÍDO queda " +
                "silenciado (mic mute) mientras el Huésped lo controla.");
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[PerfectComms] No se pudo registrar la regla de voz del Huésped: {ex.Message}");
        }
    }

    /// <summary>Registro tipado contra la API pública de Perfect Comms (1.2).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegistrarRegla() =>
        PerfectCommsApi.RegisterVoiceRule(ModId, ReglaPoseido);

    /// <summary>
    ///     REGLA DE VOZ: MUTE si el jugador evaluado es el POSEÍDO durante la
    ///     posesión; PASS en cualquier otro caso (el Huésped y el resto siguen
    ///     hablando normal). Debe ser barata y no lanzar excepciones (corre
    ///     ~20x/segundo).
    /// </summary>
    private static VoiceRuleResult ReglaPoseido(VoiceRuleContext ctx)
    {
        try
        {
            var jugador = ctx?.Player;
            if (jugador != null && HuespedManager.Activa &&
                jugador.PlayerId == HuespedManager.ObjetivoId)
            {
                return VoiceRuleResult.Mute("Controlado por el Huésped");
            }
        }
        catch
        {
            // Ante cualquier fallo: sin opinión (no se silencia a nadie).
        }

        return VoiceRuleResult.Pass;
    }
}
