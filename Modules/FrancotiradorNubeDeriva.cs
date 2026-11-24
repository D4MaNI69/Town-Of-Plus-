using Il2CppInterop.Runtime.Injection;
using Reactor.Utilities.Attributes;
using UnityEngine;

namespace TownOfPlus.Modules;

/// <summary>
///     Deriva horizontal de una nube durante el zoom out del Francotirador.
///     Al salir de pantalla por un lado, reaparece por el otro.
/// </summary>
[RegisterInIl2Cpp]
public class FrancotiradorNubeDeriva(IntPtr cppPtr) : MonoBehaviour(cppPtr)
{
    public FrancotiradorNubeDeriva() : this(ClassInjector.DerivedConstructorPointer<FrancotiradorNubeDeriva>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    /// <summary>Velocidad de deriva (unidades/segundo, con signo).</summary>
    public float Velocidad { get; set; }

    /// <summary>Ancho total del recorrido antes de envolver.</summary>
    public float Ancho { get; set; }

    private void Update()
    {
        var pos = transform.localPosition;
        pos.x += Velocidad * Time.deltaTime;

        var limite = Ancho / 2f;
        if (pos.x > limite)
        {
            pos.x = -limite;
        }
        else if (pos.x < -limite)
        {
            pos.x = limite;
        }

        transform.localPosition = pos;
    }
}