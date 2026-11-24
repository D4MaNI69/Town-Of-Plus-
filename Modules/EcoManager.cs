using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Translation;
using MiraAPI.Utilities;
using Reactor.Utilities;
using TownOfPlus.Assets;
using TownOfPlus.Options.Crewmate.CrewmateAfterlife;
using TownOfPlus.Roles.Crewmate.CrewmateAfterlife;
using TownOfUs.Modules;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TownOfPlus.Modules;

/// <summary>
///     ECO — estado compartido del rol afterlife «Eco».
///     <para>· ASIGNACIÓN (host, tras la reunión): sólo los jugadores que
///     murieron EN esa reunión por EXPULSIÓN o por ser ADIVINADOS/EJECUTADOS
///     son elegibles. Una muerte en ronda (kill, bomba, etc.) NUNCA convierte.</para>
///     <para>· ANCLA: a cada Eco se le asigna 1 jugador VIVO aleatorio
///     (crew/impostor/neutral). El Eco reaparece en su posición, queda atado a
///     él (TP de vuelta si se aleja del rango) y vuelve a su lado tras cada
///     reunión/reset.</para>
///     <para>· VISIBILIDAD/OPACIDAD: 80 % fija (quieto o en movimiento), sólo
///     visible para el ancla (los fantasmas/afterlife también lo ven).</para>
///     <para>· La verdad sincronizada es el par Eco→Ancla (RPC), no la posición.</para>
/// </summary>
public static class EcoManager
{
    /// <summary>Opacidad FIJA del Eco (80%, quieto o en movimiento).</summary>
    public const float Opacidad = 0.8f;

    /// <summary>Eco → PlayerId de su ancla (verdad sincronizada por RPC).</summary>
    private static readonly Dictionary<byte, byte> AnclasPorEco = new();

    /// <summary>Vigilancia de muertes EN reunión (host): vivos al empezar.</summary>
    private static readonly HashSet<byte> VivosAlEmpezarReunion = new();

    /// <summary>Muertos EN la reunión en curso (host): candidatos a Eco.</summary>
    private static readonly HashSet<byte> MuertosEnReunion = new();

    /// <summary>Ecos conocidos (todos): para el respawn post-reunión.</summary>
    private static readonly HashSet<byte> EcosConocidos = new();

    /// <summary>Temporizador de la limpieza periódica de anclas muertas.</summary>
    private static float _tiempoLimpieza;

    /// <summary>
    ///     True si ESTE cliente era el Eco y acaba de volver a fantasma común:
    ///     el mantenedor por frame le conserva el aspecto de fantasma normal
    ///     visible para sí mismo (bug: su propio personaje desaparecía).
    /// </summary>
    private static bool _localRevertidoAFantasma;

    // CONSULTAS
    public static byte? AnclaDe(byte ecoId) =>
        AnclasPorEco.TryGetValue(ecoId, out var ancla) ? ancla : null;

    public static bool EsEco(PlayerControl? jugador) =>
        jugador?.Data?.Role is EcoRole;

    public static bool EsAnclaDeAlgunEco(byte playerId) =>
        AnclasPorEco.ContainsValue(playerId);

    /// <summary>Eco al que esta atada un ancla (null si no es ancla).</summary>
    public static byte? EcoDe(byte anclaId)
    {
        foreach (var par in AnclasPorEco)
        {
            if (par.Value == anclaId)
            {
                return par.Key;
            }
        }

        return null;
    }

    public static bool EsEcoLocal =>
        PlayerControl.LocalPlayer?.Data?.Role is EcoRole;

    public static Dictionary<byte, byte> Pares() =>
        AnclasPorEco.ToDictionary(x => x.Key, x => x.Value);

    /// <summary>
    ///     Textos del rol (sistema de locale de MiraAPI; ES nativo en es_419,
    ///     con fallback EN/PT). "Asignado" lleva el NOMBRE DEL ANCLA ({0}):
    ///     "¡Ahora eres el Eco del más allá! Estás atado a {0}.".
    ///     "ComoConseguir" es el recordatorio de "Muere en reunión (expulsado o
    ///     adivinado) para volverte Eco.". Y "Ancla..." es el aviso de
    ///     muerte/desconexión del ancla, también con su nombre ({0}).
    /// </summary>
    public static string TextoEco(string subclave, string nombreAncla, string alternativa)
    {
        try
        {
            var plantilla = MiraLocaleManager.Get(
                "TownOfUsMira.Role.EcoRole." + subclave, alternativa);
            if (string.IsNullOrEmpty(plantilla))
                return string.Format(alternativa, nombreAncla);
            return string.Format(plantilla, nombreAncla);
        }
        catch
        {
            return string.Format(alternativa, nombreAncla);
        }
    }

    /// <summary>
    ///     Nombre visible del ancla (para los avisos con target: "... estás
    ///     atado a «{nombre}»"). Nunca devuelve nulo ni vacío.
    /// </summary>
    public static string NombreDelAncla(byte? anclaId, byte ecoId)
    {
        var nombre = ResolverPorId(anclaId ?? 255)?.Data?.PlayerName;
        if (string.IsNullOrEmpty(nombre))
            nombre = ResolverPorId(ecoId)?.Data?.PlayerName;
        return string.IsNullOrEmpty(nombre) ? "tu ancla" : nombre;
    }

    /// <summary>
    ///     Nombre visible del ECO enlazado a un ancla (lo usa el modificador
    ///     Ancla para mostrar «Estas atado a (Eco)»). Nunca devuelve nulo.
    /// </summary>
    public static string NombreDelEco(byte ecoId, byte anclaId)
    {
        var nombre = ResolverPorId(ecoId)?.Data?.PlayerName;
        if (string.IsNullOrEmpty(nombre))
            nombre = ResolverPorId(anclaId)?.Data?.PlayerName;
        return string.IsNullOrEmpty(nombre) ? "?" : nombre;
    }

    public static void ReiniciarPartida()
    {
        foreach (var par in AnclasPorEco.ToArray())
            QuitarModificadorAncla(par.Value);
        AnclasPorEco.Clear();
        VivosAlEmpezarReunion.Clear();
        MuertosEnReunion.Clear();
        EcosConocidos.Clear();
    }

    public static void AlComenzarReunion()
    {
        VivosAlEmpezarReunion.Clear();
        MuertosEnReunion.Clear();
        try
        {
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    if (p?.Data == null || p.Data.IsDead || p.Data.Disconnected)
                        continue;
                    VivosAlEmpezarReunion.Add(p.PlayerId);
                }
            }
            EcosConocidos.Clear();
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p?.Data?.Role is EcoRole)
                    EcosConocidos.Add(p.PlayerId);
            }
            // Limpieza entre partidas/reuniones: los pares cuyo Eco o ancla ya
            // no existen (o el ancla murió/desconectó) se limpian; si el ancla
            // murió, el Eco vuelve a ser un fantasma común.
            try
            {
                if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    DevolverHuerfanosAFantasma();
            }
            catch { }
        }
        catch { }
    }

    /// <summary>
    ///     HOST: si el ancla de un Eco murió/desconectó, se le asigna una NUEVA
    ///     ancla viva aleatoria (RPC a todos). Si no hay vivos, se quita el par
    ///     (el Eco queda huérfano visible solo para fantasmas).
    /// </summary>
    /// <summary>
    ///     Motivo por el que el ancla dejó de estar disponible: sirve para
    ///     elegir el aviso exacto que recibe el Eco ("fue asesinada en
    ///     ronda", "fue expulsada en reunión", "fue adivinada/ejecutada en
    ///     reunión" o "se desconectó").</summary>
    public enum MotivoPerdidaAncla
    {
        AsesinadaEnRonda = 0,
        ExpulsadaEnReunion = 1,
        AdivinadaEnReunion = 2,
        Desconectada = 3,
    }

    public static void DevolverHuerfanosAFantasma()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return;
        try
        {
            foreach (var par in AnclasPorEco.ToArray())
            {
                var eco = ResolverPorId(par.Key);
                var ancla = ResolverPorId(par.Value);

                // El propio Eco desapareció (desconexión) → solo limpiar el par.
                if (eco?.Data == null || eco.Data.Disconnected ||
                    eco.Data.Role is not EcoRole)
                {
                    QuitarEco(par.Key);
                    continue;
                }

                // Ancla VIVA y presente → el Eco sigue atado a ella.
                if (ancla?.Data != null && !ancla.Data.IsDead && !ancla.Data.Disconnected)
                    continue;

                // El ancla MURIÓ o se DESCONECTÓ → el Eco vuelve a ser un
                // fantasma común (chat/voz/visión de fantasma normales). El
                // motivo del aviso depende de DÓNDE/CÓMO murió el ancla:
                // expulsada (ExileController activo con ese jugador) vs. muerta
                // en reunión (adivinada/ejecutada, MeetingHud activo) vs.
                // asesinada en ronda vs. desconectada.
                var motivo = MotivoPerdidaAncla.AsesinadaEnRonda;
                if (ancla?.Data != null && ancla.Data.Disconnected)
                    motivo = MotivoPerdidaAncla.Desconectada;
                else if (ExileController.Instance != null &&
                         ExileController.Instance.initData?.networkedPlayer?.PlayerId == par.Value)
                    motivo = MotivoPerdidaAncla.ExpulsadaEnReunion;
                else if (MeetingHud.Instance != null)
                    motivo = MotivoPerdidaAncla.AdivinadaEnReunion;

                DevolverAFantasmaComun(par.Key, motivo);
            }
        }
        catch { }
    }

    /// <summary>
    ///     El Eco pasa a ser un FANTASMA COMÚN (RoleTypes.CrewmateGhost): se
    ///     borra su par/ancla en TODOS los clientes (RPC) y el host cambia su rol
    ///     por RPC. A partir de ahí es un fantasma normal: lee/escribe el chat de
    ///     fantasmas, oye y habla con ellos y no tiene ninguna regla del Eco.
    ///     Además, si este cliente ES el Eco, muestra el aviso CON TARGET según
    ///     el motivo ("Tu ancla {nombre} fue asesinada / expulsada /
    ///     adivinada / se desconectó...").
    /// </summary>
    public static void DevolverAFantasmaComun(byte ecoId, MotivoPerdidaAncla motivo = MotivoPerdidaAncla.AsesinadaEnRonda)
    {
        try
        {
            var eco = ResolverPorId(ecoId);
            var anclaId = AnclaDe(ecoId);

            // El par se borra SIEMPRE (host y clientes) para que dejen de
            // aplicarse los filtros de chat/visibilidad/voz del Eco.
            QuitarEco(ecoId);

            // Aviso CON TARGET: solo lo ve el cliente que ERA el Eco.
            try
            {
                var local = PlayerControl.LocalPlayer;
                if (local != null && local.PlayerId == ecoId && eco != null)
                {
                    var nombre = NombreDelAncla(anclaId, ecoId);
                    string subclave = motivo switch
                    {
                        MotivoPerdidaAncla.ExpulsadaEnReunion => "AnclaMurioExpulsada",
                        MotivoPerdidaAncla.AdivinadaEnReunion => "AnclaMurioAdivinada",
                        MotivoPerdidaAncla.Desconectada => "AnclaDesconecto",
                        _ => "AnclaMurioAsesinada",
                    };
                    string alternativa = motivo switch
                    {
                        MotivoPerdidaAncla.ExpulsadaEnReunion =>
                            "Tu ancla {0} fue expulsada en reunión: vuelves a ser un fantasma común.",
                        MotivoPerdidaAncla.AdivinadaEnReunion =>
                            "Tu ancla {0} fue adivinada/ejecutada en reunión: vuelves a ser un fantasma común.",
                        MotivoPerdidaAncla.Desconectada =>
                            "Tu ancla {0} se desconectó: vuelves a ser un fantasma común.",
                        _ =>
                            "Tu ancla {0} fue asesinada en ronda: vuelves a ser un fantasma común.",
                    };
                    var texto = TextoEco(subclave, nombre, alternativa);
                    RestaurarFantasmaComun(eco);
                    Helpers.CreateAndShowNotification(
                        "<b>" + texto + "</b>",
                        EcoRole.EcoColor, new Vector3(0f, 1f, -20f),
                        spr: TownofplusAssets.EcoIcon.LoadAsset());
                }
            }
            catch
            {
                // Sin notificación no pasa nada.
            }

            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
                return;

            Networking.EcoRpc.DevolverAFantasmaGlobal(ecoId);

            if (eco?.Data != null && eco.Data.Role is EcoRole)
                eco.RpcChangeRole((ushort)RoleTypes.CrewmateGhost);
        }
        catch { }
    }

    /// <summary>
    ///     Restaura el FANTASMA COMÚN del juego en el acto (sin esperar al
    ///     cambio de rol por red): capa Ghost (atraviesa paredes), motor vanilla
    ///     de invisibilidad (los vivos NO lo ven, los fantasmas SÍ) y — esto es
    ///     lo que arregla el bug reportado — los SPRITES del cuerpo, sombrero,
    ///     visor y mascota vuelven al 100% de opacidad, porque el Eco los tenía
    ///     al 80% (o a 0 si acababa de ocultarse) y "no se veía su propio
    ///     personaje" al pasar a fantasma normal.
    ///     SOLO debe llamarse para el jugador LOCAL (el alpha es una decisión
    ///     visual de cada cliente).
    /// </summary>
    public static void RestaurarFantasmaComun(PlayerControl fantasma)
    {
        if (fantasma?.Data == null)
            return;
        try
        {
            fantasma.gameObject.layer = LayerMask.NameToLayer("Ghost");

            // CLAVE DEL ARREGLO: shouldAppearInvisible=true hace que el motor del
            // juego ponga Visible=false, y entonces NI EL PROPIO JUGADOR ve su
            // personaje (el síntoma reportado al volver a fantasma común). El
            // fantasma normal lleva la invisibilidad para los VIVOS por su capa
            // Ghost y la lógica vanilla, no por este flag.
            fantasma.shouldAppearInvisible = false;

            // Guard: TOU Mira parchea PlayerControl.set_Visible
            // (GhostRolePatches.VisibleOverridePatch) y lanza NRE si el rol es
            // null en ese instante; jamás debe romper la visibilidad del mod.
            try
            {
                if (fantasma.Data?.Role != null)
                {
                    fantasma.Visible = true;
                }
            }
            catch { }

            try { fantasma.gameObject.SetActive(true); } catch { }
            _localRevertidoAFantasma = true;

            var cosmetics = fantasma.cosmetics;
            if (cosmetics == null)
                return;

            var body = cosmetics.currentBodySprite?.BodySprite;
            if (body != null)
            {
                var c = body.color;
                c.a = 1f;
                body.color = c;
            }

            try { fantasma.SetHatAndVisorAlpha(1f); } catch { }
            if (cosmetics.currentPet != null && cosmetics.currentPet.gameObject != null)
            {
                try { cosmetics.currentPet.gameObject.SetActive(true); } catch { }
            }

            try { cosmetics.ToggleNameVisible(false); } catch { }
        }
        catch { }
    }

    /// <summary>
    ///     Mantenedor por frame del fantasma común devuelto (solo el jugador
    ///     local): el motor vanilla vuelve a poner shouldAppearInvisible=true en
    ///     cuanto cambia el rol, así que se re-aplica el estado de "ghost normal
    ///     visible para uno mismo" hasta que el jugador deje de ser un fantasma
    ///     común (o pase a otro afterlife custom).
    /// </summary>
    public static void MantenerFantasmaComunVisible()
    {
        if (!_localRevertidoAFantasma)
            return;

        var local = PlayerControl.LocalPlayer;
        if (local?.Data == null || !local.Data.IsDead || local.Data.Role is IGhostRole)
        {
            _localRevertidoAFantasma = false;
            return;
        }

        // Transición con el rol en null (cambio de rol en curso): saltar el
        // frame, pero NO desactivar el mantenedor.
        if (local.Data.Role == null)
        {
            return;
        }

        try
        {
            local.gameObject.layer = LayerMask.NameToLayer("Ghost");
            local.shouldAppearInvisible = false;
            // Guard (ver RestaurarFantasmaComun): nunca tocar Visible con
            // rol null, TOU Mira lanza NRE en ese caso.
            local.Visible = true;
        }
        catch { }
    }

    public static void ActualizarReunion()
    {
        if (VivosAlEmpezarReunion.Count == 0)
            return;
        if (MeetingHud.Instance == null)
        {
            VivosAlEmpezarReunion.Clear();
            return;
        }
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return;
        try
        {
            foreach (var id in VivosAlEmpezarReunion.ToArray())
            {
                var jugador = ResolverPorId(id);
                if (jugador?.Data == null)
                {
                    VivosAlEmpezarReunion.Remove(id);
                    continue;
                }
                if (!jugador.Data.IsDead)
                    continue;
                VivosAlEmpezarReunion.Remove(id);
                MuertosEnReunion.Add(id);
            }
        }
        catch { }
    }

    public static bool MurioEnEstaReunion(PlayerControl jugador)
    {
        if (jugador?.Data == null)
            return false;
        try
        {
            var exiliado = ExileController.Instance?.initData?.networkedPlayer?.Object;
            if (exiliado != null && exiliado.PlayerId == jugador.PlayerId)
                return true;
        }
        catch { }
        return MuertosEnReunion.Contains(jugador.PlayerId);
    }

    // Reinicio al empezar partida (todos): evita pares fantasma entre juegos.
    public static void AlEmpezarPartida()
    {
        ReiniciarPartida();
    }

    // ASIGNACION HOST
    public static List<PlayerControl> CandidatosEco()
    {
        var lista = new List<PlayerControl>();
        try
        {
            var exiliado = ExileController.Instance?.initData?.networkedPlayer?.Object;
            if (exiliado?.Data != null && PuedeSerEco(exiliado, true))
                lista.Add(exiliado);
            foreach (var id in MuertosEnReunion)
            {
                var muerto = ResolverPorId(id);
                if (muerto == null || (exiliado != null && muerto.PlayerId == exiliado.PlayerId))
                    continue;
                if (PuedeSerEco(muerto, false))
                    lista.Add(muerto);
            }
        }
        catch { }
        return lista;
    }

    private static bool PuedeSerEco(PlayerControl jugador, bool incluirExiliado)
    {
        if (jugador?.Data == null || !jugador.Data.IsDead || jugador.Data.Disconnected)
            return false;
        try
        {
            // Ya es Eco u otro afterlife: CanGetGhostRole lo excluye (igual que
            // el pipeline normal: un convertido ya no es candidato).
            if (jugador.Data.Role is EcoRole || !jugador.CanGetGhostRole())
                return false;
            // Exclusión DEFINITIVA Jester/Kamikaze: el patch
            // BloqueoAfterlifeRolesExpulsionPatch cancelaría igual el
            // RpcChangeRole; se filtra aquí para no intentarlo.
            if (AfterlifeManager.EsRolDeExpulsion(jugador.GetRoleWhenAlive()))
                return false;
        }
        catch { return false; }
        if (!AfterlifeManager.EsTripulanteCuandoVivo(jugador))
            return false;
        if (!AfterlifeManager.PuedeRecibirAfterlife(jugador))
            return false;
        return incluirExiliado || MuertosEnReunion.Contains(jugador.PlayerId);
    }

    /// <summary>
    ///     Sliders vanilla del rol (# DE ROL y PROBABILIDAD %), EXACTAMENTE el
    ///     mismo patrón que Cerrajero/Huésped (AfterlifeManager.CoIntentar...):
    ///     si el rol está desactivado (contador 0) o ya hay tantos Ecos activos
    ///     como permite el contador, no se asigna; si no, se tira el dado con
    ///     HashRandom (patrón TOU Mira).
    /// </summary>
    public static bool PuedeAsignarseEco()
    {
        try
        {
            var data = CustomRoleUtils.GetAssignData((RoleTypes)RoleId.Get<EcoRole>());
            if (data.Count <= 0)
                return false;
            var activos = CustomRoleUtils.GetActiveRoles().OfType<EcoRole>().Count();
            if (activos >= data.Count)
                return false;
            var chance = data.Chance;
            if (chance < 100 && HashRandom.Next(101) > chance)
                return false;
            return true;
        }
        catch { return false; }
    }

    public static PlayerControl? ElegirAnclaAleatoria()
    {
        try
        {
            var vivos = PlayerControl.AllPlayerControls.ToArray()
                .Where(p => p?.Data != null && !p.Data.IsDead && !p.Data.Disconnected)
                .ToList();
            if (vivos.Count == 0)
                return null;
            return vivos[UnityEngine.Random.Range(0, vivos.Count)];
        }
        catch { return null; }
    }

    public static void ConvertirConAncla(PlayerControl eco, PlayerControl ancla)
    {
        if (eco?.Data == null || ancla?.Data == null)
            return;

        // REGLA NO NEGOCIABLE: NUNCA asignar afterlife a un jugador VIVO.
        if (!eco.Data.IsDead)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Eco] INTENTO BLOQUEADO: convertir a VIVO {eco.Data.PlayerName} en Eco afterlife.");
            return;
        }

        try
        {
            eco.RpcChangeRole(RoleId.Get<EcoRole>());
            Networking.EcoRpc.AsignarAnclaGlobal(eco, ancla);
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Eco] No se pudo convertir a {eco.Data?.PlayerName}: {ex.Message}");
        }
    }

    public static void AplicarAncla(byte ecoId, byte anclaId)
    {
        AnclasPorEco[ecoId] = anclaId;
        EcosConocidos.Add(ecoId);
        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local != null && local.PlayerId == ecoId)
                LlevarJuntoAlAncla(ecoId, anclaId);
        }
        catch { }

        // Modificador ANCLA: SOLO el host lo otorga por RPC (el RPC se difunde
        // a TODOS los clientes: así el ancla lo lleva en todas las pantallas y
        // cualquier impostor puede adivinarlo). Los clientes no host esperan
        // ese RPC en lugar de añadirlo en local (evita divergencias).
        try
        {
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                var ancla = ResolverPorId(anclaId);
                if (ancla != null && ancla.Data != null && !ancla.Data.IsDead &&
                    !ancla.Data.Disconnected && !ancla.HasModifier<Modifiers.Crewmate.AnclaModifier>())
                {
                    ancla.RpcAddModifier<Modifiers.Crewmate.AnclaModifier>();
                }
            }
        }
        catch { }
    }

    /// <summary>
    ///     Quita el modificador ANCLA del jugador (al liberar el par, morir o
    ///     desconectar). SOLO el host envía el RPC de borrado (difundido a
    ///     todos); los clientes no host no tocan su copia local.
    /// </summary>
    public static void QuitarModificadorAncla(byte anclaId)
    {
        try
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
                return;

            var ancla = ResolverPorId(anclaId);
            if (ancla == null)
                return;
            var comp = ancla.GetModifierComponent();
            if (comp == null)
                return;

            if (comp.GetModifiers<Modifiers.Crewmate.AnclaModifier>().Any())
                ancla.RpcRemoveModifier<Modifiers.Crewmate.AnclaModifier>();
        }
        catch { }
    }

    public static void QuitarEco(byte ecoId)
    {
        if (AnclasPorEco.TryGetValue(ecoId, out var viejaAncla))
            QuitarModificadorAncla(viejaAncla);
        AnclasPorEco.Remove(ecoId);
        EcosConocidos.Remove(ecoId);
    }

    // ATADO AL ANCLA
    public static void Actualizar()
    {
        if (AnclasPorEco.Count == 0)
            return;

        // Limpieza periódica (2 veces por segundo): si el ancla murió o se
        // desconectó, el Eco vuelve a ser un FANTASMA COMÚN. Solo el host lo
        // decide (y difunde el cambio por RPC).
        _tiempoLimpieza += Time.deltaTime;
        if (_tiempoLimpieza >= 0.5f)
        {
            _tiempoLimpieza = 0f;
            try
            {
                if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    DevolverHuerfanosAFantasma();
            }
            catch { }
            if (AnclasPorEco.Count == 0)
                return;
        }

        float rango;
        try { rango = MiraAPI.GameOptions.OptionGroupSingleton<EcoOptions>.Instance.RangoAtado.Value; }
        catch { rango = 4f; }
        try
        {
            foreach (var par in AnclasPorEco.ToArray())
            {
                var eco = ResolverPorId(par.Key);
                var ancla = ResolverPorId(par.Value);
                if (!EcoYAnclaValidos(eco, ancla))
                {
                    // El par murió/desconectó: el host lo reancla en el próximo
                    // inicio de reunión (ReanclarHuerfanos); aquí se salta.
                    continue;
                }
                var distancia = Vector2.Distance(eco!.GetTruePosition(), ancla!.GetTruePosition());
                if (distancia <= rango)
                    continue;
                var local = PlayerControl.LocalPlayer;
                if (local != null && local.PlayerId == par.Key)
                {
                    LlevarJuntoAlAncla(par.Key, par.Value);
                    continue;
                }
                if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    Networking.EcoRpc.VolverAlAnclaGlobal(par.Key);
            }
        }
        catch { }
    }

    public static void ReunirTrasReunion()
    {
        if (AnclasPorEco.Count == 0)
            return;
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return;
        try
        {
            // Antes de re-unir a nadie: si el ancla murió (p. ej. expulsada en
            // esta reunión) o se desconectó, ese Eco vuelve a ser fantasma común.
            DevolverHuerfanosAFantasma();
            foreach (var par in AnclasPorEco.ToArray())
            {
                var eco = ResolverPorId(par.Key);
                if (eco?.Data == null || eco.Data.Disconnected)
                    continue;
                Networking.EcoRpc.VolverAlAnclaGlobal(par.Key);
            }
            MuertosEnReunion.Clear();
            VivosAlEmpezarReunion.Clear();
        }
        catch { }
    }

    public static void LlevarJuntoAlAncla(byte ecoId, byte anclaId)
    {
        try
        {
            var eco = ResolverPorId(ecoId);
            var ancla = ResolverPorId(anclaId);
            if (!EcoYAnclaValidos(eco, ancla))
                return;
            var destino = ancla!.GetTruePosition();
            if (eco!.AmOwner || AmongUsClient.Instance.AmHost)
                eco.NetTransform.SnapTo(destino);
        }
        catch { }
    }

    private static bool EcoYAnclaValidos(PlayerControl? eco, PlayerControl? ancla)
    {
        return eco?.Data != null && !eco.Data.Disconnected &&
               ancla?.Data != null && !ancla.Data.IsDead && !ancla.Data.Disconnected;
    }

    public static void AlInicializarRol(PlayerControl eco)
    {
        if (eco?.Data == null)
            return;
        try
        {
            if (AnclasPorEco.TryGetValue(eco.PlayerId, out var anclaId))
                LlevarJuntoAlAncla(eco.PlayerId, anclaId);
            else
                Coroutines.Start(EsperarAncla(eco.PlayerId));
        }
        catch { }
    }

    private static System.Collections.IEnumerator EsperarAncla(byte ecoId)
    {
        var intentos = 0;
        while (!AnclasPorEco.ContainsKey(ecoId) && intentos < 100)
        {
            intentos++;
            yield return new WaitForSeconds(0.1f);
        }
        if (AnclasPorEco.TryGetValue(ecoId, out var anclaId))
            LlevarJuntoAlAncla(ecoId, anclaId);
    }

    public static void SalirDelRol(PlayerControl eco)
    {
        if (eco == null)
            return;
        QuitarEco(eco.PlayerId);
    }

    private static PlayerControl? ResolverPorId(byte id)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p != null && p.PlayerId == id)
                return p;
        }
        return null;
    }
}
