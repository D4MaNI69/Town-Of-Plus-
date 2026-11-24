using System.Collections;
using System.Linq;
using MiraAPI.Modifiers;
using PowerTools;
using Reactor.Utilities;
using TownOfPlus.Roles.Impostor.ImpostorPower;
using TownOfUs;
using TownOfUs.Assets;
using TownOfUs.Modifiers.Game;
using TownOfUs.Modules.RainbowMod;
using TownOfUs.Roles.Crewmate;
using TownOfUs.Utilities;
using TownOfPlus.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfPlus.Modules;

/// <summary>
///     Animación de revelación del Dictador: copia EXACTA del flujo del Mayor
///     de TOU Mira (MayorRole.CoAnimateReveal / CoAnimatePostReveal):
///     prefab transitorio del AssetBundle (TouAssets.MayorRevealPrefab),
///     material + RainbowBehaviour en cuerpo y manos con el color del jugador,
///     sonido TouAudio.MayorRevealSound, flash y velocidad 1.02; al terminar se
///     destruye el prefab transitorio y se coloca el look persistente
///     (MayorPostRevealPrefab). No se superpone con el sprite original porque
///     el PlayerIcon se oculta mientras el prefab está activo.
/// </summary>
public static class DictadorManager
{
    /// <summary>Look persistente post-reveal (para limpieza entre reuniones).</summary>
    private static GameObject? _postReveal;

    /// <summary>
    ///     ¿Se reveló el MAYOR (TOU Mira) en esta partida? Regla MAYOR + DICTADOR:
    ///     cuando el Mayor se revela, el Dictador puede revelarse SIN cumplir el
    ///     requisito de votos en contra, en esa misma reunión y en cualquiera
    ///     posterior. Se pone por el patch del RPC real del Mayor
    ///     (<see cref="Patches.DictadorMayorRevealPatch"/>) en todos los clientes.
    /// </summary>
    private static bool _mayorRevelado;

    /// <summary>
    ///     ¿Hay un Mayor revelado? Combina el aviso por RPC con la comprobación
    ///     EN VIVO del estado sincronizado del Mayor (así un cliente que se
    ///     reconecte después de la revelación sigue viendo el desbloqueo).
    /// </summary>
    public static bool MayorRevelado => _mayorRevelado || HayMayorReveladoVivo();

    /// <summary>Reinicia el desbloqueo por Mayor al empezar una partida nueva.</summary>
    public static void ReiniciarPartida()
    {
        _mayorRevelado = false;

        if (_postReveal != null)
        {
            try
            {
                Object.Destroy(_postReveal);
            }
            catch
            {
                // Ya destruido.
            }

            _postReveal = null;
        }
    }

    /// <summary>
    ///     Marca que el Mayor se ha revelado (lo llama el patch del RPC del Mayor).
    ///     NO se puede llamar con un Dictador: su revelación usa su propio RPC y
    ///     solo IMITA la animación del Mayor.
    /// </summary>
    public static void MarcarMayorRevelado(PlayerControl? mayor)
    {
        if (_mayorRevelado)
        {
            return;
        }

        _mayorRevelado = true;
        TownOfPlusPlugin.Instance?.Log.LogInfo(
            $"[Dictador] El Mayor ({mayor?.Data?.PlayerName ?? "?"}) se reveló: " +
            "el Dictador queda desbloqueado para revelarse sin votos en contra.");
    }

    /// <summary>¿Algún Mayor de la partida tiene ya el estado REVELADO (sincronizado)?</summary>
    private static bool HayMayorReveladoVivo()
    {
        try
        {
            return PlayerControl.AllPlayerControls.ToArray()
                .Any(p => p?.Data?.Role is MayorRole { Revealed: true });
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Revelación completa (primera vez): animación transitoria + sonido + look persistente.</summary>
    public static void Revelar(PlayerVoteArea voteArea, PlayerControl jugador)
    {
        Coroutines.Start(CoAnimarReveal(voteArea, jugador));
    }

    /// <summary>Reuniones posteriores: muestra directamente el look persistente de Mayor.</summary>
    public static void AnimarPostReveal(PlayerControl jugador)
    {
        var meeting = MeetingHud.Instance;
        if (meeting == null || jugador == null)
        {
            return;
        }

        var voteArea = meeting.playerStates.FirstOrDefault(x => x.PlayerId == jugador.PlayerId);
        if (voteArea != null)
        {
            Coroutines.Start(CoPostReveal(voteArea));
        }
    }

    /// <summary>
    ///     Copia exacta de MayorRole.CoAnimateReveal de TOU Mira, sustituyendo el
    ///     rol destino por DictadorRole.
    /// </summary>
    public static IEnumerator CoAnimarReveal(PlayerVoteArea voteArea, PlayerControl jugador)
    {
        if (Minigame.Instance)
        {
            Minigame.Instance.Close();
            Minigame.Instance.Close();
        }

        // Limpiar cualquier post-reveal anterior.
        LimpiarPostReveal(voteArea);

        var mayorPlayer = Object.Instantiate(TouAssets.MayorRevealPrefab.LoadAsset(), voteArea.transform);
        mayorPlayer.transform.localPosition = new Vector3(-0.8f, 0, 0);
        mayorPlayer.transform.localScale = new Vector3(0.375f, 0.375f, 1f);
        mayorPlayer.gameObject.layer = mayorPlayer.transform.GetChild(0).gameObject.layer = voteArea.gameObject.layer;

        var animationRend = mayorPlayer.GetComponent<SpriteRenderer>();
        animationRend.material = voteArea.PlayerIcon.cosmetics.currentBodySprite.BodySprite.material;
        var r = animationRend.gameObject.GetComponent<RainbowBehaviour>()
             ?? animationRend.gameObject.AddComponent<RainbowBehaviour>();
        r.AddRend(animationRend, voteArea.PlayerIcon.ColorId);

        var handRend = BuscarHijo(mayorPlayer.transform, "Hands")?.GetComponent<SpriteRenderer>();
        handRend ??= BuscarHijo(mayorPlayer.transform, "Hand")?.GetComponent<SpriteRenderer>();
        if (handRend != null)
        {
            handRend.material = voteArea.PlayerIcon.cosmetics.currentBodySprite.BodySprite.material;
            var r2 = handRend.gameObject.GetComponent<RainbowBehaviour>()
                  ?? handRend.gameObject.AddComponent<RainbowBehaviour>();
            r2.AddRend(handRend, voteArea.PlayerIcon.ColorId);
        }

        voteArea.PlayerIcon.gameObject.SetActive(false);
        mayorPlayer.gameObject.SetActive(true);
        mayorPlayer.transform.GetChild(0).gameObject.SetActive(true);
        mayorPlayer.transform.GetChild(1).gameObject.SetActive(true);

        Coroutines.Start(MiscUtils.CoFlash(TownOfUsColors.Mayor, 0.15f, 0.15f));

        var bodysAnim = mayorPlayer.GetComponent<SpriteAnim>();
        var outfitAnim = mayorPlayer.transform.GetChild(0).GetComponent<SpriteAnim>();
        var handAnim = mayorPlayer.transform.GetChild(1).GetComponent<SpriteAnim>();
        bodysAnim.SetSpeed(1.02f);
        outfitAnim.SetSpeed(1.02f);
        handAnim.SetSpeed(1.02f);
        AudioTop.Sonar(TouAudio.MayorRevealSound);
        yield return new WaitForSeconds(0.1f);
        if (jugador != null && jugador.Data.Role is DictadorRole dictador)
        {
            dictador.Revealed = true;
        }

        yield return new WaitForSeconds(bodysAnim.m_currAnim.length - 0.25f);

        // Fin de la animación transitoria: destruir y pasar al look persistente
        // (igual que MayorRole.DestroyReveal + CoAnimatePostReveal).
        mayorPlayer.gameObject.SetActive(false);
        voteArea.PlayerIcon.gameObject.SetActive(true);
        Object.Destroy(mayorPlayer);

        yield return CoPostReveal(voteArea);
    }

    /// <summary>Copia exacta de MayorRole.CoAnimatePostReveal de TOU Mira.</summary>
    private static IEnumerator CoPostReveal(PlayerVoteArea voteArea)
    {
        LimpiarPostReveal(voteArea);

        _postReveal = Object.Instantiate(TouAssets.MayorPostRevealPrefab.LoadAsset(), voteArea.transform);
        _postReveal.transform.localPosition = new Vector3(-0.8f, 0, 0);
        _postReveal.transform.localScale = new Vector3(0.375f, 0.375f, 1f);
        _postReveal.gameObject.layer = _postReveal.transform.GetChild(0).gameObject.layer = voteArea.gameObject.layer;

        var animationRend = _postReveal.GetComponent<SpriteRenderer>();
        animationRend.material = voteArea.PlayerIcon.cosmetics.currentBodySprite.BodySprite.material;
        var r = animationRend.gameObject.GetComponent<RainbowBehaviour>()
             ?? animationRend.gameObject.AddComponent<RainbowBehaviour>();
        r.AddRend(animationRend, voteArea.PlayerIcon.ColorId);

        var handRend = BuscarHijo(_postReveal.transform, "Hands")?.GetComponent<SpriteRenderer>();
        handRend ??= BuscarHijo(_postReveal.transform, "Hand")?.GetComponent<SpriteRenderer>();
        if (handRend != null)
        {
            handRend.material = voteArea.PlayerIcon.cosmetics.currentBodySprite.BodySprite.material;
            var r2 = handRend.gameObject.GetComponent<RainbowBehaviour>()
                  ?? handRend.gameObject.AddComponent<RainbowBehaviour>();
            r2.AddRend(handRend, voteArea.PlayerIcon.ColorId);
        }

        voteArea.PlayerIcon.gameObject.SetActive(false);
        _postReveal.gameObject.SetActive(true);
        _postReveal.transform.GetChild(0).gameObject.SetActive(true);
        _postReveal.transform.GetChild(1).gameObject.SetActive(true);
        yield return new WaitForSeconds(0.01f);
    }

    /// <summary>Elimina el look persistente anterior y restaura el PlayerIcon.</summary>
    private static void LimpiarPostReveal(PlayerVoteArea voteArea)
    {
        if (_postReveal == null)
        {
            return;
        }

        _postReveal.gameObject.SetActive(false);
        Object.Destroy(_postReveal);
        _postReveal = null;
        if (voteArea != null && voteArea.PlayerIcon != null)
        {
            voteArea.PlayerIcon.gameObject.SetActive(true);
        }
    }

    /// <summary>Busca un hijo por nombre de forma recursiva (reemplazo de FindRecursive).</summary>
    public static Transform? BuscarHijo(Transform padre, string nombre)
    {
        if (padre.name == nombre)
        {
            return padre;
        }

        for (var i = 0; i < padre.childCount; i++)
        {
            var resultado = BuscarHijo(padre.GetChild(i), nombre);
            if (resultado != null)
            {
                return resultado;
            }
        }

        return null;
    }

    /// <summary>
    ///     ¿Es aliado del Dictador? (impostores del equipo y Alliance vinculado a
    ///     impostores / amante impostor). Se usa para excluir sus votos al contar.
    /// </summary>
    public static bool EsAliado(PlayerControl? p)
    {
        if (p?.Data == null || p.Data.Disconnected)
        {
            return false;
        }

        if (p.Data.Role is DictadorRole || p.Data.Role?.IsImpostor == true)
        {
            return true;
        }

        // Alliance (amantes, reclutas...) vinculados a la facción impostor.
        var componente = p.GetModifierComponent();
        return componente != null &&
               componente.GetModifiers<AllianceGameModifier>()
                   .Any(m => m.TrueFactionType == AlliedFaction.Impostor);
    }
}