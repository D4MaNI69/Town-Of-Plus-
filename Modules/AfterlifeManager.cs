using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using Reactor.Utilities;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using TownOfPlus.Roles.Impostor.ImpostorAfterlife;
using TownOfPlus.Roles.Impostor.ImpostorConcealing;
using TownOfPlus.Roles.Neutral.NeutralAfterlife;
using TownOfPlus.Options.Impostor.ImpostorConcealing;
using TownOfUs.Modules;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Modules;

/// <summary>
///     Conversión a roles Afterlife estilo Haunter/Spectre de TOU Mira:
///     los roles afterlife NUNCA se asignan al inicio ni al morir (el patch
///     AfterlifeGhostRolePatch los excluye del pool inicial). Al terminar la
///     reunión (ExileController.WrapUp), SOLO el host filtra a los muertos
///     elegibles, elige uno al azar y lo convierte por RPC. La cantidad
///     (# DE ROL) y la probabilidad (%) salen de los sliders vanilla del rol,
///     leídos con GetAssignData, igual que hace TOU Mira con Haunter/Spectre.
/// </summary>
public static class AfterlifeManager
{
    private static GameManager? _juegoActual;

    /// <summary>
    ///     Anti-doble-disparo: AlWrapUp llega por DOS caminos (patch de
    ///     ExileController.WrapUp Y EjectionEvent de MiraAPI, que ocurren casi
    ///     a la vez). Solo se permite UN arranque de conversión por reunión.
    /// </summary>
    private static float _ultimoLanzamiento = -999f;

    /// <summary>Exiliado de la reunión en curso (NUNCA es candidato afterlife:
    /// convertirlo justo tras su expulsión puede interferir con roles que
    /// ganan al ser expulsados, como Jester o el Kamikaze detonado).</summary>
    private static PlayerControl? _exiliadoActual;

    /// <summary>
    ///     Jugadores cuyo arco de victoria especial YA TERMINÓ (la completaron
    ///     y ganaron). NUNCA reciben afterlife: su historia ya se cerró con su
    ///     victoria (Kamikaze detonado, Jester expulsado, ...). Se limpia al
    ///     empezar cada partida.
    /// </summary>
    private static readonly HashSet<int> ArcosTerminados = new();

    /// <summary>
    ///     REGLA DE LA AMALGAMA: SOLO puede existir UNA por partida. Este flag
    ///     se pone a true en cuanto se designa la primera Amalgama y no se
    ///     reinicia aunque esta muera o reviva: si ya existió UNA Amalgama,
    ///     ya no se designa ninguna más en toda la partida.
    /// </summary>
    private static bool _amalgamaAsignada;

    /// <summary>¿Ya se designó la única Amalgama de esta partida?</summary>
    public static bool AmalgamaAsignada => _amalgamaAsignada;

    /// <summary>
    ///     Hook del WrapUp (fin de reunión). Arranca la conversión tras un
    ///     pequeño margen, igual que CoSetGhostwalkers en TOU Mira.
    /// </summary>
    public static void AlWrapUp()
    {
        // Nueva partida detectada: reiniciar el contador por partida.
        if (_juegoActual != GameManager.Instance)
        {
            _juegoActual = GameManager.Instance;
            ArcosTerminados.Clear();
            _amalgamaAsignada = false;
        }

        // Anti-doble-disparo: el WrapUp Y el EjectionEvent llegan casi a la
        // vez; solo el primero lanza la conversión (el segundo se ignora).
        if (Time.time - _ultimoLanzamiento < 5f)
        {
            return;
        }

        _ultimoLanzamiento = Time.time;

        // REGLA AFTERLIFE vs ROLES QUE GANAN POR EXPULSIÓN: si el exiliado de
        // esta reunión completó su victoria especial (Kamikaze en la reunión
        // de detonación, Jester, ...), su arco terminó y NO recibe afterlife.
        _exiliadoActual = ExileController.Instance?.initData?.networkedPlayer?.Object;
        RegistrarArcoTerminadoDelExiliado();

        Coroutines.Start(CoConvertirAfterlife());
    }

    /// <summary>
    ///     REGLA AFTERLIFE vs ROLES QUE GANAN POR EXPULSIÓN: si el exiliado
    ///     de ESTA reunión completa su victoria especial (Kamikaze expulsado
    ///     en la reunión de detonación; Jester y cualquier rol exile-win),
    ///     su arco terminó → se marca para NO darle afterlife.
    /// </summary>
    private static void RegistrarArcoTerminadoDelExiliado()
    {
        try
        {
            var exiliado = ExileController.Instance?.initData?.networkedPlayer?.Object;
            if (exiliado == null || exiliado.Data == null)
            {
                return;
            }

            var rol = exiliado.GetRoleWhenAlive();
            if (rol == null)
            {
                return;
            }

            // Kamikaze: SOLO gana si es expulsado en la reunión de detonación.
            // Muerto en ronda o expulsado antes de esa reunión = falló →
            // SÍ puede recibir afterlife. Expulsado EN la reunión objetivo =
            // ganó / termina su arco → NO afterlife.
            if (rol is KamikazeRole)
            {
                var objetivo = (int)OptionGroupSingleton<KamikazeOptions>
                    .Instance.ReunionDetonacion.Value;

                if (KamikazeManager.DetonacionActivada ||
                    KamikazeManager.ContadorReuniones == objetivo)
                {
                    ArcosTerminados.Add(exiliado.PlayerId);
                    TownOfPlusPlugin.Instance?.Log.LogInfo(
                        $"[Afterlife] {exiliado.Data?.PlayerName} (Kamikaze) completó su arco con la expulsión → sin afterlife.");
                }

                return;
            }

            // Jester y cualquier rol exile-win (TOU Mira o compatible):
            // ser expulsado ES su victoria → arco terminado → no afterlife.
            if (EsRolQueGanaPorExiliacion(rol))
            {
                ArcosTerminados.Add(exiliado.PlayerId);
                TownOfPlusPlugin.Instance?.Log.LogInfo(
                    $"[Afterlife] {exiliado.Data?.PlayerName} ({rol.GetType().Name}) ganó al ser expulsado → sin afterlife.");
            }
        }
        catch
        {
            // Nunca romper el WrapUp por la comprobación de arcos.
        }
    }

    /// <summary>
    ///     ¿Gana este rol al ser expulsado? Lista extensible de roles
    ///     "exile-win": el Jester de TOU Mira (y variantes compatibles) es
    ///     el caso clásico. Se detecta por tipo/nombre para cubrir también
    ///     roles exile-win de otros mods TOU compatibles.
    /// </summary>
    public static bool EsRolQueGanaPorExiliacion(RoleBehaviour? rol)
    {
        if (rol == null)
        {
            return false;
        }

        // Jester (cualquier variante: TownOfUs, TOU Mira, ...).
        var nombreTipo = rol.GetType().Name;
        if (nombreTipo.Contains("Jester"))
        {
            return true;
        }

        if (rol is ITownOfUsRole touRol && touRol.RoleName == "Jester")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     ¿Es este un rol que gana al ser expulsado (Jester) o tiene una
    ///     victoria por expulsión (Kamikaze)? Están EXCLUIDOS DE FORMA
    ///     DEFINITIVA de toda asignación afterlife (de Town Of Plus Y de
    ///     TOU Mira: Haunter/Spectre) para que su victoria al ser exiliados
    ///     nunca se pierda.
    /// </summary>
    public static bool EsRolDeExpulsion(RoleBehaviour? rol)
    {
        if (rol == null)
        {
            return false;
        }

        if (rol is KamikazeRole)
        {
            return true;
        }

        var nombreTipo = rol.GetType().Name;
        if (nombreTipo.Contains("Jester"))
        {
            return true;
        }

        return rol is ITownOfUsRole touRol && touRol.RoleName == "Jester";
    }

    /// <summary>
    ///     ¿Puede este jugador MUERTO recibir un afterlife de Town of plus+?
    ///     REGLA: roles con victoria por expulsión/condición especial
    ///     (Kamikaze, Jester, ...) SOLO la reciben si YA FALLARON su
    ///     condición y quedan como fantasma "sin win path":
    ///     - Kamikaze: muerto o expulsado ANTES de la reunión de detonación
    ///       → SÍ afterlife. Expulsado EN la reunión de detonación → NO
    ///       (gana / termina su arco).
    ///     - Jester: matado en ronda (ya no puede ser expulsado) → SÍ
    ///       afterlife. Expulsado (gana como Jester) → NO.
    ///     No rompe el afterlife normal de crew/impostores elegibles.
    /// </summary>
    public static bool PuedeRecibirAfterlife(PlayerControl? jugador)
    {
        if (jugador?.Data == null)
        {
            return false;
        }

        // Ya completó su arco de victoria especial → nunca afterlife.
        if (ArcosTerminados.Contains(jugador.PlayerId))
        {
            return false;
        }

        try
        {
            var rol = jugador.GetRoleWhenAlive();

            if (rol is KamikazeRole)
            {
                // Si la detonación está activada (lo expulsaron en su reunión
                // de detonación), su victoria se está disparando → NO afterlife.
                // Cualquier otra muerte implica que falló → SÍ afterlife.
                return !KamikazeManager.DetonacionActivada;
            }
        }
        catch
        {
            // Ante cualquier error de interop, no bloquear el afterlife.
        }

        return true;
    }

    private static IEnumerator CoConvertirAfterlife()
    {
        // ── 1) DELAY / BLOQUEO TOTAL (3,5 SEGUNDOS) ─────────────────────────
        // La reunión termina → animación de expulsión → ESPERAR 3,5 SEGUNDOS.
        // Durante ese tiempo TODO lo afterlife está desactivado: no se buscan
        // candidatos ni se convierte a nadie, dando margen sobrado a que las
        // victorias por expulsión (Jester, Kamikaze detonado, ...) se
        // resuelvan primero.
        yield return new WaitForSeconds(1f);

        if (!AmongUsClient.Instance.AmHost || GameManager.Instance == null)
        {
            yield break;
        }

        if (LobbyBehaviour.Instance)
        {
            yield break;
        }

        // ── 2) PUERTA: "¿GANÓ ALGÚN ROL?" ───────────────────────────────────
        // Antes de decidir nada, esperar a que la expulsión y CUALQUIER
        // victoria por expulsión (Jester, Kamikaze en su reunión de
        // detonación, ...) se resuelvan COMPLETAMENTE. Mientras haya reunión
        // o animación de exile activa, los afterlife siguen bloqueados.
        var esperaPuerta = 0f;
        while (MeetingHud.Instance != null || ExileController.Instance != null)
        {
            esperaPuerta += Time.deltaTime;
            if (esperaPuerta > 15f)
            {
                break; // seguridad: nunca colgarnos en bucle infinito.
            }

            yield return null;
        }

        // PREGUNTA CENTRAL: ¿el juego sigue en curso?
        // - SI UN ROL GANÓ (Jester expulsado, Kamikaze detonado, etc.): el
        //   juego YA TERMINÓ → la partida acaba y JAMÁS se ejecutan los
        //   roles afterlife.
        // - SI EL JUEGO SIGUE: nadie ganó por expulsión → ahora sí se puede
        //   asignar afterlife a los elegibles.
        if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                "[Afterlife] Un rol ganó durante la espera → partida terminada, afterlife cancelado.");
            yield break;
        }

        // Seguridad extra: detonación del Kamikaze activándose (su victoria
        // se dispara al ser expulsado en la reunión de detonación) → nunca
        // convertir a nadie: la victoria debe resolverse primero.
        if (KamikazeManager.DetonacionActivada)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                "[Afterlife] Detonación del Kamikaze en curso → sin afterlife.");
            yield break;
        }

        // ── 3) EL JUEGO SIGUE → ASIGNAR AFTERLIFE ───────────────────────────
        // IMPORTANTE: cada rol afterlife intenta su conversión de forma
        // INDEPENDIENTE (antes, un yield break de la sección del Cerrajero
        // abortaba la corrutina entera y el Saboteador nunca llegaba a
        // evaluarse si la tirada del Cerrajero fallaba). La compuerta se
        // re-verifica antes de cada conversión por si el juego acaba en medio.
        yield return CoIntentarCerrajero();

        if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started ||
            KamikazeManager.DetonacionActivada)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                "[Afterlife] El juego terminó durante la conversión → se cancela el resto de afterlife.");
            yield break;
        }

        yield return CoIntentarHuesped();

        if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started ||
            KamikazeManager.DetonacionActivada)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                "[Afterlife] El juego terminó durante la conversión → se cancela el resto de afterlife.");
            yield break;
        }

        yield return CoIntentarSaboteador();

        if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started ||
            KamikazeManager.DetonacionActivada)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                "[Afterlife] El juego terminó durante la conversión → se cancela el resto de afterlife.");
            yield break;
        }

        yield return CoIntentarAmalgama();
    }

    /// <summary>
    ///     AMALGAMA (afterlife NEUTRAL): mismo patrón que Cerrajero/Saboteador
    ///     pero SIN filtro de facción — cualquier muerto elegible (crew,
    ///     impostor o neutral cuando vivo) puede convertirse en Amalgama.
    /// </summary>
    private static IEnumerator CoIntentarAmalgama()
    {
        // REGLA DE LA AMALGAMA: SOLO UNA por partida. Si ya SE DESIGNÓ una
        // (esté viva, muerta o haya revivido), no se designa ninguna más.
        if (_amalgamaAsignada)
        {
            yield break;
        }

        var data = CustomRoleUtils.GetAssignData((RoleTypes)RoleId.Get<AmalgamaRole>());

        if (data.Count <= 0)
        {
            yield break;
        }

        var activos = CustomRoleUtils.GetActiveRoles().OfType<AmalgamaRole>().Count();
        if (activos >= data.Count)
        {
            // Ya existe una Amalgama activa → no designar otra.
            yield break;
        }

        if (data.Chance < 100 && HashRandom.Next(101) > data.Chance)
        {
            yield break;
        }

        var candidatos = PlayerControl.AllPlayerControls.ToArray().Where(x =>
            x != null && x.Data != null && !x.Data.Disconnected &&
            x.Data.IsDead &&
            x != _exiliadoActual &&
            !EsRolDeExpulsion(x.GetRoleWhenAlive()) &&
            x.Data.Role is not AmalgamaRole &&
            x.CanGetGhostRole() &&
            PuedeRecibirAfterlife(x)).ToList();

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Amalgama] WrapUp: {candidatos.Count} muertos elegibles (cualquier facción).");

        if (candidatos.Count == 0)
        {
            yield break;
        }

        for (var i = candidatos.Count - 1; i > 0; i--)
        {
            var j = HashRandom.Next(i + 1);
            (candidatos[i], candidatos[j]) = (candidatos[j], candidatos[i]);
        }

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Amalgama] WrapUp: convirtiendo a {candidatos[0].Data?.PlayerName} en Amalgama.");

        // Se designó la única Amalgama de la partida: aunque luego muera o
        // reviva, ya no se designa ninguna otra.
        _amalgamaAsignada = true;

        ConvertirAmalgama(candidatos[0]);
    }

    private static IEnumerator CoIntentarCerrajero()
    {
        // Leer los sliders vanilla del rol: # DE ROL (Count) y PROBABILIDAD % (Chance).
        var data = CustomRoleUtils.GetAssignData((RoleTypes)RoleId.Get<CerrajeroRole>());

        // 1. Desactivado si el slider # DE ROL está en 0, o ya hay tantos
        //    afterlife convertidos como permite el slider # DE ROL.
        if (data.Count <= 0)
        {
            yield break;
        }

        var activos = CustomRoleUtils.GetActiveRoles().OfType<CerrajeroRole>().Count();
        if (activos >= data.Count)
        {
            yield break;
        }

        // 2-3. Probabilidad del slider PROBABILIDAD % (patrón TOU Mira: HashRandom).
        var chance = data.Chance;
        if (chance < 100 && HashRandom.Next(101) > chance)
        {
            yield break;
        }

        // Filtros de elegibilidad (mismo patrón robusto que el Saboteador/TOU Mira):
        // - muerto (IsDead; el exiliado ya está muerto en el WrapUp)
        // - no desconectado
        // - facción tripulante cuando VIVO (el rol actual de un muerto ya es
        //   su fantasma vanilla, por eso se usa GetRoleWhenAlive)
        // - que no sea ya un afterlife (CanGetGhostRole)
        var candidatos = PlayerControl.AllPlayerControls.ToArray().Where(x =>
            x != null && x.Data != null && !x.Data.Disconnected &&
            x.Data.IsDead &&
            x != _exiliadoActual &&
            !EsRolDeExpulsion(x.GetRoleWhenAlive()) &&
            x.Data.Role is not CerrajeroRole &&
            x.CanGetGhostRole() &&
            // REGLA afterlife: Kamikaze/Jester (y roles exile-win similares)
            // solo reciben afterlife si YA FALLARON su condición de victoria.
            PuedeRecibirAfterlife(x) &&
            EsTripulanteCuandoVivo(x)).ToList();

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Cerrajero] WrapUp: {candidatos.Count} tripulantes muertos elegibles.");

        if (candidatos.Count == 0)
        {
            yield break;
        }

        // Barajado aleatorio y elección de 1 (patrón Shuffle + TakeFirst).
        for (var i = candidatos.Count - 1; i > 0; i--)
        {
            var j = HashRandom.Next(i + 1);
            (candidatos[i], candidatos[j]) = (candidatos[j], candidatos[i]);
        }

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Cerrajero] WrapUp: convirtiendo a {candidatos[0].Data?.PlayerName} en Cerrajero.");

        ConvertirAfterlife(candidatos[0]);
    }

    /// <summary>
    ///     HUÉSPED (afterlife crew): SÓLO para tripulantes muertos (cuando
    ///     vivos). Mismo patrón que el Cerrajero: filtra con GetRoleWhenAlive,
    ///     CanGetGhostRole y PuedeRecibirAfterlife, baraja y elige 1.
    /// </summary>
    private static IEnumerator CoIntentarHuesped()
    {
        // Leer los sliders vanilla del rol: # DE ROL (Count) y PROBABILIDAD % (Chance).
        var data = CustomRoleUtils.GetAssignData((RoleTypes)RoleId.Get<HuespedRole>());

        // 1. Desactivado si el slider # DE ROL está en 0, o ya hay tantos
        //    Huéspedes convertidos como permite el slider # DE ROL.
        if (data.Count <= 0)
        {
            yield break;
        }

        var activos = CustomRoleUtils.GetActiveRoles().OfType<HuespedRole>().Count();
        if (activos >= data.Count)
        {
            yield break;
        }

        // 2-3. Probabilidad del slider PROBABILIDAD % (patrón TOU Mira: HashRandom).
        var chance = data.Chance;
        if (chance < 100 && HashRandom.Next(101) > chance)
        {
            yield break;
        }

        // Filtros de elegibilidad (mismo patrón robusto que el Cerrajero/TOU Mira):
        // - muerto (IsDead; el exiliado ya está muerto en el WrapUp)
        // - no desconectado
        // - facción tripulante cuando VIVO (GetRoleWhenAlive)
        // - que no sea ya un afterlife (CanGetGhostRole)
        var candidatos = PlayerControl.AllPlayerControls.ToArray().Where(x =>
            x != null && x.Data != null && !x.Data.Disconnected &&
            x.Data.IsDead &&
            x != _exiliadoActual &&
            !EsRolDeExpulsion(x.GetRoleWhenAlive()) &&
            x.Data.Role is not HuespedRole &&
            x.Data.Role is not CerrajeroRole &&
            x.CanGetGhostRole() &&
            PuedeRecibirAfterlife(x) &&
            EsTripulanteCuandoVivo(x)).ToList();

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Huésped] WrapUp: {candidatos.Count} tripulantes muertos elegibles.");

        if (candidatos.Count == 0)
        {
            yield break;
        }

        // Barajado aleatorio y elección de 1 (patrón Shuffle + TakeFirst).
        for (var i = candidatos.Count - 1; i > 0; i--)
        {
            var j = HashRandom.Next(i + 1);
            (candidatos[i], candidatos[j]) = (candidatos[j], candidatos[i]);
        }

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Huesped] WrapUp: convirtiendo a {candidatos[0].Data?.PlayerName} en Huésped.");

        ConvertirHuesped(candidatos[0]);
    }

    private static IEnumerator CoIntentarSaboteador()
    {
        // ── SABOTEADOR (afterlife impostor) ─────────────────────────────────
        // MECÁNICA DE TOU MIRA (GhostRoleEvents.CoSetGhostwalkers): filtrar con
        // GetRoleWhenAlive() + CanGetGhostRole() (NO con Data.Role, que en los
        // muertos ya es ImpostorGhost) y convertir con RpcChangeRole.
        // Además: si NO existe ningún Saboteador, se FUERZA la conversión
        // (sin tirar el dado de probabilidad) hasta que exista al menos 1.
        var dataSabo = CustomRoleUtils.GetAssignData((RoleTypes)RoleId.Get<SaboteadorRole>());

        var activosSabo = CustomRoleUtils.GetActiveRoles().OfType<SaboteadorRole>().Count();
        var fuerza = dataSabo.Count > 0 && activosSabo <= 0;

        if (dataSabo.Count <= 0)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                "[Saboteador] WrapUp: # DE ROL del Saboteador está en 0 → no habrá conversión.");
            yield break;
        }

        if (activosSabo >= dataSabo.Count)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                $"[Saboteador] WrapUp: ya hay {activosSabo}/{dataSabo.Count} Saboteadores → no convierte a más.");
            yield break;
        }

        if (!fuerza && dataSabo.Chance < 100 && HashRandom.Next(101) > dataSabo.Chance)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                $"[Saboteador] WrapUp: falló la tirada de probabilidad ({dataSabo.Chance}%) → sin conversión esta reunión.");
            yield break;
        }

        var candidatosSabo = PlayerControl.AllPlayerControls.ToArray().Where(x =>
            x != null && x.Data != null && !x.Data.Disconnected &&
            x.Data.IsDead &&
            x != _exiliadoActual &&
            !EsRolDeExpulsion(x.GetRoleWhenAlive()) &&
            x.Data.Role is not SaboteadorRole &&
            // REGLA afterlife: Kamikaze/Jester (y roles exile-win similares)
            // NO reciben afterlife si aún pueden cumplir su victoria especial
            // (expulsión en la reunión de detonación / expulsión Jester).
            // Solo entran si YA FALLARON (ver PuedeRecibirAfterlife). Antes
            // se excluía al Kamikaze de forma incondicional comparando
            // Data.Role (que en un muerto ya es ImpostorGhost): eso nunca
            // filtraba nada y de paso podía convertir a un Kamikaze que aún
            // podía detonar, rompiendo su arco.
            x.CanGetGhostRole() &&
            EsImpostorCuandoVivo(x) &&
            PuedeRecibirAfterlife(x)).ToList();

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Saboteador] WrapUp: {candidatosSabo.Count} impostores muertos elegibles" +
            (fuerza ? " (FORZADO: no existe ningún Saboteador aún)" : "") + ".");

        if (candidatosSabo.Count == 0)
        {
            yield break;
        }

        for (var i = candidatosSabo.Count - 1; i > 0; i--)
        {
            var j = HashRandom.Next(i + 1);
            (candidatosSabo[i], candidatosSabo[j]) = (candidatosSabo[j], candidatosSabo[i]);
        }

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Saboteador] WrapUp: convirtiendo a {candidatosSabo[0].Data?.PlayerName} en Saboteador" +
            (fuerza ? " (forzado)" : "") + ".");

        ConvertirSaboteador(candidatosSabo[0]);
    }

    /// <summary>
    ///     ¿Era impostor este jugador cuando estaba vivo? (patrón TOU Mira:
    ///     el rol ACTUAL de un muerto ya es el fantasma vanilla, así que se
    ///     comprueba el rol CUANDO VIVO). Cubre impostores vanilla y roles
    ///     custom de equipo impostor (Kamikaze, etc.).
    /// </summary>
    public static bool EsImpostorCuandoVivo(PlayerControl jugador)
    {
        try
        {
            var rol = jugador.GetRoleWhenAlive();
            if (rol == null)
            {
                return jugador.Data.Role.IsImpostor;
            }

            return rol.IsImpostor ||
                   (rol is ITownOfUsRole touRole && touRole.Team == ModdedRoleTeams.Impostor);
        }
        catch
        {
            return jugador.Data?.Role?.IsImpostor == true;
        }
    }

    /// <summary>
    ///     ¿Era tripulante este jugador cuando estaba vivo? (patrón TOU Mira:
    ///     el rol ACTUAL de un muerto ya es su fantasma vanilla). Es el
    ///     inverso de EsImpostorCuandoVivo: cualquier jugador que no sea
    ///     impostor/neutral-custom de equipo impostor cuenta como tripulante.
    /// </summary>
    public static bool EsTripulanteCuandoVivo(PlayerControl jugador)
    {
        return !EsImpostorCuandoVivo(jugador);
    }

    /// <summary>
    ///     4. RPC de conversión: el host cambia el rol a afterlife; TODOS los
    ///     clientes reciben el cambio (Initialize aplica fantasma + mecánicas).
    ///     El rol queda persistido en los datos de la partida (reconexión OK).
    ///     Usa RpcChangeRole (como ConvertirSaboteador/TOU Mira) para convertir
    ///     a un muerto cuyo rol actual ya es su fantasma vanilla.
    /// </summary>
    public static void ConvertirAfterlife(PlayerControl jugador)
    {
        if (jugador?.Data == null)
        {
            return;
        }

        // REGLA NO NEGOCIABLE: NUNCA asignar afterlife a un jugador VIVO.
        if (!jugador.Data.IsDead)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Afterlife] INTENTO BLOQUEADO: convertir a VIVO {jugador.Data.PlayerName} en Cerrajero afterlife.");
            return;
        }

        jugador.RpcChangeRole(RoleId.Get<CerrajeroRole>());
    }

    /// <summary>
    ///     4. RPC de conversión del Huésped: el host cambia el rol a afterlife;
    ///     TODOS los clientes reciben el cambio (Initialize aplica fantasma +
    ///     mecánicas). Mismo patrón que ConvertirAfterlife (RpcChangeRole).
    /// </summary>
    public static void ConvertirHuesped(PlayerControl jugador)
    {
        if (jugador?.Data == null)
        {
            return;
        }

        // REGLA NO NEGOCIABLE: NUNCA asignar afterlife a un jugador VIVO.
        if (!jugador.Data.IsDead)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Afterlife] INTENTO BLOQUEADO: convertir a VIVO {jugador.Data.PlayerName} en Huésped afterlife.");
            return;
        }

        jugador.RpcChangeRole(RoleId.Get<HuespedRole>());
    }

    /// <summary>
    ///     Conversión de un impostor muerto a Saboteador por RPC del host.
    ///     USA RpcChangeRole como TOU Mira (GhostRoleEvents usa
    ///     player.RpcChangeRole(RoleId.Get&lt;HaunterRole&gt;()) para sus
    ///     afterlife): el RpcSetRole vanilla/custom directo fallaba para
    ///     convertir un muerto cuyo rol actual es ImpostorGhost.
    /// </summary>
    public static void ConvertirSaboteador(PlayerControl jugador)
    {
        if (jugador?.Data == null)
        {
            return;
        }

        // REGLA NO NEGOCIABLE: NUNCA asignar afterlife a un jugador VIVO.
        if (!jugador.Data.IsDead)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Afterlife] INTENTO BLOQUEADO: convertir a VIVO {jugador.Data.PlayerName} en Saboteador afterlife.");
            return;
        }

        jugador.RpcChangeRole(RoleId.Get<SaboteadorRole>());
    }

    /// <summary>
    ///     Conversión de CUALQUIER muerto elegible (crew, impostor o neutral
    ///     cuando vivo) a Amalgama (afterlife neutral), por RPC del host.
    /// </summary>
    public static void ConvertirAmalgama(PlayerControl jugador)
    {
        if (jugador?.Data == null)
        {
            return;
        }

        // REGLA NO NEGOCIABLE: NUNCA asignar afterlife a un jugador VIVO.
        if (!jugador.Data.IsDead)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Afterlife] INTENTO BLOQUEADO: convertir a VIVO {jugador.Data.PlayerName} en Amalgama afterlife.");
            return;
        }

        jugador.RpcChangeRole(RoleId.Get<AmalgamaRole>());
    }
}