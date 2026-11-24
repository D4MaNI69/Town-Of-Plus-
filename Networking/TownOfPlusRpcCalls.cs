namespace TownOfPlus.Networking;

/// <summary>
///     IDs de RPC propios de Town Of Plus. Empiezan en 400 para evitar colisiones
///     con otros mods (Divani Mods usa 200-265).
///     Este archivo es el registro UNICO de todas las IDs de RPC del mod:
///     todas las clases de RPC usan estos valores.
/// </summary>
public enum TownOfPlusRpcCalls : uint
{
    FrancotiradorDisparo = 401,

    // ── KAMIKAZE ─────────────────────────────────────────────────────────────

    /// <summary>Ciclo del botón Engaño (sincroniza el índice del ciclo).</summary>
    KamikazeEngano = 402,

    /// <summary>Cambiaformas vanilla hacia un jugador vivo aleatorio.</summary>
    KamikazeCambiaformas = 403,

    /// <summary>Camuflaje: invisible exactamente 1 segundo.</summary>
    KamikazeCamuflaje = 404,

    /// <summary>Ducto falso (PNG no usable) que desaparece a los 3 segundos.</summary>
    KamikazeDuctoFalso = 405,

    /// <summary>Arrastre de cuerpo (máximo 3 segundos).</summary>
    KamikazeArrastre = 406,

    /// <summary>Victoria por expulsión del Kamikaze en la reunión de detonación.</summary>
    KamikazeDetonacion = 407,

    /// <summary>Revelación del Dictador (Candidato → Dictador) con animación de Mayor.</summary>
    DictadorReveal = 408,

    /// <summary>Luces Fuera del Saboteador (apagón custom de visión para no-imps).</summary>
    SaboteadorLucesFuera = 409,

    /// <summary>Abrir una puerta del Cerrajero (sincronizada a todos).</summary>
    CerrajeroAbrirPuerta = 410,

    /// <summary>Cerrar una puerta del Cerrajero (sincronizada a todos).</summary>
    CerrajeroCerrarPuerta = 411,

    /// <summary>La Víctima guarda una nota secreta (lista interna sincronizada).</summary>
    VictimaNota = 412,

    /// <summary>El host revela las notas de la Víctima muerta al empezar la reunión.</summary>
    VictimaRevelarNotas = 413,

    /// <summary>La Amalgama pide al host revivir en un cuerpo (fin de canalización).</summary>
    AmalgamaPedirRevivir = 414,

    /// <summary>El host completa el revive: quita el cuerpo, revive y asigna rol aleatorio.</summary>
    AmalgamaCompletarRevivir = 415,

    /// <summary>El Profesor marca un target para reentrenar en reunion.</summary>
    ProfesorMarcar = 416,

    /// <summary>El host reparte las 3 opciones de cambio de rol al reentrenado.</summary>
    ProfesorOpciones = 417,

    /// <summary>Aviso solo al target cuando el reentreno falla (imp/neutral).</summary>
    ProfesorAviso = 418,

    /// <summary>El reentrenado elige su nuevo rol crew (validado por el host).</summary>
    ProfesorCambioRol = 419,

    /// <summary>El Huésped pide al host poseer a un jugador vivo cercano.</summary>
    HuespedPeticionPosesion = 420,

    /// <summary>El host difunde el inicio de la posesión (todos los clientes).</summary>
    HuespedPosesionInicio = 421,

    /// <summary>El host difunde el fin de la posesión (todos los clientes).</summary>
    HuespedPosesionFin = 422,

    /// <summary>Dirección del controlador en el host hacia todos los clientes.</summary>
    HuespedPosesionInput = 423,

    /// <summary>El host reenvía la dirección validada del Huésped a todos.</summary>
    HuespedPosesionDireccion = 424,

    /// <summary>Input del Huésped estilo Puppeteer: RPC único unreliable a todos.</summary>
    HuespedPosesionInputUnreliable = 427,

    /// <summary>
    ///     RETIRADO (rework del Ermitaño a ESCAPE): el ID se mantiene reservado
    ///     para no reasignar números de RPC entre versiones del mod. El Escape del
    ///     Ermitaño ya no necesita RPC: el TP lo replica NetTransform.RpcSnapTo del
    ///     propio dueño y la invisibilidad, el modifier Swoop.
    /// </summary>
    ErmitanoMarcar = 425,

    /// <summary>RETIRADO (rework del Ermitaño a ESCAPE): ver <see cref="ErmitanoMarcar"/>.</summary>
    ErmitanoVolver = 426,

    /// <summary>El Aislador marca a un jugador vivo (tinte #109FB9 solo para él).</summary>
    AisladorMarcar = 428,

    /// <summary>El Aislador encierra a todos los marcados en burbujas 3 s.</summary>
    AisladorBurbuja = 429,

    /// <summary>El Aislador activa su escudo (burbuja que lo sigue + inmunidad).</summary>
    AisladorEscudo = 430,

    /// <summary>El host asigna el ancla del Eco (eco + ancla vivos en todos).</summary>
    EcoAsignarAncla = 431,

    /// <summary>El host devuelve al Eco junto a su ancla (TP por rango/post-reunión).</summary>
    EcoVolverAlAncla = 432,

    /// <summary>El ancla murió/desconectó: el Eco vuelve a ser fantasma común.</summary>
    EcoDevolverAFantasma = 433,

    /// <summary>RETIRADO (sin cargas): el Abogado ya no gana nada saboteando.
    /// Se mantiene reservado para no reasignar números de RPC.</summary>
    AbogadoSabotaje = 440,

    /// <summary>Host difunde reuniones/último uso del Abogado.</summary>
    AbogadoEstado = 441,

    /// <summary>Abogado usa Terminar en reunión (cooldown por reuniones).</summary>
    AbogadoTerminar = 442,

    /// <summary>RETIRADO (botón Rehacer eliminado).
    /// Se mantiene reservado para no reasignar números de RPC.</summary>
    AbogadoRehacer = 443,

    /// <summary>Abogado chantajea a un jugador (marca en ronda).</summary>
    AbogadoChantaje = 444,

    /// <summary>Limpieza de la marca de chantaje (al terminar la reunión).</summary>
    AbogadoChantajeLimpiar = 445,

    // ── DEUDOR (crewmate power) ──────────────────────────────────────────────

    /// <summary>El Deudor hace un trato (marca) con un jugador vivo en ronda.</summary>
    DeudorTrato = 446,

    /// <summary>Un marcado avisa al host que usó una habilidad (kill o botón MiraAPI).</summary>
    DeudorUsoHabilidad = 447,

    /// <summary>El host difunde que un marcado pasó a deber un favor.</summary>
    DeudorFavor = 448,

    // ── COLEGA (crewmate support) ────────────────────────────────────────────

    /// <summary>El Colega marca a un jugador vivo (Acompañar).</summary>
    ColegaAcompanar = 449,

    /// <summary>El marcado avisa que usó una habilidad (oculta la flecha 3 s).</summary>
    ColegaUsoHabilidad = 450,

    /// <summary>El Colega pide al host usar Esperanza en la reunión.</summary>
    ColegaEsperanza = 451,

    /// <summary>El host confirma Esperanza (intercambio de votos de la pareja).</summary>
    ColegaEsperanzaConfirmada = 452,
}