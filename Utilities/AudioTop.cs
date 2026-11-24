using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Utilities;

/// <summary>
///     Reproductor de audio de Town Of Plus.
///     SoundManager.PlaySound SIN AudioMixerGroup explícito usa el canal de
///     MÚSICA, así que estos efectos dejaban de sonar si la música estaba a 0%.
///     Aquí SIEMPRE se envían al canal SFX (SoundManager.SfxChannel), igual
///     que los pasos, kills y otros efectos del juego: respetan el volumen de
///     EFECTOS y suenan aunque la música esté en silencio.
/// </summary>
public static class AudioTop
{
    /// <summary>Reproduce un clip por el canal de efectos (SFX). Devuelve el AudioSource o null.</summary>
    public static AudioSource? Sonar(AudioClip? clip, bool bucle = false, float volumen = 1f)
    {
        try
        {
            if (clip == null || SoundManager.Instance == null)
            {
                return null;
            }

            return SoundManager.Instance.PlaySound(clip, bucle, volumen, SoundManager.Instance.SfxChannel);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Reproduce un activo de audio (LoadableAsset) por el canal de efectos (SFX).</summary>
    public static AudioSource? Sonar(LoadableAsset<AudioClip>? activo, float volumen = 1f)
    {
        try
        {
            return activo == null ? null : Sonar(activo.LoadAsset(), false, volumen);
        }
        catch
        {
            return null;
        }
    }
}