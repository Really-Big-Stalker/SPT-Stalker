using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTStalker.Artifacts;

namespace SPTStalker.Anomalies;

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 3)]
public class AnomalyLoader(
    ISptLogger<AnomalyLoader> logger,
    ModHelper modHelper,
    TemplateTable templateTable,
    ArtifactRegistry artifactRegistry,
    AnomalyLayoutService layoutService) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config = modHelper.GetJsonDataFromFile<AnomalyFile>(modPath, "data/anomalies/anomalies.json");
        layoutService.Configure(config, CollectArmorClasses());

        if (config.Radiation.ArtifactsFitSpecialSlots)
        {
            AllowArtifactsInSpecialSlots(config.Radiation.ShieldedSubSlots);
        }

        if (config.Enabled)
        {
            GenerateLocationAndLootPatch.Service = layoutService;
            new GenerateLocationAndLootPatch().Enable();
            logger.Success($"[SPT-Stalker] Anomalies ready ({config.Types.Count(t => t.Enabled)} types). Install the client plugin to see them in raid.");
        }

        return Task.CompletedTask;
    }

    private List<ArmorClassDto> CollectArmorClasses()
    {
        return templateTable.Items.Values
            .Where(i => i.Properties?.ArmorClass is > 0)
            .Select(i => new ArmorClassDto { Tpl = i.Id.ToString(), ArmorClass = i.Properties!.ArmorClass!.Value })
            .ToList();
    }

    /// <summary>Adds every artifact to the item filter of the pockets' special slots (by default they only take markers, compasses, etc.).</summary>
    private void AllowArtifactsInSpecialSlots(List<string> slotNames)
    {
        var artifacts = artifactRegistry.All.Select(a => a.Tpl).ToList();
        if (artifacts.Count == 0 || slotNames.Count == 0)
        {
            return;
        }

        var changed = 0;
        foreach (var pockets in templateTable.Items.Values.Where(i => i.Parent == BaseClasses.POCKETS))
        {
            foreach (var slot in pockets.Properties?.Slots ?? [])
            {
                if (slot.Name is null || !slotNames.Contains(slot.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var filter = slot.Properties?.Filters?.FirstOrDefault();
                if (filter is null)
                {
                    continue;
                }

                filter.Filter ??= [];
                foreach (var tpl in artifacts)
                {
                    filter.Filter.Add(tpl);
                }

                changed++;
            }
        }

        logger.Info($"[SPT-Stalker] Artifacts allowed in {changed} pocket special slots");
    }
}
