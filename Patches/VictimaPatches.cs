using HarmonyLib;
using TownOfPlus.Modules;
using UnityEngine;

namespace TownOfPlus.Patches;

/// <summary>
///     Intercepción de TODOS los caminos de envío de chat mientras la Víctima
///     escribe su NOTA: el texto NUNCA se publica en el chat general/partida.
///     Se captura 1 mensaje por uso hacia la lista interna de notas y el chat
///     temporal se cierra de inmediato. Una ventana anti-doble-captura evita
///     guardar el mismo envío dos veces al parchear varios métodos del flujo.
/// </summary>
public static class VictimaChatCaptura
{
    /// <summary>Ventana anti-doble-captura para no guardar el mismo envío dos veces.</summary>
    internal static float UltimaCaptura = -999f;

    /// <summary>
    ///     Devuelve true = pasar al chat vanilla; false = NUNCA publicar.
    /// </summary>
    internal static bool CapturarNota(ChatController chat)
    {
        // En REUNIÓN el chat es el chat normal (no es el flujo de Nota en
        // ronda): no se captura nada, no se guarda nota, no se gasta Nota.
        if (MeetingHud.Instance != null && !VictimaManager.ModoNotaActivo)
        {
            return true;
        }

        var activo = VictimaManager.ModoNotaActivo || Time.time - UltimaCaptura < 0.3f;
        if (!activo)
        {
            return true; // Fuera del modo nota → chat normal.
        }

        UltimaCaptura = Time.time;
        var texto = VictimaManager.TomarTextoActual(chat);
        VictimaManager.ConfirmarNotaOCancelar(texto);
        return false; // NUNCA llegar al chat de la partida.
    }
}

/// <summary>Submit del chat libre (punto principal de envío al pulsar Enter).</summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.OnFreeChatSubmit))]
public static class VictimaOnFreeChatSubmitPatch
{
    public static bool Prefix(ChatController __instance)
    {
        return VictimaChatCaptura.CapturarNota(__instance);
    }
}

/// <summary>Segundo punto de envío del chat libre (free chat).</summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendFreeChat))]
public static class VictimaSendFreeChatPatch
{
    public static bool Prefix(ChatController __instance)
    {
        return VictimaChatCaptura.CapturarNota(__instance);
    }
}

/// <summary>Punto base de envío (SendChat).</summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
public static class VictimaSendChatPatch
{
    public static bool Prefix(ChatController __instance)
    {
        return VictimaChatCaptura.CapturarNota(__instance);
    }
}

/// <summary>Submit del quick-chat (seguridad extra: no debe publicar nunca).</summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.OnQuickChatSubmit))]
public static class VictimaOnQuickChatSubmitPatch
{
    public static bool Prefix(ChatController __instance)
    {
        return VictimaChatCaptura.CapturarNota(__instance);
    }
}

/// <summary>Quick-chat (seguridad extra: no debe publicar nunca).</summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendQuickChat))]
public static class VictimaSendQuickChatPatch
{
    public static bool Prefix(ChatController __instance)
    {
        return VictimaChatCaptura.CapturarNota(__instance);
    }
}

/// <summary>
///     Cierre manual del chat (botón atrás / tecla) durante el modo nota:
///     se cancela y se limpia la UI sin guardar (nunca se guardan vacíos).
/// </summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.Close))]
public static class VictimaChatClosePatch
{
    public static void Prefix()
    {
        // Si el cierre viene de VictimaManager (que desactiva el modo antes
        // de llamar a Close), aquí ya no está activo y no se hace nada.
        if (VictimaManager.ModoNotaActivo)
        {
            VictimaManager.CerrarChatNota(limpiarUiYa: true);
        }
    }
}

/// <summary>
///     Al finalizar la animación de expulsión (WrapUp): si la Víctima fue
///     expulsada, el host revela YA todas sus notas a todos, sin esperar a la
///     próxima reunión. (El resto de WrapUp del rol/Afterlife sigue intacto.)
/// </summary>
[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
public static class VictimaExilioWrapUpPatch
{
    public static void Postfix()
    {
        try
        {
            VictimaManager.AlTerminarExilio();
        }
        catch
        {
            // Nunca romper el WrapUp por un error del rol.
        }
    }
}
/// <summary>
///     Bloquea reabrir el chat de Nota en RONDA tras enviar / agotar el tiempo
///     (chat D E S C A N D A D O): mientras VictimaManager.ChatBloqueado esté
///     activo y no se esté escribiendo una nueva nota, Toggle no puede volver a
///     abrir la ventana. Devuelve false = se descarta la acción vanilla.
///     El chat queda LIBRE en reunión, si la Víctima es Lovers, si otro rol le
///     dio chat (Telecom → VictimaManager.ChatExternoConcedido), o si es
///     fantasma/rol afterlife (puede escribir con los fantasmas).
/// </summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.Toggle))]
public static class VictimaChatTogglePatch
{
    public static bool Prefix()
    {
        // Chat libre (reunión / Lovers / fantasma-afterlife) → permitir.
        // Ronda → respetar el candado de Nota.
        return VictimaManager.ChatLibre ||
               !VictimaManager.ChatBloqueado || VictimaManager.ModoNotaActivo;
    }
}

/// <summary>
///     Mismo candado sobre SetVisible: no se puede volver a mostrar la ventana
///     de chat en ronda hasta el próximo uso válido de Nota. Libre en reunión,
///     si es Lovers, si otro rol le dio chat (Telecom → ChatExternoActivo,
///     detectado por el modifier activo), si es fantasma/rol afterlife o si la
///     partida ya terminó.
/// </summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.SetVisible))]
public static class VictimaChatSetVisiblePatch
{
    public static bool Prefix(bool visible)
    {
        if (!visible)
        {
            return true; // Ocultar siempre está permitido.
        }

        // Chat libre (partida terminada / reunión / Lovers / Telecom /
        // fantasma-afterlife) → permitir. Ronda → respetar el candado de Nota.
        return VictimaManager.ChatLibre ||
               !VictimaManager.ChatBloqueado || VictimaManager.ModoNotaActivo;
    }
}

/// <summary>
///     Motor del rol Víctima: corre UNA vez por frame desde HudManager.Update.
///     <para>· Muerte en reunión: si una Víctima que estaba viva al empezar la
///     reunión aparece muerta (adivinada, ejecutada, asesinada por un rol en la
///     reunión), el host revela sus notas + el aviso de que era la Víctima al
///     instante.</para>
///     <para>· Chat externo (Telecom): detecta si otro sistema le habilitó el
///     chat y libera el candado de Nota.</para>
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class VictimaMotorPatch
{
    public static void Postfix()
    {
        try
        {
            VictimaManager.ActualizarReunion();
        }
        catch
        {
            // Un tick del rol nunca debe romper el HUD.
        }
    }
}