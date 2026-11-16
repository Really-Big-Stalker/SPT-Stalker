using System;
using UnityEngine;

namespace SPTStalker.Client
{
    // Mirrors the server's AnomalyLayout JSON (/sptstalker/anomalies), read with Unity's JsonUtility.
#pragma warning disable 0649
    [Serializable]
    internal class AnomalyLayout
    {
        public string location;
        public AnomalyFieldData[] fields;
        public AnomalyTypeData[] types;
        public RadiationSettings radiation;
        public ArtifactRadiation[] artifacts;
        public ArmorSettings armor;
        public ArmorClass[] armorClasses;
        public AnomalyPlacementData placement;
    }

    [Serializable]
    internal class AnomalyPlacementData
    {
        public float minOpenSky = 8;
        public float clearance = 2;
        public int maxBlockedDirections = 2;
        public float maxNavMeshDistance = 1.5f;
        public int attempts = 8;
        public float fieldRadius = 8;
    }

    [Serializable]
    internal class ArmorSettings
    {
        public float reductionPerClass;
        public float maxReduction;
        public string[] bodySlots;
        public string[] headSlots;
    }

    [Serializable]
    internal class ArmorClass
    {
        public string tpl;
        public int armorClass;
    }

    [Serializable]
    internal class RadiationSettings
    {
        public bool enabled;
        public float maxDose;
        public float decayPerSecond;
        public float[] artifactRadiationByTier;
        public string[] shieldedSlots;
        public string[] shieldedSubSlots;
        public GasMask[] gasMasks;
        public RadiationCure[] cures;
        public RadiationStage[] stages;
    }

    [Serializable]
    internal class GasMask
    {
        public string name;
        public string tpl;
        public float protection;
    }

    [Serializable]
    internal class RadiationCure
    {
        public string name;
        public string tpl;
        public float doseReduction;
    }

    [Serializable]
    internal class RadiationStage
    {
        public string name;
        public float dose;
        public float damagePerSecond;
        public string[] bodyParts;
        public float sicknessEverySeconds;
        public bool gameRadiationEffect;
    }

    [Serializable]
    internal class ArtifactRadiation
    {
        public string tpl;
        public float radiationPerSecond;
    }

    [Serializable]
    internal class AnomalyFieldData
    {
        public string type;
        public Vector3 center;
        public string artifact;
        public AnomalyData[] anomalies;
    }

    [Serializable]
    internal class AnomalyData
    {
        public Vector3 position;
        public float radius;
    }

    [Serializable]
    internal class AnomalyTypeData
    {
        public string key;
        public AnomalyEffect effect;
    }

    [Serializable]
    internal class AnomalyEffect
    {
        public string damageType;
        public float damagePerSecond;
        public float burstDamage;
        public float burstInterval;
        public string[] bodyParts;
        public float stunSeconds;
        public float contusionSeconds;
        public float fractureChancePercent;
        public float bleedChancePercent;
        public float intoxicationChancePercent;
        public float[] lightColor;
        public float lightIntensity;
        public float flicker;
        public float humPitch;
        public float radiationPerSecond;
        public float auraRadiationPerSecond;
        public float auraRadiusMultiplier;
        public float maskProtection;
        public float armorProtection;
    }
#pragma warning restore 0649
}
