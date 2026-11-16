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
    }
#pragma warning restore 0649
}
