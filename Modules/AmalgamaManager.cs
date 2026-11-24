using System.Collections;
using System.Linq;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using Reactor.Utilities;
using TownOfPlus.Networking;
using TownOfPlus.Utilities;
using TownOfPlus.Options.Neutral.NeutralAfterlife;
using TownOfPlus.Roles.Neutral.NeutralAfterlife;
using TownOfUs.Modules;
using TownOfUs.Modifiers;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TownOfPlus.Modules;

/// <summary>
///     Lógica del rol Amalgama (afterlife neutral):
///     - Canalización local del revive: al completar los N segundos, se pide
///       al host completar; si alguien reporta un cuerpo o llama reunión
///       durante la canalización, FALLA (el cuerpo no se consume).
///     - Al completar: el cuerpo desaparece, la Amalgama revive con el color
///       y cosméticos del cuerpo y recibe un rol ALEATORIO del pool de roles
///       habilitados (cantidad ≥ 1) en esa partida (crew, impostor o neutral).
/// </summary>
public static class AmalgamaManager
{
    /// <summary>¿Está la Amalgama local canalizando un revive?</summary>
    public static bool Canalizando { get; private set; }

    /// <summary>ParentId del cuerpo objetivo de la canalización en curso.</summary>
    private static byte _cuerpoObjetivo;

    /// <summary>¿El jugador local tiene el rol Amalgama?</summary>
    public static bool EsAmalgamaLocal =>
        EsAmalgama(PlayerControl.LocalPlayer);

    /// <summary>¿El jugador tiene el rol Amalgama? (rol actual o cuando estaba vivo)</summary>
    public static bool EsAmalgama(PlayerControl? jugador)
    {
        if (jugador?.Data == null)
        {
            return false;
        }

        try
        {
            return jugador.Data.Role is AmalgamaRole ||
                   jugador.GetRoleWhenAlive() is AmalgamaRole;
        }
        catch
        {
            return jugador.Data.Role is AmalgamaRole;
        }
    }

    // ── BÚSQUEDA DE CUERPO ───────────────────────────────────────────────────

    /// <summary>Devuelve el cuerpo no reportado más cercano a la Amalgama local.
    ///     ÁREA PEQUEÑA: solo cuerpos a 1.25 unidades (prácticamente encima).</summary>
    public static DeadBody? BuscarCuerpoCercano(float rango = 1.25f)
    {
        DeadBody? mejor = null;
        var mejorDist = rango;

        var local = PlayerControl.LocalPlayer;
        if (local == null)
        {
            return null;
        }

        try
        {
            foreach (var cuerpo in Object.FindObjectsOfType<DeadBody>())
            {
                if (cuerpo == null || cuerpo.Reported)
                {
                    continue;
                }

                var dist = Vector2.Distance(local.transform.position, cuerpo.TruePosition);
                if (dist < mejorDist)
                {
                    mejorDist = dist;
                    mejor = cuerpo;
                }
            }
        }
        catch
        {
            return null;
        }

        return mejor;
    }

    // ── CANALIZACIÓN ─────────────────────────────────────────────────────────

    /// <summary>Inicia la canalización sobre un cuerpo. False si ya está canalizando.</summary>
    public static bool IniciarRevive(byte bodyParentId)
    {
        if (Canalizando)
        {
            return false;
        }

        Canalizando = true;
        _cuerpoObjetivo = bodyParentId;

        var segundos = OptionGroupSingleton<AmalgamaOptions>.Instance.SegundosParaRevivir.Value;

        // Efectos de canalización: overlay rosa #FF84CD en fade + Revive.wav en bucle.
        CrearOverlayRevive();
        Coroutines.Start(CoFadeOverlay(segundos));
        IniciarSonidoRevive();

        Coroutines.Start(CoCanalizar(segundos));
        return true;
    }

    /// <summary>
    ///     Canalización de N segundos. FALLA si alguien reporta un cuerpo o
    ///     llama reunión (MeetingHud/ExileController aparecen): el cuerpo NO
    ///     se consume y la Amalgama pierde esa oportunidad.
    /// </summary>
    private static IEnumerator CoCanalizar(float segundos)
    {
        var restante = segundos;

        // Bloquear el movimiento del jugador local mientras canaliza.
        var local = PlayerControl.LocalPlayer;
        try
        {
            if (local != null)
            {
                local.moveable = false;
            }
        }
        catch
        {
            // Si falla, no bloquea el revive.
        }

        while (restante > 0f)
        {
            // Cancelación: report de cuerpo o llamada a reunión.
            if (MeetingHud.Instance != null || ExileController.Instance != null)
            {
                RestaurarMovimiento(local);
                PararEfectosRevive();
                CancelarRevive("alguien reportó un cuerpo o llamó a reunión");
                yield break;
            }

            restante -= Time.deltaTime;
            yield return null;
        }

        Canalizando = false;
        RestaurarMovimiento(local);

        // Fin del canal: parar el sonido de revive (el overlay hace su
        // fade-out dentro de CoFadeOverlay).
        PararSonidoRevive();

        AmalgamaRpc.PedirRevivir(PlayerControl.LocalPlayer, _cuerpoObjetivo);
    }

    /// <summary>Reactiva el movimiento del jugador local (fin/cancelación de canalización).</summary>
    private static void RestaurarMovimiento(PlayerControl? local)
    {
        try
        {
            if (local != null)
            {
                local.moveable = true;
            }
        }
        catch
        {
            // Nada crítico.
        }
    }

    /// <summary>Cancela la canalización actual (el cuerpo NO se consume).</summary>
    public static void CancelarRevive(string motivo)
    {
        if (!Canalizando)
        {
            return;
        }

        Canalizando = false;
        _cuerpoObjetivo = 0;
        RestaurarMovimiento(PlayerControl.LocalPlayer);

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Amalgama] Canalización FALLIDA ({motivo}): el cuerpo no se consume.");

        try
        {
            Helpers.CreateAndShowNotification(
                "<b>El revive falló...</b>\nNo pudiste completar la canalización.",
                Color.white, new Vector3(0f, 1f, -20f),
                spr: TownOfPlus.Assets.TownofplusAssets.AmalgamaIcon.LoadAsset());
        }
        catch
        {
            // Sin notificación no pasa nada.
        }
    }

    /// <summary>Limpia estados locales al deinitializar el rol.</summary>
    public static void LimpiarEstadosLocales()
    {
        Canalizando = false;
        _cuerpoObjetivo = 0;
        RestaurarMovimiento(PlayerControl.LocalPlayer);
        PararEfectosRevive();
    }

    /// <summary>
    ///     RED DE SEGURIDAD del revive: tras llamar a RevivePlayer (Altruist),
    ///     se comprueba si el jugador sigue VIVO; si por cualquier motivo el
    ///     pipeline no lo revivió (compuerta interna de Data.IsDead, sync
    ///     tardío, etc.), se FORZA el revive manual en este cliente: limpia el
    ///     historial de muertes, revive, asigna el rol y lo reposiciona.
    /// </summary>
    public static IEnumerator CoForzarReviveSiFalta(
        PlayerControl amalgama, ushort roleId, bool esCustom, Vector2 posicion)
    {
        yield return new WaitForSeconds(1f);

        try
        {
            if (amalgama == null || amalgama.Data == null)
            {
                yield break;
            }

            // Si el juego ya lo considera vivo, RevivePlayer hizo su trabajo.
            if (!amalgama.Data.IsDead)
            {
                TownOfPlusPlugin.Instance?.Log.LogInfo(
                    "[Amalgama] Revive confirmado: el jugador ya está vivo.");
                yield break;
            }

            TownOfPlusPlugin.Instance?.Log.LogWarning(
                "[Amalgama] RevivePlayer no aplicó el revive → FORZANDO revive manual.");

            // 1) Limpiar el historial de muerte (como ReviveUtilities).
            try
            {
                GameHistory.ClearMurder(amalgama);
            }
            catch
            {
                // Sin historial no pasa nada.
            }

            // 2) Revivir: el juego vuelve a detectarlo vivo (asiste a reuniones/vota).
            try
            {
                amalgama.Revive();
            }
            catch (Exception ex)
            {
                TownOfPlusPlugin.Instance?.Log.LogWarning(
                    $"[Amalgama] Revive() manual falló: {ex.Message}");
            }

            // 3) Asignar el rol nuevo (canal oficial TOU: ChangeRole sirve para
            //    custom y vanilla).
            try
            {
                // RpcChangeRole (PlayerControl) aplica el cambio p.ej. en modo
                // host; para cualquier cliente se delega en RevivePlayer, que
                // ya lo hizo. Aquí es solo red de seguridad del host.
                if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                {
                    amalgama.RpcChangeRole(roleId, true);
                }
            }
            catch (Exception ex)
            {
                TownOfPlusPlugin.Instance?.Log.LogWarning(
                    $"[Amalgama] ChangeRole manual falló: {ex.Message}");
            }

            // 4) Posición y capa de jugador.
            try
            {
                amalgama.NetTransform.RpcSnapTo(posicion);
                amalgama.gameObject.layer = LayerMask.NameToLayer("Players");
                amalgama.shouldAppearInvisible = false;
                amalgama.cosmetics.ToggleNameVisible(true);
            }
            catch
            {
                // Sin snap no se rompe el revive.
            }
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Amalgama] Red de seguridad del revive: {ex.Message}");
        }
    }

    // ── CUERPOS ──────────────────────────────────────────────────────────────

    /// <summary>Obtiene el cuerpo por el PlayerId del jugador original.</summary>
    public static DeadBody? ObtenerCuerpo(byte bodyParentId)
    {
        try
        {
            return Object.FindObjectsOfType<DeadBody>()
                .FirstOrDefault(b => b != null && b.ParentId == bodyParentId);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Destruye el cuerpo indicado (local en cada cliente).</summary>
    public static void DestruirCuerpo(byte bodyParentId)
    {
        try
        {
            foreach (var cuerpo in Object.FindObjectsOfType<DeadBody>())
            {
                if (cuerpo != null && cuerpo.ParentId == bodyParentId)
                {
                    Object.Destroy(cuerpo.gameObject);
                }
            }
        }
        catch
        {
            // Sin cuerpos que limpiar.
        }
    }

    /// <summary>Resetea a la Amalgama a tripulante normal y VISIBLE (capa, invisibilidad, cosmetics).</summary>
    public static void ResetearJugadorAVisible(PlayerControl jugador)
    {
        try
        {
            jugador.gameObject.layer = LayerMask.NameToLayer("Players");
            jugador.shouldAppearInvisible = false;
            jugador.cosmetics.ToggleNameVisible(true);
            jugador.SetHatAndVisorAlpha(1f);
            var sprite = jugador.cosmetics.currentBodySprite.BodySprite;
            if (sprite != null)
            {
                sprite.color = Color.white;
            }
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning($"[Amalgama] ResetearJugadorAVisible: {ex.Message}");
        }
    }

    /// <summary>Copia el color del dueÃ±o del cuerpo usado para revivir.</summary>
    public static void CopiarCosmeticsDelCuerpo(PlayerControl jugador, byte bodyParentId)
    {
        try
        {
            var info = GameData.Instance.GetPlayerById(bodyParentId);
            if (info != null)
            {
                // Capturar UNA VEZ la apariencia ORIGINAL de la Amalgama
                // (antes de copiar la del cuerpo) para restaurarla al
                // terminar la partida.
                if (!_aparienciasOriginales.ContainsKey(jugador.PlayerId) && jugador.Data != null)
                {
                    var propia = jugador.Data.DefaultOutfit;
                    _aparienciasOriginales[jugador.PlayerId] = new AparienciaOriginal(
                        (byte)propia.ColorId,
                        jugador.Data.PlayerName,
                        propia.HatId, propia.VisorId, propia.SkinId, propia.PetId);
                }

                jugador.RpcSetColor((byte)info.DefaultOutfit.ColorId);

                // Copia COMPLETA de la apariencia del dueño del cuerpo:
                // gorro, visor, traje, mascota y NOMBRE (todo sincronizado
                // por los RPC oficiales del juego a todos los clientes).
                var outfit = info.DefaultOutfit;
                try { jugador.RpcSetHat(outfit.HatId); } catch { /* sin gorro */ }
                try { jugador.RpcSetVisor(outfit.VisorId); } catch { /* sin visor */ }
                try { jugador.RpcSetSkin(outfit.SkinId); } catch { /* sin skin */ }
                try { jugador.RpcSetPet(outfit.PetId); } catch { /* sin mascota */ }
                try { jugador.RpcSetName(info.PlayerName); } catch { /* sin nombre */ }
            }
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning($"[Amalgama] CopiarCosmeticsDelCuerpo: {ex.Message}");
        }
    }
    // ── APARIENCIA ORIGINAL (solo dura ESTA partida) ─────────────────────────

    /// <summary>Apariencia previa de la Amalgama, capturada ANTES de copiar la del cuerpo.</summary>
    private sealed record AparienciaOriginal(
        byte ColorId, string Nombre, string Gorro, string Visor, string Traje, string Mascota);

    private static readonly Dictionary<byte, AparienciaOriginal> _aparienciasOriginales = new();

    /// <summary>Limpia el registro de apariencias originales (fin/inicio de partida).</summary>
    public static void LimpiarAparienciasOriginales() => _aparienciasOriginales.Clear();

    /// <summary>
    ///     Restaura la apariencia y el nombre ORIGINALES de cada jugador que
    ///     copió la apariencia de un cuerpo al revivir. Se ejecuta en cada
    ///     cliente al terminar la partida (el juego ya re-sincroniza el
    ///     outfit al volver al lobby; esto lo garantiza).
    ///     ANTICHEAT: solo el DUEÑO envía los RPCs de cosméticos.
    /// </summary>
    public static void RestaurarAparienciasOriginales()
    {
        try
        {
            foreach (var kvp in _aparienciasOriginales)
            {
                var jugador = PlayerControl.AllPlayerControls.ToArray()
                    .FirstOrDefault(p => p != null && p.PlayerId == kvp.Key);
                if (jugador == null || !jugador.AmOwner)
                {
                    continue;
                }

                var o = kvp.Value;
                try { jugador.RpcSetColor(o.ColorId); } catch { /* sin color */ }
                try { jugador.RpcSetHat(o.Gorro); } catch { /* sin gorro */ }
                try { jugador.RpcSetVisor(o.Visor); } catch { /* sin visor */ }
                try { jugador.RpcSetSkin(o.Traje); } catch { /* sin skin */ }
                try { jugador.RpcSetPet(o.Mascota); } catch { /* sin mascota */ }
                try { jugador.RpcSetName(o.Nombre); } catch { /* sin nombre */ }
            }
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Amalgama] Restauración de apariencias originales falló: {ex.Message}");
        }
    }

    // ── EFECTOS DE CANALIZACIÓN (overlay + sonido) ───────────────────────────

    private static GameObject? _overlayRevive;
    private static AudioSource? _sonidoRevive;

    /// <summary>Rosa espectral #FF84CD (overlay de canalización).</summary>
    private static readonly Color ColorOverlayRevive = new(255f / 255f, 132f / 255f, 205f / 255f, 1f);

    private const float AlphaMaxOverlay = 0.45f;

    /// <summary>Crea el overlay a pantalla completa (alpha inicial 0).</summary>
    private static void CrearOverlayRevive()
    {
        DestruirOverlayRevive();
        try
        {
            var go = new GameObject("TOP_OverlayReviveAmalgama");
            Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var image = go.AddComponent<Image>();
            image.color = new Color(
                ColorOverlayRevive.r, ColorOverlayRevive.g, ColorOverlayRevive.b, 0f);
            _overlayRevive = go;
        }
        catch
        {
            _overlayRevive = null;
        }
    }

    private static void DestruirOverlayRevive()
    {
        if (_overlayRevive == null)
        {
            return;
        }

        try
        {
            Object.Destroy(_overlayRevive);
        }
        catch
        {
            // Ya destruido.
        }

        _overlayRevive = null;
    }

    /// <summary>Reproduce Revive.wav EN BUCLE durante toda la canalización.</summary>
    private static void IniciarSonidoRevive()
    {
        PararSonidoRevive();
        try
        {
            if (SoundManager.Instance == null)
            {
                return;
            }

            var clip = SonidosTop.Revive.LoadAsset();
            if (clip == null)
            {
                return;
            }

            _sonidoRevive = AudioTop.Sonar(clip, true, 0.8f);
        }
        catch
        {
            _sonidoRevive = null;
        }
    }

    /// <summary>Detiene el sonido en bucle del revive.</summary>
    private static void PararSonidoRevive()
    {
        if (_sonidoRevive == null)
        {
            return;
        }

        try
        {
            _sonidoRevive.Stop();
        }
        catch
        {
            // Ya detenido.
        }

        _sonidoRevive = null;
    }

    /// <summary>Corta TODOS los efectos de la canalización (cancelación, fin de partida, etc.).</summary>
    public static void PararEfectosRevive()
    {
        PararSonidoRevive();
        DestruirOverlayRevive();
    }

    /// <summary>
    ///     FADE de pantalla #FF84CD: sube el alpha de 0 a AlphaMaxOverlay en
    ///     `segundos` (mientras canaliza) y al terminar baja a 0 en 0.6s y
    ///     se destruye el overlay.
    /// </summary>
    private static IEnumerator CoFadeOverlay(float segundos)
    {
        var image = _overlayRevive != null ? _overlayRevive.GetComponent<Image>() : null;
        if (image == null)
        {
            yield break;
        }

        var c = image.color;

        // Fade-IN gradual durante toda la canalización.
        var restante = segundos;
        while (restante > 0f)
        {
            var dt = Time.deltaTime;
            restante -= dt;
            var alpha = Mathf.Lerp(0f, AlphaMaxOverlay, 1f - Mathf.Max(0f, restante) / Mathf.Max(0.01f, segundos));
            image.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }

        image.color = new Color(c.r, c.g, c.b, AlphaMaxOverlay);

        // Fade-OUT suave al completar el revive.
        var t = 1f;
        while (t > 0f)
        {
            t -= Time.deltaTime / 0.6f;
            image.color = new Color(c.r, c.g, c.b, Mathf.Max(0f, t) * AlphaMaxOverlay);
            yield return null;
        }

        DestruirOverlayRevive();
    }

    /// <summary>
    ///     REVIVIR SIN ATRAVESAR PAREDES: restaura la física/colisión de
    ///     jugador VIVO (el estado espectral deja la colisión desactivada
    ///     para poder cruzar paredes, igual que un fantasma del juego).
    /// </summary>
    public static void RestaurarColisionViva(PlayerControl jugador)
    {
        try
        {
            jugador.MyPhysics.ResetMoveState();
            jugador.Collider.enabled = true;
            jugador.gameObject.layer = LayerMask.NameToLayer("Players");
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning($"[Amalgama] RestaurarColisionViva: {ex.Message}");
        }
    }

    /// <summary>
    ///     Pool de roles HABILITADOS con cantidad ≥ 1 en esta partida (el host
    ///     elige aquí). Incluye roles custom (MiraAPI/TOU) y vanilla; excluye
    ///     afterlife/ghost roles y la propia Amalgama.
    /// </summary>
    public static List<RoleBehaviour> PoolRolesHabilitados()
    {
        var opciones = new List<RoleBehaviour>();

        // 1) Roles custom habilitados (crew, impostor o neutral).
        try
        {
            foreach (var rol in CustomRoleManager.AllRoles)
            {
                if (rol == null || rol is IGhostRole)
                {
                    continue;
                }

                try
                {
                    var asign = CustomRoleUtils.GetAssignData(rol.Role);
                    if (asign != null && asign.Count > 0)
                    {
                        opciones.Add(rol);
                    }
                }
                catch
                {
                    // Rol sin datos de asignación: fuera del pool.
                }
            }
        }
        catch
        {
            // Sin roles custom: seguir con vanilla.
        }

        // 2) Roles vanilla habilitados.
        try
        {
            var roleOptions = GameOptionsManager.Instance.currentNormalGameOptions.roleOptions;
            foreach (var rol in RoleManager.Instance.AllRoles)
            {
                if (rol == null || rol is IGhostRole || CustomRoleUtils.IsCustomRole(rol))
                {
                    continue;
                }

                if (roleOptions.GetNumPerGame(rol.Role) > 0)
                {
                    opciones.Add(rol);
                }
            }
        }
        catch
        {
            // Sin acceso a opciones vanilla: quedarse con los customs.
        }

        return opciones;
    }

    /// <summary>Elige un rol aleatorio del pool de roles habilitados. Null si el pool está vacío.</summary>
    public static RoleBehaviour? ElegirRolAleatorio()
    {
        var pool = PoolRolesHabilitados();
        if (pool.Count == 0)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                "[Amalgama] Pool de roles habilitados VACÍO: no se puede revivir con rol.");
            return null;
        }

        var elegido = pool[UnityEngine.Random.Range(0, pool.Count)];
        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Amalgama] Rol aleatorio elegido del pool ({pool.Count} habilitados): {elegido.GetType().Name}.");
        return elegido;
    }

    // ── POOL DE MODIFICADORES HABILITADOS (+1s tras el revive) ───────────────

    /// <summary>
    ///     Facción del modifier esperada para un rol nuevo: crew / imp / neutral.
    ///     Se aceptan modifiers de ESA facción (FactionType Crewmate*/Impostor*/
    ///     Neutral*) más los Universal, siguiendo el pipeline de TOU Mira
    ///     (MiscUtils.GetModifierFaction / TouBaseGameModifier.FactionType).
    /// </summary>
    public static ModifierFaction? FaccionDeRol(RoleBehaviour rol)
    {
        try
        {
            if (rol.IsImpostor())
            {
                return ModifierFaction.Impostor;
            }

            if (rol.IsNeutral())
            {
                return ModifierFaction.Neutral;
            }

            if (rol.IsCrewmate())
            {
                return ModifierFaction.Crewmate;
            }
        }
        catch
        {
            // Rol sin facción conocida.
        }

        return null;
    }

    /// <summary>
    ///     Pool de modificadores HABILITADOS de la facción dada: solo modifiers
    ///     de TOU Mira con PROBABILIDAD &gt; 0 y CANTIDAD &gt; 0 en los ajustes de la
    ///     sala (GetAssignmentChance / GetAmountPerGame) y válidos sobre el rol
    ///     nuevo (IsModifierValidOn). Los Universal cuentan para cualquier
    ///     facción. Si no hay ninguno válido, el pool queda vacío.
    /// </summary>
    public static List<Type> PoolModifiersHabilitados(RoleBehaviour rolNuevo, ModifierFaction faccion)
    {
        var pool = new List<Type>();

        try
        {
            foreach (var mod in MiscUtils.AllBaseGameModifiers)
            {
                if (mod is not TouBaseGameModifier touMod)
                {
                    continue;
                }

                // Solo modifiers HABILITADOS con cantidad > 0 en esta sala.
                if (touMod.GetAssignmentChance() <= 0 || touMod.GetAmountPerGame() <= 0)
                {
                    continue;
                }

                // De la MISMA facción que el rol nuevo (o universal).
                var faccionMod = MiscUtils.GetModifierFaction(touMod);
                var mismaFaccion = faccionMod == faccion ||
                                   faccionMod.ToString().StartsWith(faccion.ToString()) ||
                                   faccionMod == ModifierFaction.Universal;
                if (!mismaFaccion)
                {
                    continue;
                }

                // Válido sobre el rol recién asignado.
                if (!touMod.IsModifierValidOn(rolNuevo))
                {
                    continue;
                }

                pool.Add(touMod.GetType());
            }
        }
        catch (System.Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Amalgama] Construcción del pool de modifiers falló: {ex.Message}");
        }

        return pool;
    }

    /// <summary>
    ///     Elige un modifier aleatorio del pool habilitado de la facción del rol
    ///     nuevo. Null si no hay ningún modifier válido (no se asigna nada, no
    ///     crashea).
    /// </summary>
    public static Type? ElegirModifierAleatorio(RoleBehaviour rolNuevo)
    {
        var faccion = FaccionDeRol(rolNuevo);
        if (faccion == null)
        {
            return null;
        }

        var pool = PoolModifiersHabilitados(rolNuevo, faccion.Value);
        if (pool.Count == 0)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                $"[Amalgama] Sin modifiers habilitados de facción {faccion}: no se asigna modifier.");
            return null;
        }

        var elegido = pool[UnityEngine.Random.Range(0, pool.Count)];
        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Amalgama] Modifier aleatorio elegido de facción {faccion} ({pool.Count} habilitados): {elegido.Name}.");
        return elegido;
    }
}