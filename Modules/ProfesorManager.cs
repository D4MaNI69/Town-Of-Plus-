using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using TownOfPlus.Options.Crewmate.CrewmatePower;
using TownOfPlus.Roles.Crewmate.CrewmatePower;
using TownOfUs.Modules;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Modules;

public static class ProfesorManager
{
    private static GameManager? _juegoActual;
    private static bool _profesorExistio;
    private static int _reentrenosHechos;
    private static readonly Dictionary<byte, int> TareasContadas = new();
    private static readonly Dictionary<byte, int> CargasPorProfesor = new();
    private static readonly Dictionary<byte, int> CooldownPorProfesor = new();
    private static readonly Dictionary<byte, HashSet<byte>> MarcadoPorProfesor = new();
    private static readonly Dictionary<byte, bool> PendienteCambio = new();
    private static readonly Dictionary<byte, List<RoleBehaviour>> OpcionesCambio = new();

    /// <summary>
    ///     AJUSTE PEDIDO 2 (botón "Cambiar Rol"): jugadores cuyo menú de cambio
    ///     de rol YA se CERRÓ (eligieran rol o cancelaran). El botón desaparece
    ///     y no vuelve a mostrarse esa ronda/partida para ese jugador. Se limpia
    ///     al cambiar de partida.
    /// </summary>
    private static readonly HashSet<byte> CambiosCerrados = new();

    /// <summary>
    ///     AJUSTE ANTI-RE-REENTRENO: jugadores YA reentrenados por el Profesor
    ///     en esta partida (imp, neutral o crew). Nunca pueden ser reentrenados
    ///     OTRA VEZ por el mismo u otro Profesor. Se limpia al cambiar de partida.
    /// </summary>
    private static readonly HashSet<byte> YaReentrenados = new();

    /// <summary>¿Este jugador ya fue reentrenado en esta partida?</summary>
    public static bool YaFueReentrenado(byte jugadorId) => YaReentrenados.Contains(jugadorId);

    public static bool ProfesorExistio => _profesorExistio;
    public static int ReentrenosHechos => _reentrenosHechos;

    /// <summary>
    ///     PEDIDO GLOBAL: la reunión en modo DISCUSIÓN bloquea los botones de
    ///     habilidad de TODOS los roles de Town Of Plus (Reentrenar, Revelar,
    ///     etc.). El chat, ajustes y salir siguen libres. Los botones vuelven
    ///     al pasar a VOTACIÓN (NotVoted/Voted) o al terminar la reunión.
    /// </summary>
    public static bool DiscusionActiva =>
        MeetingHud.Instance != null &&
        MeetingHud.Instance.state == MeetingHud.MeetingStates.Discussion;

    public static int MaxReentrenos =>
        (int)OptionGroupSingleton<ProfesorOptions>.Instance.MaxReentrenos.Value;

    public static int TareasPorCarga =>
        (int)OptionGroupSingleton<ProfesorOptions>.Instance.TareasParaReentrenar.Value;

    public static int CooldownBase =>
        (int)OptionGroupSingleton<ProfesorOptions>.Instance.RondasCooldown.Value;

    public static void ChequearNuevaPartida()
    {
        if (_juegoActual == GameManager.Instance)
        {
            return;
        }

        _juegoActual = GameManager.Instance;
        _profesorExistio = CustomRoleUtils.GetActiveRolesOfType<ProfesorRole>().Any();
        _reentrenosHechos = 0;
        TareasContadas.Clear();
        CargasPorProfesor.Clear();
        CooldownPorProfesor.Clear();
        MarcadoPorProfesor.Clear();
        PendienteCambio.Clear();
        OpcionesCambio.Clear();
        YaReentrenados.Clear();
        CambiosCerrados.Clear();
    }

    public static void ResetEntrePartidas()
    {
        _juegoActual = null;
        _profesorExistio = false;
        _reentrenosHechos = 0;
        TareasContadas.Clear();
        CargasPorProfesor.Clear();
        CooldownPorProfesor.Clear();
        MarcadoPorProfesor.Clear();
        PendienteCambio.Clear();
        OpcionesCambio.Clear();
        YaReentrenados.Clear();
        CambiosCerrados.Clear();
    }

    public static void AlInicializarProfesor(PlayerControl profesor)
    {
        ChequearNuevaPartida();
        _profesorExistio = true;
        if (profesor == null)
        {
            return;
        }

        CargasPorProfesor.TryAdd(profesor.PlayerId, 0);
        CooldownPorProfesor.TryAdd(profesor.PlayerId, 0);
        TareasContadas.TryAdd(profesor.PlayerId, 0);
    }

    public static int CargasDe(byte profesorId)
    {
        return CargasPorProfesor.TryGetValue(profesorId, out var c) ? c : 0;
    }

    public static int CooldownDe(byte profesorId)
    {
        return CooldownPorProfesor.TryGetValue(profesorId, out var c) ? c : 0;
    }

    public static bool PuedeMarcar(PlayerControl? profesor)
    {
        if (profesor == null || profesor.Data == null || profesor.Data.IsDead)
        {
            return false;
        }

        if (profesor.Data.Role is not ProfesorRole)
        {
            return false;
        }

        ChequearNuevaPartida();
        if (_reentrenosHechos >= MaxReentrenos)
        {
            return false;
        }

        if (CooldownDe(profesor.PlayerId) > 0)
        {
            return false;
        }

        return CargasDe(profesor.PlayerId) > 0;
    }

    public static void AlCompletarTarea(PlayerControl jugador)
    {
        if (jugador == null || jugador.Data?.Role is not ProfesorRole)
        {
            return;
        }

        ChequearNuevaPartida();
        var hechas = TareasContadas.TryGetValue(jugador.PlayerId, out var t) ? t : 0;
        hechas++;
        TareasContadas[jugador.PlayerId] = hechas;
        var cada = Mathf.Max(2, TareasPorCarga);
        if (hechas % cada != 0)
        {
            return;
        }

        CargasPorProfesor[jugador.PlayerId] = CargasDe(jugador.PlayerId) + 1;
        if (jugador.Data.Role is ProfesorRole rol && jugador.AmOwner)
        {
            rol.CargasDisponibles = CargasDe(jugador.PlayerId);
        }
    }

    public static void FijarMarcado(byte profesorId, byte targetId)
    {
        ChequearNuevaPartida();
        if (targetId == 255)
        {
            MarcadoPorProfesor.Remove(profesorId);
            return;
        }

        AniadirMarcadoReunion(profesorId, targetId);
    }

    /// <summary>
    ///     Marca a OTRO jugador objetivo en la reunión actual. Soporta
    ///     multi-marcado: un mismo Profesor puede marcar hasta sus cargas
    ///     disponibles en una sola reunión.
    /// </summary>
    public static void AniadirMarcadoReunion(byte profesorId, byte targetId)
    {
        var set = MarcadoPorProfesor.TryGetValue(profesorId, out var s) ? s : new HashSet<byte>();
        set.Add(targetId);
        MarcadoPorProfesor[profesorId] = set;
    }

    /// <summary>¿Este objetivo ya fue marcado por este Profesor en la reunión actual?</summary>
    public static bool MarcadoEstaReunion(byte profesorId, byte targetId)
    {
        return MarcadoPorProfesor.TryGetValue(profesorId, out var set) && set.Contains(targetId);
    }

    /// <summary>Cuántos jugadores ha marcado este Profesor en la reunión actual.</summary>
    public static int MarcadosEnReunion(byte profesorId)
    {
        return MarcadoPorProfesor.TryGetValue(profesorId, out var set) ? set.Count : 0;
    }

    public static void AlTerminarReunion()
    {
        ChequearNuevaPartida();
        if (!AmongUsClient.Instance.AmHost)
        {
            return;
        }

        foreach (var kv in MarcadoPorProfesor.ToList())
        {
            MarcadoPorProfesor.Remove(kv.Key);
            var profesor = TownOfUs.Utilities.MiscUtils.PlayerById(kv.Key);
            if (profesor == null)
            {
                continue;
            }

            foreach (var targetId in kv.Value)
            {
                var target = TownOfUs.Utilities.MiscUtils.PlayerById(targetId);
                if (target == null)
                {
                    continue;
                }

                // Solo mientras el Profesor siga siéndolo y le queden cargas.
                if (profesor.Data == null || profesor.Data.Role is not ProfesorRole)
                {
                    break;
                }

                if (CargasDe(profesor.PlayerId) <= 0)
                {
                    break;
                }

                // Tope global de reentrenos de la partida.
                if (_reentrenosHechos >= MaxReentrenos)
                {
                    break;
                }

                if (target.Data == null || target.Data.IsDead || target.Data.Disconnected)
                {
                    continue;
                }

                // AJUSTE ANTI-RE-REENTRENO: un jugador ya reentrenado en esta
                // partida (imp, neutral o crew) NO puede ser reentrenado otra vez.
                if (YaFueReentrenado(target.PlayerId))
                {
                    TownOfPlusPlugin.Instance?.Log.LogInfo(
                        $"[Profesor] {target.Data?.PlayerName} ya fue reentrenado esta partida: reentreno cancelado.");
                    continue;
                }

                CargasPorProfesor[profesor.PlayerId] = Mathf.Max(0, CargasDe(profesor.PlayerId) - 1);
                _reentrenosHechos++;
                CooldownPorProfesor[profesor.PlayerId] = CooldownBase;
                YaReentrenados.Add(target.PlayerId);

                if (!target.IsCrewmate())
                {
                    Networking.ProfesorRpc.EnviarAvisoFallido(target);
                    continue;
                }

                var auto = target.PlayerId == profesor.PlayerId;
                var opciones = CambioRolProfesor.ConstruirOpciones(target, auto);
                if (opciones.Count == 0)
                {
                    continue;
                }

                OpcionesCambio[target.PlayerId] = opciones;
                PendienteCambio[target.PlayerId] = true;
                Networking.ProfesorRpc.EnviarOpciones(target, opciones);
            }
        }
    }

    public static void AlEmpezarRonda()
    {
        ChequearNuevaPartida();
        foreach (var id in CooldownPorProfesor.Keys.ToList())
        {
            if (CooldownPorProfesor[id] > 0)
            {
                CooldownPorProfesor[id]--;
            }
        }

        foreach (var kv in OpcionesCambio.ToList())
        {
            var jugador = TownOfUs.Utilities.MiscUtils.PlayerById(kv.Key);
            if (jugador == null || jugador.Data == null || jugador.Data.IsDead)
            {
                OpcionesCambio.Remove(kv.Key);
                PendienteCambio.Remove(kv.Key);
            }
        }
    }

    public static bool ConsumirCambio(byte jugadorId, ushort roleId)
    {
        if (!OpcionesCambio.TryGetValue(jugadorId, out var ops))
        {
            return false;
        }

        var elegido = ops.FirstOrDefault(r => r != null && (ushort)r.Role == roleId);
        if (elegido == null)
        {
            return false;
        }

        OpcionesCambio.Remove(jugadorId);
        PendienteCambio.Remove(jugadorId);

        // El cambio se aplicó (rol elegido): el botón ya no debe volver.
        CambiosCerrados.Add(jugadorId);
        return true;
    }

    public static void GuardarOpciones(byte jugadorId, List<RoleBehaviour> opciones)
    {
        // Si el jugador ya cerró su menú de cambio, no se le vuelve a ofrecer:
        // el botón "Cambiar Rol" debe quedarse oculto el resto de la partida.
        if (CambiosCerrados.Contains(jugadorId))
        {
            return;
        }

        OpcionesCambio[jugadorId] = opciones.ToList();
        PendienteCambio[jugadorId] = true;
    }

    public static bool TieneCambioPendiente(byte jugadorId)
    {
        if (CambiosCerrados.Contains(jugadorId))
        {
            return false;
        }

        return PendienteCambio.TryGetValue(jugadorId, out var p) && p &&
               OpcionesCambio.TryGetValue(jugadorId, out var ops) && ops.Count > 0;
    }

    /// <summary>
    ///     PEDIDO 2 (PROFESOR): el jugador CERRÓ su interfaz de "Cambiar Rol"
    ///     (eligió un rol o la canceló). El cambio queda RESUELTO: el botón se
    ///     oculta y no vuelve a mostrarse en lo que resta de partida.
    /// </summary>
    public static void MarcarCambioResuelto(byte jugadorId)
    {
        CambiosCerrados.Add(jugadorId);
        PendienteCambio[jugadorId] = false;
        OpcionesCambio.Remove(jugadorId);
    }

    public static List<RoleBehaviour> OpcionesDe(byte jugadorId)
    {
        return OpcionesCambio.TryGetValue(jugadorId, out var ops) ? ops.ToList() : new List<RoleBehaviour>();
    }

    public static bool EsProfesor(PlayerControl? jugador)
    {
        if (jugador?.Data == null)
        {
            return false;
        }

        try
        {
            return jugador.Data.Role is ProfesorRole ||
                   jugador.GetRoleWhenAlive() is ProfesorRole;
        }
        catch
        {
            return jugador.Data.Role is ProfesorRole;
        }
    }
}
