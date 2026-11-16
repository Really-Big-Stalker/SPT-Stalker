using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTStalker.Anomalies;

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 3)]
public class AnomalyLoader(
    ISptLogger<AnomalyLoader> logger,
    ModHelper modHelper,
    AnomalyLayoutService layoutService) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config = modHelper.GetJsonDataFromFile<AnomalyFile>(modPath, "data/anomalies/anomalies.json");
        layoutService.Configure(config);

        if (config.Enabled)
        {
            GenerateLocationAndLootPatch.Service = layoutService;
            new GenerateLocationAndLootPatch().Enable();
            logger.Success($"[SPT-Stalker] Anomalies ready ({config.Types.Count(t => t.Enabled)} types). Install the client plugin to see them in raid.");
        }

        return Task.CompletedTask;
    }
}
