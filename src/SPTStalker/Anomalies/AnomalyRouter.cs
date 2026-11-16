using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace SPTStalker.Anomalies;

/// <summary>GET /sptstalker/anomalies: the current raid's anomaly layout for the calling player (read by the client plugin).</summary>
[Injectable(TypePriority = OnLoadOrder.Routers)]
public class AnomalyRouter(JsonUtil jsonUtil, AnomalyLayoutService layouts)
    : StaticRouter(jsonUtil, [
        new RouteAction<EmptyRequestData>(
            "/sptstalker/anomalies",
            (url, info, sessionId, output, cancellationToken) =>
                new ValueTask<string>(jsonUtil.Serialize(layouts.GetLayout(sessionId)) ?? "{}"))
    ]);
