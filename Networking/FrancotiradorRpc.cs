using System;
using MiraAPI.GameOptions;
using Reactor.Networking.Attributes;
using TownOfPlus.Modules;
using Reactor.Networking.Rpc;
using TownOfPlus.Options.Impostor.ImpostorKilling;
using TownOfPlus.Utilities;
using UnityEngine;

namespace TownOfPlus.Networking;

/// <summary>
///     RPCs del Francotirador. El disparo es GLOBAL: suena en TODOS los
///     clientes (tripulantes, impostores, neutrales y el propio francotirador)
///     para que todos sepan que hay un francotirador acechando.
///     El enum con TODAS las IDs del mod vive en TownOfPlusRpc.cs.
/// </summary>
public static class FrancotiradorRpc
{
    /// <summary>
    ///     El francotirador mata: reproduce el disparo en su cliente y lo
    ///     envÃƒÂ­a al resto de la partida. Nunca lanza excepciones: si el envÃƒÂ­o
    ///     falla (p. ej. freeplay sin clientes remotos), solo se pierde el
    ///     sonido, nunca el disparo.
    /// </summary>
    public static void EnviarDisparoGlobal()
    {
        // El cliente que dispara lo oye de inmediato (el RPC no vuelve a ÃƒÂ©l).
        ReproducirDisparo();

        // La flecha del propio tirador tambiÃƒÂ©n aparece en su cliente (el RPC
        // con LocalHandling.None no vuelve a ÃƒÂ©l).
        try
        {
            FlechasDisparo.Crear(PlayerControl.LocalPlayer.PlayerId);
        }
        catch
        {
            // CosmÃƒÂ©tico.
        }


        try
        {
            RpcDisparoGlobal(PlayerControl.LocalPlayer);
        }
        catch (Exception ex)
        {
            TownOfPlusPlugin.Instance?.Log.LogWarning(
                $"[Francotirador] Sonido global no enviado: {ex.Message}");
        }
    }

    /// <summary>Se ejecuta en cada cliente remoto: reproduce el disparo.</summary>
    [MethodRpc((uint)TownOfPlusRpcCalls.FrancotiradorDisparo, LocalHandling = RpcLocalHandling.None)]
    public static void RpcDisparoGlobal(PlayerControl sender)
    {
        ReproducirDisparo();

        // Flecha roja hacia el francotirador: cada cliente crea la suya,
        // apuntando en vivo a la posicion del tirador (estilo Titiritero).
        try
        {
            if (sender != null)
            {
                FlechasDisparo.Crear(sender.PlayerId);
            }
        }
        catch
        {
            // Cosmetico.
        }
    }

    private static void ReproducirDisparo()
    {
        try
        {
            if (SoundManager.Instance == null)
            {
                return;
            }

            var clip = SonidosTop.Disparo.LoadAsset();
            if (clip == null)
            {
                return;
            }

            const float volumen = 1.0f;
            AudioTop.Sonar(clip, false, volumen);
        }
        catch
        {
            // Sin sonido configurado: no pasa nada.
        }
    }
}
