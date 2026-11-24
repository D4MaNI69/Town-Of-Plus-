using System;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TownOfPlus.Assets;

public static class TownofplusAssets
{
    private const string IconsPath = "TownOfPlus.Resources.Icons";
    private const string ButtonsPath = "TownOfPlus.Resources.Buttons";
    private const string ModsPath = "TownOfPlus.Resources.Modifications";
    private const string ShortPath = "TownOfPlus.Resources";
    private const string AnnouncementsPath = "TownOfPlus.Resources.Announcements";

    // Banner/logo superpuesto a los anuncios propios (198×192, ppu 290,
    // MISMO tamaño que el logo de anuncios de TOU Mira: AuAvengers.png).
    public static LoadableAsset<Sprite> AnnouncementBanner { get; } =
        new LoadableResourceAsset($"{AnnouncementsPath}.AnnouncementBanner.png", 290);

    // Ícono del Francotirador (200 ppu) — Resources/Icons/francotirador.png
    public static LoadableAsset<Sprite> FrancotiradorIcon { get; } = new LoadableResourceAsset($"{IconsPath}.francotirador.png", 200);

    // Botones del Francotirador (100 ppu)
    public static LoadableAsset<Sprite> RecargarButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Recargar.png", 100);
    public static LoadableAsset<Sprite> DispararButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Disparar.png", 100);
    // Botón para SALIR del zoom (100 ppu): el PNG Salir.png NO existe en el
    // proyecto; se usa Volver.png (Resources/Buttons/Volver.png), que existe.
    public static LoadableAsset<Sprite> SalirZoomButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Volver.png", 100);

    // Mira roja que sigue a cada objetivo durante el zoom out (100 ppu)
    public static LoadableAsset<Sprite> MiraRoja { get; } = new LoadableResourceAsset($"{ShortPath}.MiraRoja.png", 100);

    // Ícono del Cerrajero (200 ppu) — Resources/Icons/cerrajero.png
    public static LoadableAsset<Sprite> CerrajeroIcon { get; } = new LoadableResourceAsset($"{IconsPath}.cerrajero.png", 200);

    // Botones del Cerrajero (100 ppu)
    public static LoadableAsset<Sprite> AbrirButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Abrir.png", 100);
    public static LoadableAsset<Sprite> CerrarButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Cerrar.png", 100);

    // ── HUÉSPED (afterlife crew) ────────────────────────────────────────────
    // Ícono del Huésped (200 ppu) — Resources/Icons/Huesped.png
    public static LoadableAsset<Sprite> HuespedIcon { get; } = new LoadableResourceAsset($"{IconsPath}.Huesped.png", 200);

    // Botón Poseer (100 ppu) — Resources/Buttons/Poseer.png
    public static LoadableAsset<Sprite> PoseerButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Poseer.png", 100);

    // ── KAMIKAZE ─────────────────────────────────────────────────────────────
    // Ícono del Kamikaze (200 ppu) — Resources/Icons/Kamikaze.png
    public static LoadableAsset<Sprite> KamikazeIcon { get; } = new LoadableResourceAsset($"{IconsPath}.Kamikaze.png", 200);

    // Botón principal Engaño (100 ppu): el PNG viejo Engano.png YA NO EXISTE
    // (el ciclo usa Transformar/Desvanecer/Excavar/Arrastrar). Se usa
    // Transformar.png como sprite genérico inicial — FixedUpdate lo reemplaza
    // al instante por el de la habilidad actual del ciclo.
    public static LoadableAsset<Sprite> EnganoButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Transformar.png", 100);

    // Íconos por habilidad del ciclo Engaño (100 ppu) — Resources/Buttons/
    // Transformar (Cambiaformas/Morph) · Desvanecer (Camuflaje/Swoop) ·
    // Excavar (Ducto falso/Mine) · Arrastrar (Arrastre/Drag).
    public static LoadableAsset<Sprite> EnganoCambiaformasButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Transformar.png", 100);
    public static LoadableAsset<Sprite> EnganoCamuflajeButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Desvanecer.png", 100);
    public static LoadableAsset<Sprite> EnganoDuctoButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Excavar.png", 100);
    public static LoadableAsset<Sprite> EnganoArrastreButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Arrastrar.png", 100);

    // Sprite del ducto falso (100 ppu): el PNG Kamikaze/DuctoFalso.png NO
    // existe en el proyecto; se usa Desvanecer.png (existe).
    public static LoadableAsset<Sprite> DuctoFalsoSprite { get; } = new LoadableResourceAsset($"{ButtonsPath}.Desvanecer.png", 100);

    // ── DICTADOR ─────────────────────────────────────────────────────────────
    // Ícono del Dictador (200 ppu) — Resources/Icons/Dictador.png
    public static LoadableAsset<Sprite> DictadorIcon { get; } = new LoadableResourceAsset($"{IconsPath}.Dictador.png", 200);

    // Botón Reveal del Dictador (100 ppu) — Resources/Buttons/DictadorReveal.png
    public static LoadableAsset<Sprite> DictadorRevealButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.DictadorReveal.png", 100);

    // ── SABOTEADOR (afterlife impostor) ──────────────────────────────────────
    // Ícono del Saboteador (200 ppu) — Resources/Icons/Saboteador.png
    public static LoadableAsset<Sprite> SaboteadorIcon { get; } = new LoadableResourceAsset($"{IconsPath}.Saboteador.png", 200);

    // Botón Luces Fuera (100 ppu) — Resources/Buttons/LucesFuera.png
    public static LoadableAsset<Sprite> LucesFueraButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.LucesFuera.png", 100);

    // ── VÍCTIMA ──────────────────────────────────────────────────────────────
    // Ícono de la Víctima (200 ppu) — Resources/Icons/Victima.png
    public static LoadableAsset<Sprite> VictimaIcon { get; } = new LoadableResourceAsset($"{IconsPath}.Victima.png", 200);

    // Botón Nota (100 ppu) — Resources/Buttons/Nota.png
    public static LoadableAsset<Sprite> NotaButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Nota.png", 100);

    // ── AMALGAMA (afterlife neutral) ─────────────────────────────────────────
    // Ícono de la Amalgama (200 ppu) — Resources/Icons/Amalgama.png
    public static LoadableAsset<Sprite> AmalgamaIcon { get; } = new LoadableResourceAsset($"{IconsPath}.Amalgama.png", 200);

    // Botón Revivir (100 ppu) — Resources/Buttons/Revivir.png
    public static LoadableAsset<Sprite> RevivirButton { get; } = new LoadableResourceAsset($"{ButtonsPath}.Revivir.png", 100);

    // ── PROFESOR (power crew; PNG opcionales, con fallback procedural) ──
    public static LoadableAsset<Sprite> ProfesorIcon { get; } = Utilities.SpritesProfesor.IconoRolAsset;
    public static LoadableAsset<Sprite> ProfesorReentrenarButton { get; } = Utilities.SpritesProfesor.BotonReentrenarAsset;
    public static LoadableAsset<Sprite> ProfesorCambiarButton { get; } = Utilities.SpritesProfesor.BotonCambiarAsset;

    // ── ERMITAÑO (investigative crew) — rework ESCAPE ───────────────────────
    // Ícono del Ermitaño (200 ppu) — Resources/Icons/Ermitano.png (o procedural)
    public static LoadableAsset<Sprite> ErmitanoIcon { get; } = Utilities.SpritesErmitano.IconoRolAsset;

    // Botón Escape (150 ppu) — Resources/Buttons/Escape.png
    public static LoadableAsset<Sprite> EscapeButton { get; } = Utilities.SpritesErmitano.BotonEscapeAsset;

    // PNG del cursor de Escape (150 ppu) — Resources/EscapeCursor.png
    // (se dibuja al 70% de opacidad sobre el ratón; el alpha lo pone el manager)
    public static LoadableAsset<Sprite> ErmitanoEscapeCursor { get; } =
        Utilities.SpritesErmitano.CursorEscapeAsset;

    // ── ECO (afterlife crew) ─────────────────────────────────────────────────
    // Ícono del Eco (200 ppu) — Resources/Icons/Eco.png (o procedural)
    public static LoadableAsset<Sprite> EcoIcon { get; } = Utilities.SpritesEco.IconoRolAsset;

    // ── ANCLA (afterlife crew) ─────────────────────────────────────────────────
    // Ícono del Ancla (200 ppu) — Resources/Icons/Modif/Ancla.png (o procedural)
    public static LoadableAsset<Sprite> AnclaIcon { get; } = Utilities.SpritesAncla.IconoModAsset;

    // ── AISLADOR (support crew) ──────────────────────────────────────────────
    // Ícono del Aislador (200 ppu) — Resources/Icons/Aislador.png (o procedural)
    public static LoadableAsset<Sprite> AisladorIcon { get; } = Utilities.SpritesAislador.IconoRolAsset;

    // ── ABOGADO (impostor support) ───────────────────────────────────────────
    // Ícono (200 ppu) y botones (100 ppu) — Resources/Icons y Resources/Buttons
    // (placeholders PNG con fallback procedural en Utilities/SpritesAbogado).
    public static LoadableAsset<Sprite> AbogadoIcon { get; } = Utilities.SpritesAbogado.IconoRolAsset;
    public static LoadableAsset<Sprite> AbogadoChantajeAsset { get; } = Utilities.SpritesAbogado.BotonChantajeAsset;
    public static LoadableAsset<Sprite> AbogadoTerminarAsset { get; } = Utilities.SpritesAbogado.BotonTerminarAsset;

    // Versión GRIS del botón de reunión (en espera → deshabilitado).
    public static LoadableAsset<Sprite> AbogadoTerminarGrisAsset { get; } = Utilities.SpritesAbogado.BotonTerminarGrisAsset;

    // Botones Soplar / Burbuja / Escudo (100 ppu) — Resources/Buttons/
    // Burbuja es el PNG compartido para marcados y escudo.
    public static LoadableAsset<Sprite> AisladorSoplarButton { get; } = Utilities.SpritesAislador.BotonSoplarAsset;
    public static LoadableAsset<Sprite> AisladorBurbujaButton { get; } = Utilities.SpritesAislador.BurbujaAsset;
    public static LoadableAsset<Sprite> AisladorEscudoButton { get; } = Utilities.SpritesAislador.BotonEscudoAsset;

    // ── DEUDOR (power crew) ──────────────────────────────────────────────────
    // Ícono (200 ppu) y botón Trato (100 ppu) — Resources/Icons y Resources/Buttons
    // (placeholders PNG con fallback procedural en Utilities/SpritesDeudor).
    public static LoadableAsset<Sprite> DeudorIcon { get; } = Utilities.SpritesDeudor.IconoRolAsset;
    public static LoadableAsset<Sprite> DeudorTratoButton { get; } = Utilities.SpritesDeudor.BotonTratoAsset;

    // ── COLEGA (support crew) ────────────────────────────────────────────────
    // Ícono (200 ppu), botón Acompañar (100 ppu) y botón de reunión Esperanza
    // (100 ppu) — placeholders PNG con fallback procedural en
    // Utilities/SpritesColega.
    public static LoadableAsset<Sprite> ColegaIcon { get; } = Utilities.SpritesColega.IconoRolAsset;
    public static LoadableAsset<Sprite> ColegaAcompanarButton { get; } = Utilities.SpritesColega.BotonAcompanarAsset;
    public static LoadableAsset<Sprite> ColegaEsperanzaButton { get; } = Utilities.SpritesColega.BotonEsperanzaAsset;
}