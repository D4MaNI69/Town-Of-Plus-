using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Meeting.Voting;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using MiraAPI.Voting;
using Reactor.Utilities.Extensions;
using TownOfPlus.Modules;
using TownOfPlus.Options.Impostor.ImpostorPower;
using TownOfPlus.Roles.Impostor.ImpostorPower;
using TownOfUs.Roles.Crewmate;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Mecánicas de reunión del Dictador:
///     - Acumula votos EN CONTRA entre reuniones (no se resetean),
///       excluyendo votos de sí mismo, aliados impostores y su Alliance.
///     - NUEVA mecánica de redirección (solo revelado): se redirigen COMO
///       MÁXIMO 2 votos ajenos hacia el objetivo que votó el propio Dictador
///       (su voto nunca se toca: ya apunta ahí). Fuentes, en este orden:
///       A) votos EN CONTRA del Dictador; B) si aún queda hueco, 1 voto de los
///       que van EN CONTRA de un aliado impostor.
/// </summary>
public static class DictadorMeetingEvents
{
    /// <summary>Máximo de votos AJENOS que el Dictador redirige (su voto no cuenta).</summary>
    public const int MaxVotosRedirigidos = 2;

    /// <summary>Id del voto de "skip" en el sistema de votos de MiraAPI (VotingUtils).</summary>
    private const byte VotoSkip = 253;
    /// <summary>
    ///     HOST, en el CÁLCULO de los votos (antes de decidir la expulsión y de
    ///     dibujar resultados): SOLO si el Dictador YA SE REVELÓ, se redirigen
    ///     como máximo 2 votos AJENOS hacia el objetivo del Dictador (el jugador
    ///     que él votó; skip si votó skip):
    ///     <para>· 1 voto en su contra → se redirige 1.</para>
    ///     <para>· 2 en su contra → se redirigen 2.</para>
    ///     <para>· 3 o más en su contra → solo se redirigen 2.</para>
    ///     <para>· 3 contra un aliado impostor + 3 contra el Dictador → 1 del
    ///     aliado + 1 del Dictador (total 2), más el voto del Dictador.</para>
    ///     El voto propio del Dictador SIEMPRE cuenta hacia su objetivo y NUNCA
    ///     se modifica. Al mutar la lista del evento el host recalcula la
    ///     expulsión y difunde los votos ya redirigidos a todos los clientes
    ///     (la pantalla de resultados muestra el reparto final).
    /// </summary>
    [RegisterEvent]
    public static void RedirigirVotosDictador(ProcessVotesEvent @event)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        var votos = @event?.Votes;
        if (votos == null || votos.Count == 0)
        {
            return;
        }

        // ABOGADO: si Terminar quedó activo en ESTA votación, el
        // Abogado tiene prioridad: la redirección de votos NO se ejecuta.
        if (AbogadoManager.TerminarActivo)
        {
            return;
        }

        var dictadores = CustomRoleUtils.GetActiveRolesOfType<DictadorRole>()
            .Where(d => d.Revealed && d.Player?.Data != null && !d.Player.Data.IsDead)
            .ToList();
        if (dictadores.Count == 0)
        {
            return;
        }

        foreach (var dictador in dictadores)
        {
            RedirigirDeDictador(votos, dictador.Player.PlayerId);
        }
    }

    /// <summary>
    ///     Redistribuye los votos de UN Dictador sobre la lista de votos de la
    ///     reunión (solo host, en el cálculo previo a la expulsión).
    /// </summary>
    private static void RedirigirDeDictador(List<CustomVote> votos, byte idDictador)
    {
        // 1) OBJETIVO = el suspect del voto PROPIO del Dictador (skip incluido).
        //    Sin voto propio no hay objetivo: no se redirige nada.
        var indicePropio = votos.FindIndex(v => v.Voter == idDictador);
        if (indicePropio < 0)
        {
            return;
        }

        var objetivo = votos[indicePropio].Suspect;
        if (objetivo == VotoSkip)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                "[Dictador] Votó skip: los votos redirigidos van a skip.");
        }

        // 2) FUENTE A: votos EN CONTRA del Dictador (sin contar el suyo).
        var indicesA = IndicesVotos(votos, v => v.Voter != idDictador && v.Suspect == idDictador);

        // 3) FUENTE B: votos EN CONTRA del aliado impostor más votado (el voto
        //    del propio aliado no cuenta y su objetivo nunca es él mismo).
        var aliado = AliadoMasVotado(votos, idDictador, objetivo);
        var indicesB = aliado == null
            ? new List<int>()
            : IndicesVotos(votos, v => v.Voter != idDictador && v.Suspect == aliado.Value);

        // 4) REPARTO (nunca más de 2 redirigidos): si el aliado tiene votos se
        //    le reserva 1 hueco (regla "1 del aliado + el resto del Dictador").
        var reservaAliado = indicesB.Count > 0 ? 1 : 0;
        var redirigidosA = Math.Min(indicesA.Count, MaxVotosRedirigidos - reservaAliado);
        var redirigidosB = Math.Min(indicesB.Count, MaxVotosRedirigidos - redirigidosA);

        Redirigir(votos, indicesA, redirigidosA, objetivo);
        Redirigir(votos, indicesB, redirigidosB, objetivo);
    }

    /// <summary>Índices (en orden de emisión) de los votos que cumplen el filtro.</summary>
    private static List<int> IndicesVotos(List<CustomVote> votos, Func<CustomVote, bool> filtro)
    {
        var indices = new List<int>();
        for (var i = 0; i < votos.Count; i++)
        {
            if (filtro(votos[i]))
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    /// <summary>
    ///     Aliado impostor (otro impostor, nunca el Dictador) con MÁS votos en
    ///     contra en esta reunión. Null si nadie votó a un aliado impostor (o si
    ///     el único "aliado" votado es el propio objetivo del Dictador, porque
    ///     redirigir esos votos hacia él mismo no cambiaría nada).
    /// </summary>
    private static byte? AliadoMasVotado(List<CustomVote> votos, byte idDictador, byte objetivo)
    {
        byte? mejor = null;
        var mejorCantidad = 0;

        var porJugador = votos
            .Where(v => v.Voter != idDictador && v.Suspect != idDictador)
            .GroupBy(v => v.Suspect);

        foreach (var grupo in porJugador)
        {
            var cantidad = grupo.Count();
            if (grupo.Key == objetivo || cantidad <= mejorCantidad)
            {
                continue;
            }

            var jugador = TownOfUs.Utilities.MiscUtils.PlayerById(grupo.Key);
            if (jugador?.Data == null || jugador.Data.IsDead || jugador.Data.Disconnected)
            {
                continue;
            }

            // "Aliado impostor": otro impostor del equipo.
            if (jugador.Data.Role?.IsImpostor != true)
            {
                continue;
            }

            mejor = grupo.Key;
            mejorCantidad = cantidad;
        }

        return mejor;
    }

    /// <summary>
    ///     Redirige los ÚLTIMOS <paramref name="cantidad"/> votos de la lista de
    ///     índices (los más recientes) hacia el objetivo del Dictador.
    /// </summary>
    private static void Redirigir(List<CustomVote> votos, List<int> indices, int cantidad, byte objetivo)
    {
        if (cantidad <= 0 || indices.Count == 0)
        {
            return;
        }

        for (var i = Math.Max(0, indices.Count - cantidad); i < indices.Count; i++)
        {
            var voto = votos[indices[i]];
            voto.Suspect = objetivo;
            votos[indices[i]] = voto;
        }
    }


    /// <summary>
    ///     Al terminar la votación, cuenta los votos EN CONTRA de cada Candidato
    ///     y los acumula (persisten entre reuniones). Cuentan votos de jugadores
    ///     que no sean impostores aliados ni su Alliance; SI EL DICTADOR SE VOTA
    ///     A SÍ MISMO, ese voto propio SÍ cuenta para revelarse.
    /// </summary>
    [RegisterEvent]
    public static void ContarVotosEnContra(VotingCompleteEvent _)
    {
        var dictadores = CustomRoleUtils.GetActiveRolesOfType<DictadorRole>().ToList();
        if (dictadores.Count == 0)
        {
            return;
        }

        var umbral = (int)OptionGroupSingleton<DictadorOptions>.Instance.VotosParaRevelar.Value;

        foreach (var dictador in dictadores)
        {
            if (dictador?.Player?.Data == null)
            {
                continue;
            }

            var votos = PlayerControl.AllPlayerControls.ToArray()
                .Where(p => p != null &&
                            (p.PlayerId == dictador.Player.PlayerId || !DictadorManager.EsAliado(p)))
                .Sum(p =>
                {
                    var vd = p.GetVoteData();
                    if (vd == null)
                    {
                        return 0;
                    }

                    return vd.Votes.Count(v => v.Suspect == dictador.Player.PlayerId);
                });

            dictador.VotosEnContra += votos;

            // Mostrar progreso siempre al candidato (hardcode ON).
            if (dictador.Player.AmOwner)
            {
                var texto =
                    $"<b>Votos en contra {dictador.VotosEnContra}/{umbral}</b>.";
                if (dictador.PuedeRevelarse)
                {
                    // MAYOR + DICTADOR: el desbloqueo también puede venir de que el
                    // Mayor se haya revelado (sin necesidad de votos en contra).
                    texto = DictadorManager.MayorRevelado
                        ? "<b>¡El Mayor se reveló! Ya puedes revelarte</b>"
                        : $"<b>¡Puedes revelarte {dictador.VotosEnContra}/{umbral}!</b>";
                }

                var title = $"<color=#{Palette.ImpostorRed.ToHtmlStringRGBA()}>Dictador</color>";
                try
                {
                    TownOfUs.Utilities.MiscUtils.AddFakeChat(dictador.Player.Data, title, texto, false, true);
                }
                catch
                {
                    // Sin chat no pasa nada.
                }
            }
        }
    }
}

/// <summary>
///     MAYOR + DICTADOR: cuando el Mayor (TOU Mira) se REVELA en una reunión, el
///     Dictador queda desbloqueado para revelarse SIN cumplir el requisito de
///     votos en contra: en esa misma reunión o en cualquier reunión posterior.
///
///     Se engancha el RPC REAL de la revelación del Mayor
///     (<c>MayorRole.RpcAnimateNewReveal</c>): al ser un RPC, el Postfix corre en
///     el remitente y en TODOS los clientes, así que el desbloqueo es global.
///     SOLO cuenta el Mayor de verdad: el Dictador revelado NO pasa por aquí
///     (usa su propio RPC y solo imita la animación del Mayor), y el resto de la
///     mecánica del Dictador (votos en contra, redirección, etc.) no se toca.
/// </summary>
[HarmonyPatch(typeof(MayorRole), nameof(MayorRole.RpcAnimateNewReveal))]
public static class DictadorMayorRevealPatch
{
    public static void Postfix(PlayerControl plr)
    {
        try
        {
            if (plr?.Data?.Role is MayorRole)
            {
                DictadorManager.MarcarMayorRevelado(plr);
            }
        }
        catch
        {
            // El desbloqueo nunca debe romper la reunión.
        }
    }
}