using System;
using System.Collections;
using System.Collections.Generic;
using MiraAPI.GameOptions;
using Reactor.Utilities;
using TownOfPlus.Options.Crewmate.CrewmateInvestigative;
using TownOfPlus.Roles.Crewmate.CrewmateInvestigative;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Modules;

/// <summary>Fases del Ritualista.</summary>
public enum FaseRitual
{
    /// <summary> Marcando jugadores con el botón Ritual.</summary>
    Marcando,

    /// <summary>Revelación hecha: puede Sacrificar.</summary>
    Sacrificando,

    /// <summary>Sin tiros o rol consumido.</summary>
    Terminado,
}

/// <summary>
///     Estado del Ritualista (solo cliente local): marcas de objetivos,
///     revelación de facciones en la reunión y tiros de Sacrificar.
/// </summary>
public static class RitualistaManager
{
    private static readonly List<byte> Marcados = [];

    /// <summary>Fase actual del Ritualista local.</summary>
    public static FaseRitual Fase { get; private set; } = FaseRitual.Terminado;

    /// <summary>Tiros de Sacrificar disponibles.</summary>
    public static int TirosRestantes { get; private set; }

    /// <summary>Máximo de jugadores a marcar (opción de partida).</summary>
    public static int MaxMarcas =>
        (int)OptionGroupSingleton<RitualistaOptions>.Instance.JugadoresAMarcar.Value;

    public static int CantidadMarcados => Marcados.Count;

    public static bool MarcasCompletas => CantidadMarcados >= MaxMarcas;

    /// <summary>¿El jugador local es un Ritualista vivo?</summary>
    public static bool EsLocalRitualista
    {
        get
        {
            var local = PlayerControl.LocalPlayer;
            return local != null && local.Data != null && !local.Data.IsDead &&
                   local.Data.Role is RitualistaRole;
        }
    }

    public static bool EstaMarcado(byte id) => Marcados.Contains(id);

    /// <summary>Nueva partida: fase de marcado desde cero.</summary>
    public static void Reiniciar()
    {
        Marcados.Clear();
        ObjetivosNotificados.Clear();
        TirosRestantes = 0;
        Fase = FaseRitual.Marcando;
    }

    /// <summary>El Ritualista murió o perdió el rol.</summary>
    public static void Salir()
    {
        Marcados.Clear();
        ObjetivosNotificados.Clear();
        TirosRestantes = 0;
        Fase = FaseRitual.Terminado;
    }

    /// <summary>Marca a un objetivo. Devuelve false si no es válido.</summary>
    public static bool Marcar(PlayerControl objetivo)
    {
        if (Fase != FaseRitual.Marcando || MarcasCompletas || objetivo?.Data == null)
        {
            return false;
        }

        if (Marcados.Contains(objetivo.PlayerId))
        {
            return false;
        }

        Marcados.Add(objetivo.PlayerId);
        return true;
    }

    /// <summary>Texto cabecera del mensaje de revelación (para detectar la burbuja).</summary>
    public const string TextoRevelacion = "Tus objetivos son:";

    /// <summary>¿Es impostor o neutral? (los "malos" del ritual).</summary>
    public static bool EsImpostorONeutral(PlayerControl? p)
    {
        if (p?.Data?.Role == null)
        {
            return false;
        }

        if (p.IsImpostorAligned())
        {
            return true;
        }

        return p.Data.Role is ITownOfUsRole tou &&
               tou.RoleAlignment.ToString().StartsWith("Neutral", StringComparison.Ordinal);
    }

    private static string FacccionDe(PlayerControl p)
    {
        if (p.IsImpostorAligned())
        {
            return "Impostor";
        }

        if (p.Data?.Role is ITownOfUsRole tou &&
            tou.RoleAlignment.ToString().StartsWith("Neutral", StringComparison.Ordinal))
        {
            return "Neutral";
        }

        return "Tripulante";
    }

    private static PlayerControl? DeId(byte id)
    {
        for (var i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
        {
            var p = PlayerControl.AllPlayerControls[i];
            if (p != null && p.PlayerId == id)
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>
    ///     Al empezar una reunión con las marcas completas: revela en el chat
    ///     (solo para el Ritualista) las facciones de sus objetivos en orden
    ///     aleatorio, sin decir cuál es cuál, y otorga los tiros de Sacrificar.
    /// </summary>
    public static void AlIniciarReunion()
    {
        if (!EsLocalRitualista || Fase != FaseRitual.Marcando || !MarcasCompletas)
        {
            return;
        }

        var facciones = new List<string>();
        var malos = 0;
        foreach (var id in Marcados)
        {
            var p = DeId(id);
            if (p == null)
            {
                continue;
            }

            var faccion = FacccionDe(p);
            facciones.Add(faccion);
            if (faccion != "Tripulante")
            {
                malos++;
            }
        }

        // Orden aleatorio: no se revela cuál marca es cuál.
        for (var i = facciones.Count - 1; i > 0; i--)
        {
            var j = UnityEngine.Random.Range(0, i + 1);
            (facciones[i], facciones[j]) = (facciones[j], facciones[i]);
        }

        // Cabecera en negro y cada facción con SU color oficial:
        // Impostor = rojo, Neutral = gris, Tripulante = azul claro.
        var hexImpostor = "#" + ColorUtility.ToHtmlStringRGB(Palette.ImpostorRed);
        var hexNeutral = "#" + ColorUtility.ToHtmlStringRGB(Color.gray);
        var hexTripulante = "#" + ColorUtility.ToHtmlStringRGB(Palette.CrewmateRoleBlue);

        var texto = $"<color=#000000>{TextoRevelacion}</color>";
        foreach (var faccion in facciones)
        {
            var hex = faccion switch
            {
                "Impostor" => hexImpostor,
                "Neutral" => hexNeutral,
                _ => hexTripulante,
            };
            texto += $"\n<color={hex}>{faccion}</color>";
        }

        TirosRestantes = malos;
        Fase = malos > 0 ? FaseRitual.Sacrificando : FaseRitual.Terminado;

        Coroutines.Start(MostrarChat(texto));
    }

    private static IEnumerator MostrarChat(string texto)
    {
        yield return new WaitForSeconds(0.75f);

        var chat = HudManager.Instance?.Chat;
        if (chat == null || PlayerControl.LocalPlayer == null)
        {
            yield break;
        }

        chat.AddChat(PlayerControl.LocalPlayer, texto);
    }

    /// <summary>Consume un tiro de Sacrificar.</summary>
    public static void ConsumirTiro()
    {
        TirosRestantes = Mathf.Max(0, TirosRestantes - 1);
        if (TirosRestantes == 0)
        {
            Fase = FaseRitual.Terminado;
        }
    }

    /// <summary>
    ///     Consume el tiro de un SACRIFICIO REALIZADO: el objetivo muere por
    ///     el propio Sacrificar, así que se marca como "notificado" para que
    ///     ComprobarObjetivosMuertos NO consuma un segundo tiro por la misma
    ///     muerte (bug: cada sacrificio gastaba 2 cargos).
    /// </summary>
    public static void ConsumirSacrificio(byte objetivoId)
    {
        ObjetivosNotificados.Add(objetivoId);
        ConsumirTiro();
    }


    // ------------------------------------------------------------------
    //  Muerte / expulsión de objetivos marcados
    // ------------------------------------------------------------------

    private static readonly HashSet<byte> ObjetivosNotificados = [];

    /// <summary>Cabecera del aviso cuando matan/expulsan a un objetivo.</summary>
    public const string TextoMuerteObjetivo = "Mataron a tu objetivo:";

    /// <summary>
    ///     Comprueba cada frame si algún objetivo marcado murió o fue expulsado.
    ///     Al detectarlo: resta un tiro de Sacrificar y avisa por chat con la
    ///     facción del objetivo en su color oficial.
    /// </summary>
    public static void ComprobarObjetivosMuertos()
    {
        if (!EsLocalRitualista || Marcados.Count == 0)
        {
            return;
        }

        foreach (var id in Marcados)
        {
            if (ObjetivosNotificados.Contains(id))
            {
                continue;
            }

            var p = DeId(id);
            if (p == null || p.Data == null)
            {
                continue; // Desconectado o aún sin datos: no avisar.
            }

            if (!p.Data.IsDead)
            {
                continue;
            }

            ObjetivosNotificados.Add(id);

            var faccion = FacccionDe(p);
            var hex = faccion switch
            {
                "Impostor" => "#" + ColorUtility.ToHtmlStringRGB(Palette.ImpostorRed),
                "Neutral" => "#" + ColorUtility.ToHtmlStringRGB(Color.gray),
                _ => "#" + ColorUtility.ToHtmlStringRGB(Palette.CrewmateRoleBlue),
            };

            Coroutines.Start(MostrarChat(
                $"<color=#000000>{TextoMuerteObjetivo}</color> <color={hex}>{faccion}</color>"));

            // Su tiro solo se pierde si la víctima era impostor/neutral: los
            // usos de Sacrificar = las VÍCTIMAS de la lista (imps+neutrals).
            // La muerte de un tripulante marcado no consume cargo.
            if (TirosRestantes > 0 && faccion != "Tripulante")
            {
                ConsumirTiro();
            }
        }
    }
}