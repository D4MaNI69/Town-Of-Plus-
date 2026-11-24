using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MiraAPI.PluginLoading;
using MiraAPI.Translation;
using Reactor;
using Reactor.Networking;
using Reactor.Networking.Attributes;
using TownOfPlus.Networking;
using TownOfUs.Patches.Options;

namespace TownOfPlus;

[BepInPlugin(Id, "Town Of Plus", Version)]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
[BepInDependency("mira.api")]
[BepInDependency("auavengers.tou.mira", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.edgetel.perfectcomms", BepInDependency.DependencyFlags.SoftDependency)]
[ReactorModFlags(ModFlags.RequireOnAllClients)]
public class TownOfPlusPlugin : BasePlugin, IMiraPlugin
{
    public const string Id = "com.d4mani.townofplus";
    public const string Version = "1.0.8";
    public const string PerfectCommsGuid = "com.edgetel.perfectcomms";

    public static TownOfPlusPlugin Instance { get; private set; } = null!;
    public new ManualLogSource Log => base.Log;

    public Harmony Harmony { get; } = new(Id);
    public string OptionsTitleText => "Town Of Plus";

    public ConfigFile GetConfigFile() => Config;

    public string GetAbbreviatedModName() => "<b><color=#00C896>TOP</color></b>";

    public override void Load()
    {
        Instance = this;
        ParchearTodoConRespaldo();
        MiraLocaleManager.Register(Id, typeof(TownOfPlusPlugin).Assembly.GetName().Name!);
        Patches.PuertasSonidosPatch.Register(Harmony);
        Patches.TopModNewsFetcher.CheckForNews();

        if (IL2CPPChainloader.Instance.Plugins.ContainsKey(PerfectCommsGuid))
        {
            Log.LogInfo("Perfect Comms detectado.");
            try
            {
                Modules.PerfectCommsHuespedBridge.Register();
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[PerfectComms] No se pudo registrar la regla del Huesped: {ex.Message}");
            }
            try
            {
                Modules.PerfectCommsEcoBridge.Register();
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[PerfectComms] No se pudo registrar la regla del Eco: {ex.Message}");
            }
        }

        Log.LogInfo($"Town Of Plus v{Version} loaded successfully!");
    }

    /// <summary>
    ///     Aplica los patches UNO POR UNO con try/catch: si un patch class
    ///     apunta a un método que ya no existe en esta versión del juego,
    ///     se registra el error y SE SIGUEN aplicando los demás.
    /// </summary>
    private void ParchearTodoConRespaldo()
    {
        var ok = 0;
        var fallos = 0;

        foreach (var tipo in typeof(TownOfPlusPlugin).Assembly.GetTypes())
        {
            if (tipo.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
            {
                continue;
            }

            try
            {
                Harmony.CreateClassProcessor(tipo).Patch();
                ok++;
            }
            catch (Exception e)
            {
                fallos++;
                Log.LogWarning($"[Patches] {tipo.Name} no se pudo aplicar: {e.Message}");
            }
        }

        Log.LogInfo($"[Patches] Aplicados: {ok}. Fallidos: {fallos}.");
    }
}