using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace SPTStalker.Anomalies;

/// <summary>
/// GET /sptstalker/anomalies: the current raid's anomaly layout for the calling player (read by the client plugin).
/// POST /sptstalker/settings: raid-generation settings from the client's F12 menu (apply from the next raid).
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Routers)]
public class AnomalyRouter(JsonUtil jsonUtil, AnomalyLayoutService layouts)
    : StaticRouter(jsonUtil, [
        new RouteAction<EmptyRequestData>(
            "/sptstalker/anomalies",
            (url, info, sessionId, output, cancellationToken) =>
                new ValueTask<string>(jsonUtil.Serialize(layouts.GetLayout(sessionId)) ?? "{}")),
        new RouteAction<ClientRaidSettings>(
            "/sptstalker/settings",
            (url, info, sessionId, output, cancellationToken) =>
            {
                layouts.SetClientSettings(sessionId, info);
                return new ValueTask<string>("{\"ok\":true}");
            })
    ]);
