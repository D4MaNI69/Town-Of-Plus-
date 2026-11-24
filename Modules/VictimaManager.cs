using System.Collections;
using System.Linq;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Utilities;
using Reactor.Utilities;
using TownOfPlus.Networking;
using TownOfPlus.Options.Crewmate.CrewmateSupport;
using TownOfPlus.Roles.Crewmate.CrewmateSupport;
using TownOfUs.Modifiers.Game.Alliance;
using TownOfUs.Modules;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfPlus.Modules;

/// <summary>
///     LÃ³gica del rol VÃ­ctima (Town Of Plus):
///     - BOTÃ“N NOTA: abre un chat temporal SOLO para la VÃ­ctima (reutiliza el
///       ChatController vanilla en modo local; nunca envÃ­a al chat pÃºblico:
///       el texto se intercepta en los patches de SendChat/SendFreeChat).
///     - El mensaje se guarda en una lista interna (RPC a todos los clientes)
///       y NO se revela a nadie todavÃ­a.
///     - Al enviar, o al agotarse el tiempo de escritura (comportamiento
///       elegido: si no escribiÃ³ nada NO se guarda nada â€” nunca se guardan
///       notas vacÃ­as), el chat se cierra y Nota vuelve a cooldown.
///     - Cuando la VÃ­ctima muere (kill) o es expulsada, se marca la
///       revelaciÃ³n como PENDIENTE: se muestra en la PRÃ“XIMA reuniÃ³n
///       (la eyecciÃ³n NO revela en la misma reuniÃ³n donde ocurriÃ³).
///     - RevelaciÃ³n: alerta tipo afterlife (icono del rol + "La VÃ­ctima ha
///       muerto / Pero ha dejado atrÃ¡s sus notas...") y burbuja negra con
///       texto de colores (estilo Dictador) con todas las notas en orden:
///       "(NombreVÃ­ctima): nota".
/// </summary>
public static class VictimaManager
{
    /// <summary>
    ///     LISTA ÃšNICA ACUMULATIVA de notas por jugador (PlayerId â†’ notas en
    ///     orden de envÃ­o). Persiste TODA la partida: se acumulan notas a lo
    ///     largo de todas las rondas/reuniones y se sincronizan a todos los
    ///     clientes vÃ­a RPC (VictimaNota). SOLO se vacÃ­a al empezar una nueva
    ///     partida (ReiniciarPartida, llamado solo al empezar/terminar partida), asÃ­ ninguna nota se pierde entre
    ///     reuniones y la revelaciÃ³n post-muerte muestra absolutamente todas.
    /// </summary>
    private static readonly Dictionary<byte, List<string>> NotasPorJugador = new();

    /// <summary>Â¿EstÃ¡ la VÃ­ctima local escribiendo su nota? (chat temporal abierto)</summary>
    public static bool ModoNotaActivo { get; private set; }

    /// <summary>
    ///     Candado del chat temporal de Nota. Se activa al ENVIAR o al agotarse
    ///     el tiempo (o cerrar): bloquea que el chat se reabra en ronda y que el
    ///     botÃ³n/pestaÃ±a vuelvan a aparecer hasta el PRÃ“XIMO uso vÃ¡lido de Nota
    ///     (tras cooldown). Se desactiva solo en AbrirChatNota.
    /// </summary>
    public static bool ChatBloqueado { get; set; }

    /// <summary>
    ///     Buffer del texto que la VÃ­ctima estÃ¡ escribiendo. Se actualiza cada
    ///     frame mientras el modo nota estÃ¡ activo. Sirve para confirmar el
    ///     mensaje con ENTER aunque el flujo vanilla haya limpiado ya el campo:
    ///     asÃ­ ENTER SIEMPRE envÃ­a el mensaje escrito.
    /// </summary>
    private static string _textoEscribiendo = string.Empty;

    /// <summary>VÃ­ctima pendiente de revelaciÃ³n (host). Se limpia al revelar.</summary>
    private static byte _victimaPendiente;
    private static bool _revelacionPendiente;

    /// <summary>
    ///     VÃ­ctimas ya reveladas en esta partida (por muerte en reuniÃ³n,
    ///     expulsiÃ³n o la reuniÃ³n posterior a un kill): evita revelar dos veces.
    /// </summary>
    private static readonly HashSet<byte> _reveladas = new();

    /// <summary>
    ///     VÃ­ctimas VIVAS al empezar la reuniÃ³n actual: si alguna pasa a estar
    ///     muerta DURANTE la reuniÃ³n (adivinada, ejecutada, asesinada por un rol
    ///     en la reuniÃ³n...) el host revela sus notas + el aviso al instante.
    /// </summary>
    private static readonly HashSet<byte> _victimasEnReunion = new();

    /// <summary>El candado de Nota siempre está activo (hardcodeado a true).</summary>
    public static bool BloqueoNotaActivo => true;

    /// <summary>
    ///     CHAT EXTERNO CONCEDIDO (Telecom y otros): cuando otro rol/sistema
    ///     abre o re-habilita el chat de la VÃ­ctima, el candado de Nota se libera
    ///     para que pueda usar ESE chat. Se reinicia al empezar una partida.
    /// </summary>
    public static bool ChatExternoConcedido { get; private set; }

    /// <summary>Referencia al botÃ³n Nota local para reiniciar su cooldown al cerrar el chat.</summary>
    public static TownOfPlus.Buttons.Crewmate.CrewmateSupport.NotaBoton? BotonNotaLocal { get; set; }

    // â”€â”€ CONSULTAS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Notas guardadas de un jugador (copia segura, en orden de envÃ­o).</summary>
    public static List<string> NotasDe(PlayerControl? jugador)
    {
        if (jugador == null || !NotasPorJugador.TryGetValue(jugador.PlayerId, out var lista))
        {
            return new List<string>();
        }

        return lista.ToList();
    }

    /// <summary>Â¿Le queda cupo de notas a la VÃ­ctima? (0 = infinito)</summary>
    public static bool TieneCupo(PlayerControl? victima)
    {
        var limite = (int)OptionGroupSingleton<VictimaOptions>.Instance.CantidadMensajes.Value;
        if (limite <= 0)
        {
            return true; // Infinito
        }

        return NotasDe(victima).Count < limite;
    }

    /// <summary>Â¿El jugador tiene el rol VÃ­ctima? (rol actual o rol cuando estaba vivo)</summary>
    public static bool EsVictima(PlayerControl? jugador)
    {
        if (jugador?.Data == null)
        {
            return false;
        }

        try
        {
            return jugador.Data.Role is VictimaRole ||
                   jugador.GetRoleWhenAlive() is VictimaRole;
        }
        catch
        {
            return jugador.Data.Role is VictimaRole;
        }
    }

    /// <summary>Â¿La VÃ­ctima local tiene el modifier Lovers (Alliance de TOU Mira)?</summary>
    public static bool EsLoversLocal()
    {
        var local = PlayerControl.LocalPlayer;
        return local != null && local.HasModifier<LoverModifier>();
    }

    /// <summary>
    ///     Â¿La VÃ­ctima local estÃ¡ muerta (fantasma) o tiene un rol afterlife
    ///     (Cerrajero/Saboteador/Amalgama, que implementan IGhostRole)?
    ///     En ese estado puede escribir con los fantasmas.
    /// </summary>
    public static bool EsFantasmaOAfterlife()
    {
        var local = PlayerControl.LocalPlayer;
        if (local?.Data == null)
        {
            return false;
        }

        // Muerto â†’ fantasma (puede chatear con los fantasmas).
        if (local.Data.IsDead)
        {
            return true;
        }

        // Rol afterlife (los roles del mÃ¡s allÃ¡ implementan IGhostRole).
        try
        {
            return local.Data.Role is IGhostRole;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Â¿El chat debe estar LIBRE para la VÃ­ctima (sin el candado de Nota)?
    ///     True en reuniÃ³n, si es Lovers, o si es fantasma/afterlife.
    /// </summary>
    public static bool ChatLibre
    {
        get
        {
            // Partida terminada â†’ chat libre (no dejar el candado pegado).
            if (PartidaTerminada())
            {
                return true;
            }

            // ReuniÃ³n â†’ chat libre.
            if (MeetingHud.Instance != null)
            {
                return true;
            }

            // Lovers â†’ chat libre siempre (no se bloquea en juego).
            if (EsLoversLocal())
            {
                return true;
            }

            // CHAT EXTERNO (Telecom, etc.): otro rol le dio chat a la VÃ­ctima.
            // Se comprueba EN VIVO: mientras el modifier estÃ© activo el chat queda
            // libre; en cuanto el Telecom lo retire, el candado vuelve a operar.
            if (ChatExternoActivo())
            {
                return true;
            }

            // Fantasma / afterlife â†’ puede escribir con los fantasmas.
            return EsFantasmaOAfterlife();
        }
    }

    /// <summary>Â¿La partida ya terminÃ³? (game over / vuelta a resultados).</summary>
    private static bool PartidaTerminada()
    {
        try
        {
            return AmongUsClient.Instance != null &&
                   AmongUsClient.Instance.GameState == AmongUsClient.GameStates.Ended;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Â¿Cuenta como "chat externo" concedido? Detectado EN VIVO por el
    ///     modifier activo del jugador local (p. ej. TelecomChatModifier del
    ///     Divani, detectado por nombre â€” sin acoplarse a ese ensamblado), mÃ¡s
    ///     la concesiÃ³n explÃ­cita vÃ­a el hook pÃºblico (ConcederChatExterno).
    ///     Solo si la opciÃ³n de compatibilidad estÃ¡ activa (por defecto sÃ­).
    /// </summary>
    private static bool ChatExternoActivo() =>
        ChatExternoConcedido || ModifierSugiereChat();

    /// <summary>
    ///     HOOK DE INTEGRACIÃ“N (pÃºblico): cualquier rol/sistema que le conceda un
    ///     chat a la VÃ­ctima (p. ej. el Telecom) puede llamar a esto para que el
    ///     candado de Nota se libere y la VÃ­ctima pueda abrir ESE chat.
    ///     TambiÃ©n lo usa la detecciÃ³n automÃ¡tica de chats externos.
    /// </summary>
    public static void ConcederChatExterno(string motivo)
    {
        if (ChatExternoConcedido)
        {
            return;
        }

        ChatExternoConcedido = true;
        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Victima] Chat externo concedido ({motivo}): se libera el candado de Nota " +
            "y la VÃ­ctima puede usar ese chat.");

        // Re-habilitar ventana/botÃ³n de chat YA en este cliente.
        RestablecerChat();
    }

    /// <summary>Reinicia la concesiÃ³n de chat externo (nueva partida / game over).</summary>
    public static void RevocarChatExterno()
    {
        ChatExternoConcedido = false;
    }

    /// <summary>Â¿El nombre de tipo sugiere un rol/modifier que concede chat?</summary>
    private static bool NombreSugiereChat(string? nombre)
    {
        if (string.IsNullOrEmpty(nombre))
        {
            return false;
        }

        return nombre.Contains("Telecom", StringComparison.OrdinalIgnoreCase) ||
               nombre.Contains("Journalist", StringComparison.OrdinalIgnoreCase) ||
               nombre.Contains("Telecommunication", StringComparison.OrdinalIgnoreCase) ||
               nombre.Contains("Communicator", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nombre del rol (o del rol cuando estaba vivo) del jugador local.</summary>
    private static string LocalRoleName()
    {
        var local = PlayerControl.LocalPlayer;
        if (local?.Data == null)
        {
            return string.Empty;
        }

        var nombre = local.Data.Role?.GetType().Name ?? string.Empty;
        if (nombre.Length > 0)
        {
            return nombre;
        }

        try
        {
            return local.GetRoleWhenAlive()?.GetType().Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     Â¿AlgÃºn modifier activo del jugador local sugiere un chat concedido?
    ///     Puramente defensivo: ante cualquier fallo devuelve false.
    /// </summary>
    private static bool ModifierSugiereChat()
    {
        try
        {
            var local = PlayerControl.LocalPlayer;
            var componente = local?.GetModifierComponent();
            var mods = componente?.ActiveModifiers;
            if (mods == null)
            {
                return false;
            }

            foreach (var m in mods)
            {
                if (m != null && NombreSugiereChat(m.GetType().Name))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Sin acceso a los modifiers: no hay indicio.
        }

        return false;
    }

    // â”€â”€ CHAT TEMPORAL DE NOTA (solo cliente local de la VÃ­ctima) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Abre el chat temporal de nota. Devuelve false si no se pudo abrir.</summary>
    public static bool AbrirChatNota()
    {
        if (ModoNotaActivo)
        {
            return false;
        }

        // Nuevo uso vÃ¡lido de Nota: desbloquear el chat (los patches de
        // Toggle/SetVisible dejan de bloquear desde este momento).
        ChatBloqueado = false;

        try
        {
            var chat = HudManager.Instance.Chat;
            chat.gameObject.SetActive(true);

            // Solo abrir si no estÃ¡ ya abierto/abriÃ©ndose (evita que un Toggle
            // adicional cierre el chat justo despuÃ©s de abrirlo).
            if (chat.IsClosedOrClosing)
            {
                chat.Toggle();
            }

            // Forzar el campo de texto libre (por si el modo quickchat estuviera activo).
            try
            {
                if (chat.quickChatMenu != null)
                {
                    chat.quickChatMenu.gameObject.SetActive(false);
                }

                if (chat.quickChatField != null)
                {
                    chat.quickChatField.gameObject.SetActive(false);
                }

                if (chat.freeChatField != null)
                {
                    chat.freeChatField.gameObject.SetActive(true);
                    chat.freeChatField.Clear();
                    chat.freeChatField.Focus();
                }
            }
            catch
            {
                // Si el quickchat no se puede ocultar, el envÃ­o igual queda interceptado.
            }

            // Re-mostrar el botÃ³n de abrir chat para este uso de Nota
            // (se oculta al cerrar, hasta el prÃ³ximo uso vÃ¡lido).
            try
            {
                MostrarBotonChat(chat);
            }
            catch
            {
                // BotÃ³n no disponible.
            }
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Victima] No se pudo abrir el chat de nota: {ex.Message}");
            return false;
        }

        ModoNotaActivo = true;

        _textoEscribiendo = string.Empty;
        Coroutines.Start(CoVigilarTexto());

        var segundos = OptionGroupSingleton<VictimaOptions>.Instance.TiempoEscritura.Value;
        try
        {
            Helpers.CreateAndShowNotification(
                $"<b>Escribe tu nota...</b>\nTienes {segundos:0} segundos. " +
                "Lo que envÃ­es se guardarÃ¡ en secreto hasta tu muerte.",
                Color.white, new Vector3(0f, 1f, -20f),
                spr: TownOfPlus.Assets.TownofplusAssets.VictimaIcon.LoadAsset());
        }
        catch
        {
            // Sin notificaciÃ³n no pasa nada.
        }

        Coroutines.Start(CoTiempoEscritura(segundos));
        return true;
    }

    /// <summary>
    ///     Tiempo lÃ­mite de escritura: al agotarse se cierra el chat SIN
    ///     guardar nada (preferible no guardar vacÃ­o).
    /// </summary>
    private static IEnumerator CoTiempoEscritura(float segundos)
    {
        var restante = segundos;
        while (restante > 0f && ModoNotaActivo)
        {
            restante -= Time.deltaTime;
            yield return null;
        }

        // Timeout: el chat sigue abierto y nadie enviÃ³ â†’ cerrar y limpiar sin guardar.
        if (ModoNotaActivo)
        {
            CerrarChatNota(limpiarUiYa: true);
        }
    }

    /// <summary>
    ///     Cierra y DESHABILITA el chat temporal de Nota y hace desaparecer el
    ///     botÃ³n/pestaÃ±a de abrir chat hasta el prÃ³ximo uso vÃ¡lido del botÃ³n
    ///     Nota. Si no se enviÃ³ nada (timeout o cierre manual) la UI se limpia
    ///     YA; si se enviÃ³, la limpieza de la UI se pospone 1 segundo.
    /// </summary>
    public static void CerrarChatNota(bool limpiarUiYa = true)
    {
        ModoNotaActivo = false; // antes de chat.Close() para que el patch deje pasar

        // Candado: bloquea reabrir el chat hasta el prÃ³ximo uso vÃ¡lido de Nota.
        // NO se bloquea en reuniÃ³n, si es Lovers, si otro rol le dio chat
        // (Telecom), si es fantasma/afterlife o si la opciÃ³n desactiva el bloqueo.
        var bloquear = !ChatLibre && BloqueoNotaActivo;
        if (bloquear)
        {
            ChatBloqueado = true;
        }

        try
        {
            var chat = HudManager.Instance.Chat;
            if (chat == null)
            {
                return;
            }

            // Limpiar el mensaje de la UI del chat.
            if (limpiarUiYa)
            {
                LimpiarCampoEscritura(chat);
            }

            // Deshabilitar el chat (cerrar la pantalla).
            if (chat.IsOpenOrOpening)
            {
                chat.Close();
            }

            // Si se bloquea, ocultar el botÃ³n de abrir chat hasta el prÃ³ximo
            // uso de Nota; si el chat queda libre (Lovers/fantasma/afterlife),
            // mantener el botÃ³n visible para abrir/cerrar con libertad.
            if (bloquear)
            {
                OcultaBotonChat(chat);
            }
            else
            {
                MostrarBotonChat(chat);
            }
        }
        catch
        {
            // Ya cerrado o no disponible.
        }

        // El cooldown de Nota se reanuda al CERRAR el chat (enviado o no).
        ReiniciarCooldownNota();
    }

    ///     El texto NO se publica en el chat (ni general ni privado): solo se
    ///     guarda en la lista interna privada. Nunca se guardan notas vacÃ­as.
    /// </summary>
    public static void ConfirmarNotaOCancelar(string texto)
    {
        var escribiendo = ModoNotaActivo;
        var textoLimpio = (texto ?? string.Empty).Trim();

        // Cerrar el modo Nota y limpiar el campo de escritura del chat.
        CerrarChatNota(limpiarUiYa: true);

        if (!escribiendo || string.IsNullOrEmpty(textoLimpio))
        {
            return; // Nada que guardar: no se guardan notas vacÃ­as.
        }

        // SOLO guardar en la lista interna privada (fuente de verdad) y sync
        // por RPC. No queda nada visible en el chat.
        VictimaRpc.EnviarNota(PlayerControl.LocalPlayer, textoLimpio);
    }

    /// <summary>
    ///     Devuelve el texto que la VÃ­ctima tiene escrito. Prioriza el campo
    ///     real; si el flujo vanilla ya lo limpiÃ³, usa el buffer capturado.
    /// </summary>
    public static string TomarTextoActual(ChatController chat)
    {
        try
        {
            var t = chat?.freeChatField?.Text ?? string.Empty;
            if (!string.IsNullOrEmpty(t))
            {
                _textoEscribiendo = t;
                return t;
            }
        }
        catch
        {
            // Seguir con el buffer.
        }

        return _textoEscribiendo;
    }

    /// <summary>
    ///     Vigila el campo de escritura mientras el modo nota estÃ¡ activo y
    ///     mantiene el buffer actualizado (para poder confirmar con ENTER).
    /// </summary>
    private static IEnumerator CoVigilarTexto()
    {
        while (ModoNotaActivo)
        {
            try
            {
                var c = HudManager.Instance?.Chat;
                var t = c?.freeChatField?.Text;
                if (!string.IsNullOrEmpty(t))
                {
                    _textoEscribiendo = t;
                }
            }
            catch
            {
                // Mantener el Ãºltimo texto conocido.
            }

            yield return null;
        }
    }

    /// <summary>Limpia el campo de escritura del chat (mensaje de la UI).</summary>
    private static void LimpiarCampoEscritura(ChatController chat)
    {
        _textoEscribiendo = string.Empty;

        try
        {
            if (chat.freeChatField != null)
            {
                chat.freeChatField.Clear();
            }
        }
        catch
        {
            // Sin campo no pasa nada.
        }
    }

    /// <summary>Oculta el botÃ³n de abrir chat (pestaÃ±a) hasta el prÃ³ximo uso de Nota.</summary>
    private static void OcultaBotonChat(ChatController chat)
    {
        try
        {
            if (chat.chatButton != null)
            {
                chat.chatButton.gameObject.SetActive(false);
            }

            if (chat.quickChatButton != null)
            {
                chat.quickChatButton.gameObject.SetActive(false);
            }

            if (chat.openKeyboardButton != null)
            {
                chat.openKeyboardButton.gameObject.SetActive(false);
            }
        }
        catch
        {
            // BotÃ³n ya oculto o no disponible.
        }
    }

    /// <summary>Re-muestra el botÃ³n de chat al abrir un nuevo uso de Nota.</summary>
    private static void MostrarBotonChat(ChatController chat)
    {
        try
        {
            if (chat.chatButton != null)
            {
                chat.chatButton.gameObject.SetActive(true);
            }

            if (chat.quickChatButton != null)
            {
                chat.quickChatButton.gameObject.SetActive(true);
            }
        }
        catch
        {
            // BotÃ³n ya visible o no disponible.
        }
    }

    /// <summary>Borrar el mensaje de la UI del chat tras un breve retardo (tras enviar).</summary>
    private static IEnumerator CoLimpiarCampo(float segundos)
    {
        yield return new WaitForSeconds(segundos);

        try
        {
            LimpiarCampoEscritura(HudManager.Instance.Chat);
        }
        catch
        {
            // Sin chat no pasa nada.
        }
    }

    private static void ReiniciarCooldownNota()
    {
        var boton = BotonNotaLocal;
        if (boton == null)
        {
            return;
        }

        try
        {
            boton.Timer = OptionGroupSingleton<VictimaOptions>.Instance.CooldownNota.Value;
        }
        catch
        {
            // BotÃ³n no inicializado todavÃ­a.
        }
    }

    // â”€â”€ GUARDADO DE NOTAS (todos los clientes, vÃ­a RPC) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    ///     Guarda UNA nota en la lista persistente en memoria de la vÃ­ctima
    ///     (List&lt;string&gt; en orden de envÃ­o). NUNCA se limpia entre
    ///     reuniones ni al cerrar el chat (solo al cambiar de partida). El
    ///     guardado es independiente de la ventana/UI del chat.
    /// </summary>
    public static void GuardarNota(PlayerControl? victima, string nota)
    {
        if (victima == null || string.IsNullOrEmpty(nota))
        {
            return;
        }

        if (!NotasPorJugador.TryGetValue(victima.PlayerId, out var lista))
        {
            lista = new List<string>();
            NotasPorJugador[victima.PlayerId] = lista;
        }

        // Respetar el lÃ­mite configurado (0 = infinito).
        var limite = (int)OptionGroupSingleton<VictimaOptions>.Instance.CantidadMensajes.Value;
        if (limite > 0 && lista.Count >= limite)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                $"[Victima] Nota de {victima.Data?.PlayerName} DESCARTADA por lÃ­mite ({lista.Count}).");
            return;
        }

        lista.Add(nota);

        // LOG TEMPORAL (BepInEx): verificar cuÃ¡ntas notas hay en la lista.
        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Victima] NOTA GUARDADA de {victima.Data?.PlayerName}: \"{nota}\" â†’ " +
            $"la lista tiene ahora {lista.Count} nota(s).");
    }

    // â”€â”€ MUERTE / EXPULSIÃ“N Y REVELACIÃ“N â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    ///     RESET DE PARTIDA. Se llama SOLO al empezar una partida nueva
    ///     (IntroBeginEvent) y al terminar la partida (GameEndEvent). NUNCA se
    ///     llama de forma perezosa a mitad de partida: antes se usaba
    ///     GameManager.Instance como "identidad de partida", pero su referencia
    ///     cambia durante la partida (reuniones/exilios) y BORRABA las notas
    ///     guardadas â†’ "no llegÃ³ a escribir ninguna nota". Las notas persisten
    ///     toda la partida y SOLO se reinician aquÃ­.
    /// </summary>
    public static void ReiniciarPartida()
    {
        NotasPorJugador.Clear();
        _revelacionPendiente = false;
        _victimaPendiente = 0;
        _reveladas.Clear();
        _victimasEnReunion.Clear();
        ChatExternoConcedido = false;
        ChatBloqueado = false;

        // Nueva partida: el chat vuelve a estar libre (no dejar el
        // bloqueo de Nota pegado entre partidas).
        RestablecerChat();

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            "[Victima] Reset de partida: notas y estado limpiados.");
    }

    /// <summary>
    ///     La VÃ­ctima muriÃ³ (kill en ronda) o fue expulsada: la revelaciÃ³n de
    ///     sus notas queda PENDIENTE para la PRÃ“XIMA reuniÃ³n. Solo el host
    ///     marca el pendiente y dispara el RPC de revelaciÃ³n.
    /// </summary>
    public static void RegistrarMuerte(PlayerControl? victima)
    {
        // Se reconoce a la vÃ­ctima por rol vivo O por tener notas guardadas:
        // asÃ­, aunque el chequeo del rol cuando estaba vivo falle al estar
        // muerta, TODAS sus notas acumuladas se revelan igualmente.
        if (victima == null || (!EsVictima(victima) && NotasDe(victima).Count == 0))
        {
            return;
        }

        // Ya revelada (muerte en reuniÃ³n / expulsiÃ³n): no se repite.
        if (_reveladas.Contains(victima.PlayerId))
        {
            return;
        }

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        _victimaPendiente = victima.PlayerId;
        _revelacionPendiente = true;

        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Victima] Muerte/expulsiÃ³n registrada: {NotasDe(victima).Count} nota(s) " +
            "acumuladas; la revelaciÃ³n ocurrirÃ¡ en la PRÃ“XIMA reuniÃ³n.");
    }

    /// <summary>
    ///     MUERTE DENTRO DE UNA REUNIÃ“N. En una reuniÃ³n un jugador puede morir
    ///     sin ser expulsado: adivinado/acusado correctamente (roles de
    ///     adivinaciÃ³n tipo Assassin/Guesser), EJECUTADO por un rol que mata en
    ///     la reuniÃ³n (carcelero/verdugo y similares), o asesinado por cualquier
    ///     habilidad de reuniÃ³n. En TODOS esos casos se revelan YA sus notas y el
    ///     aviso de que era la VÃ­ctima, sin esperar a la prÃ³xima reuniÃ³n.
    /// </summary>
    public static void MuerteEnReunion(PlayerControl? victima) =>
        RevelarInmediato(victima, "muriÃ³ dentro de la reuniÃ³n (adivinada/ejecutada)");

    /// <summary>
    ///     Revela INMEDIATAMENTE (host, RPC a todos) las notas + el aviso de rol
    ///     de una VÃ­ctima que acaba de morir (reuniÃ³n o expulsiÃ³n). Idempotente:
    ///     cada VÃ­ctima se revela una sola vez por partida. Si no llegÃ³ a
    ///     escribir notas, se revela igualmente el aviso de que era la VÃ­ctima.
    /// </summary>
    public static void RevelarInmediato(PlayerControl? victima, string motivo)
    {
        if (victima?.Data == null)
        {
            return;
        }

        // Se reconoce por rol vivo O por notas guardadas (rol ya perdido).
        if (!EsVictima(victima) && NotasDe(victima).Count == 0)
        {
            return;
        }

        // Una sola revelaciÃ³n por VÃ­ctima y partida.
        if (!_reveladas.Add(victima.PlayerId))
        {
            return;
        }

        // Si habÃ­a una revelaciÃ³n pendiente para la prÃ³xima reuniÃ³n, ya no hace
        // falta (se revela ahora).
        if (_victimaPendiente == victima.PlayerId)
        {
            _revelacionPendiente = false;
            _victimaPendiente = 0;
        }

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        var notas = NotasDe(victima);
        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Victima] REVELACIÃ“N INMEDIATA ({motivo}): {notas.Count} nota(s) de " +
            $"{victima.Data.PlayerName} â†’ se envÃ­an a todos.");

        VictimaRpc.EnviarRevelacion(victima, notas);
    }

    /// <summary>
    ///     Al EMPEZAR la reuniÃ³n: recuerda las VÃ­ctimas VIVAS que participan. Si
    ///     alguna muere durante la reuniÃ³n, el watchdog (ActualizarReunion) la
    ///     detecta y revela sus notas al instante.
    /// </summary>
    public static void ConfigurarVigilanciaReunion()
    {
        _victimasEnReunion.Clear();

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p?.Data == null || p.Data.IsDead || p.Data.Disconnected)
            {
                continue;
            }

            if (EsVictima(p))
            {
                _victimasEnReunion.Add(p.PlayerId);
            }
        }

        if (_victimasEnReunion.Count > 0)
        {
            TownOfPlusPlugin.Instance?.Log.LogInfo(
                $"[Victima] Vigilando {_victimasEnReunion.Count} VÃ­ctima(s) viva(s) " +
                "durante esta reuniÃ³n (muerte en reuniÃ³n = revelaciÃ³n inmediata).");
        }
    }

    /// <summary>
    ///     TICK por frame durante la reuniÃ³n: si una VÃ­ctima que estaba viva al
    ///     empezar la reuniÃ³n aparece MUERTA (sin haber sido expulsada), el host
    ///     revela sus notas + el aviso al instante. Cubre cualquier vÃ­a de muerte
    ///     en reuniÃ³n (adivinada, ejecutada, asesinada por un rol) incluso si esa
    ///     vÃ­a no dispara el evento de muerte estÃ¡ndar.
    /// </summary>
    public static void ActualizarReunion()
    {
        if (_victimasEnReunion.Count == 0)
        {
            return;
        }

        // La reuniÃ³n terminÃ³: nada mÃ¡s que vigilar (la expulsiÃ³n la maneja
        // ExileController.WrapUp).
        if (MeetingHud.Instance == null)
        {
            _victimasEnReunion.Clear();
            return;
        }

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        foreach (var id in _victimasEnReunion.ToArray())
        {
            var victima = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p.PlayerId == id);

            if (victima?.Data == null)
            {
                _victimasEnReunion.Remove(id);
                continue;
            }

            if (!victima.Data.IsDead)
            {
                continue;
            }

            _victimasEnReunion.Remove(id);
            MuerteEnReunion(victima);
        }
    }

    /// <summary>
    ///     ExpulsiÃ³n de la VÃ­ctima: al FINALIZAR la animaciÃ³n de expulsiÃ³n
    ///     (ExileController.WrapUp) el host revela YA todas sus notas a todos,
    ///     sin esperar a la prÃ³xima reuniÃ³n.
    /// </summary>
    public static void AlTerminarExilio()
    {
        // Si la VÃ­ctima LOCAL fue exiliada â†’ desbloquear su chat (fantasma)
        // para que pueda escribir con los fantasmas. Corre en todos los clientes.
        try
        {
            var local = PlayerControl.LocalPlayer;
            var exiliado = ExileController.Instance?.initData?.networkedPlayer?.Object;
            if (local != null && exiliado != null && exiliado.PlayerId == local.PlayerId)
            {
                RestablecerChat();
            }
        }
        catch
        {
            // Sin chat no pasa nada.
        }

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        var exiliadoHost = ExileController.Instance?.initData?.networkedPlayer?.Object;
        if (exiliadoHost == null || (!EsVictima(exiliadoHost) && NotasDe(exiliadoHost).Count == 0))
        {
            return;
        }

        // RevelaciÃ³n inmediata (notas + aviso de que era la VÃ­ctima) al acabar la
        // animaciÃ³n de expulsiÃ³n. Idempotente: no repite si ya se revelÃ³.
        RevelarInmediato(exiliadoHost, "expulsiÃ³n/votaciÃ³n terminada");
    }

    /// <summary>Solo la VÃ­ctima puede abrir/cerrar el chat DURANTE la reuniÃ³n.</summary>
    private static void RehabilitarChatReunion()
    {
        ChatBloqueado = false;

        try
        {
            var chat = HudManager.Instance?.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.chatButton != null)
            {
                chat.chatButton.gameObject.SetActive(true);
            }

            if (chat.quickChatButton != null)
            {
                chat.quickChatButton.gameObject.SetActive(true);
            }
        }
        catch
        {
            // BotÃ³n no disponible: no bloquea el uso del chat en reuniÃ³n.
        }
    }

    /// <summary>
    ///     Restaura el chat normal de la VÃ­ctima: desbloquea el candado de Nota,
    ///     cancela el modo nota y re-muestra el botÃ³n de chat. Se usa al terminar
    ///     la partida (game over / vuelta al lobby), al empezar una nueva partida
    ///     y cuando la VÃ­ctima pasa a ser fantasma o rol afterlife.
    /// </summary>
    public static void RestablecerChat()
    {
        ChatBloqueado = false;
        ModoNotaActivo = false;

        try
        {
            var chat = HudManager.Instance?.Chat;
            if (chat == null)
            {
                return;
            }

            MostrarBotonChat(chat);
        }
        catch
        {
            // Sin chat no pasa nada.
        }
    }

    /// <summary>
    ///     Inicio de reuniÃ³n: se arma la VIGILANCIA (si una VÃ­ctima viva muere
    ///     durante la reuniÃ³n â€” adivinada, ejecutada... â€” se revelan sus notas al
    ///     instante) y el host revela las notas PENDIENTES (kill en ronda).
    /// </summary>
    public static void AlComenzarReunion()
    {
        // En reuniÃ³n el chat es libre para la VÃ­ctima: re-habilitar botÃ³n/ventana.
        RehabilitarChatReunion();

        // Vigilancia de muertes DENTRO de esta reuniÃ³n.
        ConfigurarVigilanciaReunion();

        if (!_revelacionPendiente)
        {
            return;
        }

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        var victima = PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p.PlayerId == _victimaPendiente);
        var notas = NotasDe(victima);

        _revelacionPendiente = false;
        _victimaPendiente = 0;

        if (victima == null)
        {
            return;
        }

        // LOG TEMPORAL (BepInEx): verificar cuÃ¡ntas se van a revelar.
        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Victima] REVELACIÃ“N: {notas.Count} nota(s) acumuladas de " +
            $"{victima.Data?.PlayerName} â†’ se enviarÃ¡n a todos.");

        VictimaRpc.EnviarRevelacion(victima, notas);
    }

    /// <summary>
    ///     RevelaciÃ³n local (ejecutada en TODOS los clientes por el RPC):
    ///     alerta tipo afterlife + burbuja negra con las notas en orden.
    /// </summary>
    public static void RevelarLocal(PlayerControl? victima, List<string> notas)
    {
        if (victima == null)
        {
            return;
        }

        notas ??= new List<string>();

        var nombre = victima.Data?.PlayerName ?? "VÃ­ctima";
        var colorHex = ColorUtility.ToHtmlStringRGB(VictimaRole.VictimaColor);

        // LOG TEMPORAL (BepInEx): verificar la cantidad revelada en cada cliente.
        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Victima] REVELACIÃ“N LOCAL: {notas.Count} nota(s) de {nombre} â†’ baja la burbuja.");

        // 1) AVISO DE QUE ERA LA VÃCTIMA (icono del rol). Se muestra SIEMPRE,
        //    incluso si no llegÃ³ a escribir ninguna nota.
        try
        {
            var cuerpo = notas.Count > 0
                ? "Pero ha dejado atras sus notas..."
                : "No llego a escribir ninguna nota.";

            Helpers.CreateAndShowNotification(
                $"<color=#{colorHex}><b>{nombre} era la Vi­ctima</b></color>\n" +
                $"<color=#FFFFFF>{cuerpo}</color>",
                Color.white, new Vector3(0f, 1f, -20f),
                spr: TownOfPlus.Assets.TownofplusAssets.VictimaIcon.LoadAsset());
        }
        catch
        {
            // Sin alerta no pasa nada.
        }

        // 2) Burbuja negra con texto de colores (estilo Dictador) SOLO si hay
        //    notas que mostrar.
        if (notas.Count > 0)
        {
            Coroutines.Start(CoMostrarNotas(nombre, colorHex, notas));
        }
    }

    private static IEnumerator CoMostrarNotas(string nombre, string colorHex, List<string> notas)
    {
        // PequeÃ±o margen para que la reuniÃ³n se haya asentado.
        yield return new WaitForSeconds(1f);

        var alto = 80f + 34f * notas.Count;
        var ancho = 560f;

        // Canvas propio overlay para que sea visible sobre la reuniÃ³n.
        var canvasGo = new GameObject("VictimaNotasRevelacion");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Por encima del HUD y de la reuniÃ³n (la revelaciÃ³n puede salir en plena
        // reuniÃ³n: muerte en reuniÃ³n / expulsiÃ³n).
        canvas.sortingOrder = 1000;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Burbuja negra (imagen de fondo).
        var burbujaGo = new GameObject("Burbuja");
        burbujaGo.transform.SetParent(canvasGo.transform, false);
        var burbuja = burbujaGo.AddComponent<UnityEngine.UI.Image>();
        burbuja.color = new Color(0f, 0f, 0f, 0.85f); // burbuja negra
        var rect = burbuja.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ancho, alto);

        // Texto de colores (estilo Dictador) en un hijo.
        var txtGo = new GameObject("Texto");
        txtGo.transform.SetParent(burbujaGo.transform, false);
        var texto = txtGo.AddComponent<TMPro.TextMeshProUGUI>();
        texto.fontSize = 20f;
        texto.alignment = TMPro.TextAlignmentOptions.Center;
        texto.color = Color.white;
        var trect = texto.rectTransform;
        trect.anchorMin = trect.anchorMax = new Vector2(0.5f, 0.5f);
        trect.pivot = new Vector2(0.5f, 0.5f);
        trect.sizeDelta = new Vector2(ancho - 40f, alto - 20f);
        trect.localPosition = Vector3.zero;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<color=#{colorHex}><b>Notas:</b></color>");
        foreach (var nota in notas)
        {
            sb.AppendLine($"<color=#{colorHex}>{nombre}</color>: {nota}");
        }

        texto.text = sb.ToString();

        // Desaparece sola tras 9 segundos.
        Object.Destroy(canvasGo, 9f);
    }
}