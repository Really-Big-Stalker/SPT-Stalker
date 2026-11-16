using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Services.InRaid;

namespace SPTStalker.Anomalies;

/// <summary>Rolls the anomaly layout right after the server has built a raid's location and loot.</summary>
public class GenerateLocationAndLootPatch : AbstractPatch
{
    internal static AnomalyLayoutService? Service;

    protected override MethodBase GetTargetMethod()
    {
        return typeof(LocationLifecycleService).GetMethod(nameof(LocationLifecycleService.GenerateLocationAndLoot))!;
    }

    [PatchPostfix]
    public static void Postfix(MongoId sessionId, string name, bool generateLoot, LocationBase __result)
    {
        if (!generateLoot || Service is null || __result is null)
        {
            return;
        }

        try
        {
            Service.GenerateForRaid(sessionId, name, __result);
        }
        catch (Exception e)
        {
            // Never break raid start because of anomalies
            Console.WriteLine($"[SPT-Stalker] Anomaly generation failed for {name}: {e}");
        }
    }
}
