/// <summary>
    ///     Detección del sabotaje VANILLA de Comunicaciones (Comms) de Among Us.
    ///
    ///     "Comms" es uno de los sistemas que se pueden sabotear en el juego (junto
    ///     a Luces/Electrical, Reactor, O2, Puertas, ...). Da igual QUIÉN lo haya
    ///     saboteado (el botón de sabotaje vanilla de cualquier impostor, el
    ///     Saboteador con Luces Fuera, otro rol custom, ...): mientras el sistema
    ///     de comunicaciones esté caído se aplica la regla de AFTERLIFE:
    ///     <para>· Afterlife CREW y NEUTRAL: pierden SOLO sus habilidades propias
    ///     (botones custom del rol afterlife), pero CONSERVAN el botón Usar (tareas)
    ///     y los ductos/vents.</para>
    ///     <para>· Afterlife IMPOSTOR (solo impostores afterlife): SIN NINGUNA
    ///     limitación por Comms (habilidades, botones y todo lo demás como si
    ///     Comms no estuviera saboteado).</para>
    ///
    ///     Se consulta el sistema real de Comms en el mapa de sistemas de la nave
    ///     (ShipStatus.Systems → SystemTypes.Comms → HudOverrideSystemType /
    ///     HqHudSystemType). Cuando el sabotaje está activo, IsActive=true; al
    ///     arreglarlo, vuelve a IsActive=false.
    /// </summary>
using MiraAPI.Roles;
using TownOfUs.Roles;

namespace TownOfPlus.Modules;

public static class CommsSabotageManager
{
    /// <summary>
    ///     ¿Está el sabotaje global de Comunicaciones activo AHORA en el mapa?
    ///     Devuelve true en cuanto alguien lo sabotea y hasta que lo arreglan.
    /// </summary>
    public static bool CommsSabotadas
    {
        get
        {
            try
            {
                if (ShipStatus.Instance == null)
                {
                    return false;
                }

                // FIX: Comms NO es un SwitchSystem. En Skeld/Mira/Polus es
                // HudOverrideSystemType y en Airship/Fungle es HqHudSystemType
                // (igual que usa TOU Mira en VanillaSystemCheckPatches). Ambos
                // exponen IsActive=true mientras el sabotaje sigue activo.
                var sistemas = ShipStatus.Instance.Systems;
                if (!sistemas.TryGetValue(SystemTypes.Comms, out var sistema))
                {
                    return false;
                }

                var hud = sistema.TryCast<HudOverrideSystemType>();
                if (hud != null)
                {
                    return hud.IsActive;
                }

                var hq = sistema.TryCast<HqHudSystemType>();
                if (hq != null)
                {
                    return hq.IsActive;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    ///     ¿Este jugador es un AFTERLIFE IMPOSTOR? (Saboteador o cualquier
    ///     afterlife/post-mortem de equipo impostor: rol impostor vanilla o rol
    ///     custom con equipo Impostor). Los impostores afterlife NUNCA tienen
    ///     limitaciones por Comms.
    /// </summary>
    public static bool EsAfterlifeImpostor(PlayerControl? jugador)
    {
        try
        {
            var rol = jugador?.Data?.Role;
            if (rol == null)
            {
                return false;
            }

            if (rol.IsImpostor)
            {
                return true;
            }

            return rol is ITownOfUsRole touRole && touRole.Team == ModdedRoleTeams.Impostor;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Regla de AFTERLIFE en Comms: ¿hay que BLOQUEAR las habilidades
    ///     propias (botones custom) de este jugador?
    ///     <para>· Comms arregladas → NO.</para>
    ///     <para>· Afterlife IMPOSTOR → NO (sin limitaciones por Comms).</para>
    ///     <para>· Afterlife CREW/NEUTRAL (muerto no impostor) → SÍ: sigue sin
    ///     poder usar sus habilidades propias del afterlife.</para>
    ///     <para>· Jugador vivo → NO.</para>
    ///     NUNCA bloquea el botón Usar (tareas) ni los ductos/vents: eso lo
    ///     decide el juego (y el rol) y la regla de Comms no los toca.
    /// </summary>
    public static bool BloqueaHabilidadesAfterlife(PlayerControl? jugador)
    {
        if (!CommsSabotadas)
        {
            return false;
        }

        if (jugador?.Data == null || !jugador.Data.IsDead)
        {
            return false;
        }

        return !EsAfterlifeImpostor(jugador);
    }
}