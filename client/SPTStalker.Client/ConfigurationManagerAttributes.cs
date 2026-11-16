using System;
using BepInEx.Configuration;

namespace SPTStalker.Client
{
    /// <summary>
    /// Display hints read by BepInEx Configuration Manager (the F12 menu in SPT), by name, as documented by
    /// BepInEx.ConfigurationManager. Pass an instance as a tag in ConfigDescription.
    /// </summary>
#pragma warning disable 0649, 0169, 0414
    internal sealed class ConfigurationManagerAttributes
    {
        public int? Order;
        public bool? IsAdvanced;
        public bool? ReadOnly;
        public bool? HideDefaultButton;
        public bool? HideSettingName;
        public bool? Browsable;
        public string DispName;
        public Action<ConfigEntryBase> CustomDrawer;
    }
#pragma warning restore 0649, 0169, 0414
}
