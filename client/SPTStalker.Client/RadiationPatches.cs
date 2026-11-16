using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SPTStalker.Client
{
    /// <summary>
    /// Harmony hooks:
    ///  - HealthParametersPanel.UpdateMainHealthAndStats: writes our dose into the health screen's radiation meter
    ///    (the game has the panel but nothing feeds it).
    ///  - ActiveHealthController.DoMedEffect: notices vodka/moonshine being used to lower the dose.
    /// </summary>
    internal static class RadiationPatches
    {
        private static FieldInfo _radiationPanelField;
        private static MethodInfo _setParameterValue;
        private static Type _valueStructType;

        public static void Apply(Harmony harmony)
        {
            TryPatch(harmony, "EFT.UI.Health.HealthParametersPanel", "UpdateMainHealthAndStats", nameof(HealthPanelPostfix));
            TryPatch(harmony, "EFT.HealthSystem.ActiveHealthController", "DoMedEffect", nameof(DoMedEffectPostfix));
        }

        private static void TryPatch(Harmony harmony, string typeName, string method, string postfix)
        {
            try
            {
                var type = AccessTools.TypeByName(typeName);
                var target = type == null ? null : AccessTools.Method(type, method);
                if (target == null)
                {
                    Plugin.Log.LogWarning($"SPT-Stalker: {typeName}.{method} not found, that radiation feature is off");
                    return;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(typeof(RadiationPatches).GetMethod(postfix, BindingFlags.Static | BindingFlags.NonPublic)));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"SPT-Stalker: could not patch {typeName}.{method}: {e.Message}");
            }
        }

        private static void HealthPanelPostfix(object __instance)
        {
            if (!RadiationSystem.Active || !Plugin.RadiationEnabled.Value)
            {
                return;
            }

            try
            {
                _radiationPanelField ??= AccessTools.Field(__instance.GetType(), "_radiation");
                var panel = _radiationPanelField?.GetValue(__instance) as Component;
                if (panel == null)
                {
                    return;
                }

                if (!panel.gameObject.activeSelf)
                {
                    panel.gameObject.SetActive(true);
                }

                if (_setParameterValue == null)
                {
                    _setParameterValue = AccessTools.Method(panel.GetType(), "SetParameterValue");
                    _valueStructType = _setParameterValue?.GetParameters()[0].ParameterType;
                }

                if (_setParameterValue == null)
                {
                    return;
                }

                var value = Activator.CreateInstance(_valueStructType);
                AccessTools.Field(_valueStructType, "Current")?.SetValue(value, RadiationSystem.Dose);
                AccessTools.Field(_valueStructType, "Maximum")?.SetValue(value, RadiationSystem.MaxDose);
                AccessTools.Field(_valueStructType, "Minimum")?.SetValue(value, 0f);

                // (value, warningThreshold, roundValue, countFromTop, forceUpdate)
                _setParameterValue.Invoke(panel, new object[] { value, RadiationSystem.WarningDose, 0, false, true });
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("SPT-Stalker: radiation meter update failed: " + e.Message);
            }
        }

        private static void DoMedEffectPostfix(object __instance, object item)
        {
            try
            {
                RadiationSystem.OnItemUsed(__instance, item);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("SPT-Stalker: item-use check failed: " + e.Message);
            }
        }
    }
}
