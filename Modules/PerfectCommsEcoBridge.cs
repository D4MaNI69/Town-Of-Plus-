using System.Runtime.CompilerServices;
using PerfectComms.Api;
using UnityEngine;

namespace TownOfPlus.Modules;

/// <summary>
///     Puente OPCIONAL con Perfect Comms (soft dependency, GUID
///     "com.edgetel.perfectcomms") para el Eco.
///
///     VOZ — reglas exactas del rol:
///     · El ECO y su ANCLA hablan/oyen ENTRE SÍ en cualquier fase (ronda y
///       reunión). Se usa Route explícito (no solo Pass), igual que hace el
///       Medium de TOU Mira para su enlace Medium↔fantasma: Pass no basta
///       porque el stack base de Perfect Comms mutea por defecto a los
///       hablantes muertos en ronda ("OnlyGhostsCanTalk" / voz de vivos).
///     · Cualquier otro vivo que no sea el ancla NO oye al Eco (Mute), y el
///       Eco NO oye a fantasmas ni a otros muertos/afterlife (Mute) salvo a
///       su ancla.
///     · Lado inverso permitido: los fantasmas/afterlife SÍ pueden oír al Eco
///       (no se les mutea en ese sentido).
///
///     Se registra UNA regla de pareja (RegisterVoicePairRule): el oyente es
///     siempre el jugador local (VoicePairContext.Listener).
/// </summary>
internal static class PerfectCommsEcoBridge
{
    private const string ModId = "com.d4mani.townofplus";

    internal static bool Registrado { get; private set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Register()
    {
        if (Registrado)
            return;
        try
        {
            RegistrarRegla();
            Registrado = true;
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                "[PerfectComms] Regla de voz registrada: el ECO solo habla con su ancla.");
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[PerfectComms] No se pudo registrar la regla de voz del Eco: {ex.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegistrarRegla() =>
        PerfectCommsApi.RegisterVoicePairRule(ModId, ReglaParejaEco);

    private static VoicePairResult ReglaParejaEco(VoicePairContext ctx)
    {
        try
        {
            var oyente = ctx?.Listener;
            var hablante = ctx?.Speaker;
            if (oyente == null || hablante == null)
                return VoicePairResult.Pass;

            // Hablante = Eco con ancla.
            var anclaDelHablante = EcoManager.AnclaDe(hablante.PlayerId);
            bool hablanteEsEco = anclaDelHablante.HasValue;

            // Oyente = Eco con ancla.
            var anclaDelOyente = EcoManager.AnclaDe(oyente.PlayerId);
            bool oyenteEsEco = anclaDelOyente.HasValue;

            if (!hablanteEsEco && !oyenteEsEco)
                return VoicePairResult.Pass;

            // CASO 1: el ECO habla → solo su ANCLA viva lo oye. Cualquier otro
            // oyente VIVO (no ancla) lo tiene MUTEADO. A oyentes muertos /
            // afterlife NO se les mutea (lado inverso permitido: dead oye).
            if (hablanteEsEco)
            {
                if (oyente.PlayerId == anclaDelHablante!.Value)
                {
                    // Route explícito (no Pass): un Pass se queda sin ruta en
                    // ronda porque el stack base no deja hablar a los muertos.
                    var eco2 = hablante;
                    return VoicePairResult.Route(
                        VoicePairRouteShape.Radio, 1f,
                        eco2?.GetTruePosition(), oyente.GetTruePosition(),
                        "Eco a su ancla");
                }
                bool oyenteVivo = oyente.Data != null && !oyente.Data.IsDead;
                if (oyenteVivo)
                    return VoicePairResult.Mute("Solo el ancla oye al Eco");
                return VoicePairResult.Pass;
            }

            // CASO 2: el ECO escucha → SOLO oye a su ANCLA viva. Todo hablante
            // muerto/afterlife (o vivo que no sea su ancla) queda MUTEADO.
            bool hablanteEsAncla = hablante.PlayerId == anclaDelOyente!.Value;
            bool hablanteVivo = hablante.Data != null && !hablante.Data.IsDead;
            if (hablanteEsAncla && hablanteVivo)
            {
                var eco2 = oyente;
                return VoicePairResult.Route(
                    VoicePairRouteShape.Radio, 1f,
                    hablante.GetTruePosition(), eco2?.GetTruePosition(),
                    "Ancla a su Eco");
            }
            return VoicePairResult.Mute("El Eco solo oye a su ancla");
        }
        catch
        {
            return VoicePairResult.Pass;
        }
    }
}
