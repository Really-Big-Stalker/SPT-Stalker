using System.Security.Cryptography;
using System.Text;
using SPTarkov.Server.Core.Models.Common;

namespace SPTStalker.Common;

/// <summary>
/// Builds MongoIds that are the same on every server start.
/// Trader offers must keep their ids between restarts, otherwise per-refresh buy limits
/// and any quest that unlocks an offer would lose track of it.
/// </summary>
public static class StableId
{
    public static MongoId From(params string[] parts)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts)));

        // 12 bytes -> 24 hex chars, the MongoId format
        return new MongoId(Convert.ToHexStringLower(bytes, 0, 12));
    }
}
