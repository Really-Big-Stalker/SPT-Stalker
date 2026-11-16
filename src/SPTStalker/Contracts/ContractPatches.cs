using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators.RepeatableQuests;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace SPTStalker.Contracts;

/// <summary>
/// Harmony prefixes on the vanilla repeatable-task generators. When the game has picked the Bunker
/// as the task giver, our generator builds the task instead; for every other trader the vanilla
/// generator runs as normal.
/// </summary>
public static class ContractPatches
{
    internal static ContractGenerator? Generator;

    public static void Enable(ContractGenerator generator)
    {
        Generator = generator;
        new EliminationPatch().Enable();
        new CompletionPatch().Enable();
    }

    private static bool IsOurs(MongoId traderId) => Generator is not null && traderId == Generator.TraderId;

    public class EliminationPatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(EliminationQuestGenerator).GetMethod(nameof(EliminationQuestGenerator.Generate))!;
        }

        [PatchPrefix]
        public static bool Prefix(MongoId sessionId, int pmcLevel, MongoId traderId, RepeatableQuestConfig repeatableConfig, ref RepeatableQuest? __result)
        {
            if (!IsOurs(traderId))
            {
                return true;
            }

            __result = Generator!.GenerateHunt(sessionId, pmcLevel, repeatableConfig);
            return false;
        }
    }

    public class CompletionPatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(CompletionQuestGenerator).GetMethod(nameof(CompletionQuestGenerator.Generate))!;
        }

        [PatchPrefix]
        public static bool Prefix(MongoId sessionId, int pmcLevel, MongoId traderId, RepeatableQuestConfig repeatableConfig, ref RepeatableQuest? __result)
        {
            if (!IsOurs(traderId))
            {
                return true;
            }

            __result = Generator!.GenerateArtifactHunt(sessionId, pmcLevel, repeatableConfig);
            return false;
        }
    }
}
