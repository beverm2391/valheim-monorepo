using System;

namespace BenheimQoL.Spawning;

internal static partial class SpawnPopulationProbe
{
    private sealed class TrackedRule
    {
        private WeakReference<SpawnSystem.SpawnData> target;

        internal TrackedRule(
            string source,
            string prefab,
            SpawnSystem.SpawnData spawnData)
        {
            Source = source;
            Prefab = prefab;
            target = new WeakReference<SpawnSystem.SpawnData>(spawnData);
        }

        internal string Source { get; }
        internal string Prefab { get; }
        internal RuleConfiguration Configuration { get; set; }
        internal bool HasConfiguration { get; set; }
        internal int LoadedCount { get; set; }
        internal bool Saturated { get; set; }
        internal bool HasPopulation { get; set; }
        internal float LastSampleAt { get; set; }
        internal bool HasSample => HasPopulation;
        internal float LastPopulationEmissionAt { get; set; }
        internal bool Faulted { get; set; }

        internal void SetTarget(SpawnSystem.SpawnData spawnData)
        {
            target.SetTarget(spawnData);
            ResetObservation();
        }

        internal bool TryGetTarget(out SpawnSystem.SpawnData? spawnData)
        {
            return target.TryGetTarget(out spawnData);
        }

        internal void ResetObservation()
        {
            Configuration = default;
            HasConfiguration = false;
            LoadedCount = 0;
            Saturated = false;
            HasPopulation = false;
            LastSampleAt = 0f;
            LastPopulationEmissionAt = 0f;
            Faulted = false;
        }
    }

    private readonly struct RuleConfiguration : IEquatable<RuleConfiguration>
    {
        private RuleConfiguration(
            string source,
            string prefab,
            float spawnInterval,
            float spawnChance,
            int cap,
            int groupSizeMin,
            int groupSizeMax,
            float spawnDistance,
            string biome,
            string biomeArea,
            float minAltitude,
            float maxAltitude)
        {
            Source = source;
            Prefab = prefab;
            SpawnInterval = spawnInterval;
            ConfiguredSpawnChance = spawnChance;
            ConfiguredCap = cap;
            GroupSizeMin = groupSizeMin;
            GroupSizeMax = groupSizeMax;
            SpawnDistance = spawnDistance;
            Biome = biome;
            BiomeArea = biomeArea;
            MinAltitude = minAltitude;
            MaxAltitude = maxAltitude;
        }

        internal string Source { get; }
        internal string Prefab { get; }
        internal float SpawnInterval { get; }
        internal float ConfiguredSpawnChance { get; }
        internal int ConfiguredCap { get; }
        internal int GroupSizeMin { get; }
        internal int GroupSizeMax { get; }
        internal float SpawnDistance { get; }
        internal string Biome { get; }
        internal string BiomeArea { get; }
        internal float MinAltitude { get; }
        internal float MaxAltitude { get; }

        internal static RuleConfiguration Capture(
            string source,
            string prefab,
            SpawnSystem.SpawnData spawnData)
        {
            return new RuleConfiguration(
                source,
                prefab,
                spawnData.m_spawnInterval,
                spawnData.m_spawnChance,
                spawnData.m_maxSpawned,
                spawnData.m_groupSizeMin,
                spawnData.m_groupSizeMax,
                spawnData.m_spawnDistance,
                spawnData.m_biome.ToString(),
                spawnData.m_biomeArea.ToString(),
                spawnData.m_minAltitude,
                spawnData.m_maxAltitude);
        }

        public bool Equals(RuleConfiguration other)
        {
            return Source == other.Source &&
                Prefab == other.Prefab &&
                SpawnInterval.Equals(other.SpawnInterval) &&
                ConfiguredSpawnChance.Equals(other.ConfiguredSpawnChance) &&
                ConfiguredCap == other.ConfiguredCap &&
                GroupSizeMin == other.GroupSizeMin &&
                GroupSizeMax == other.GroupSizeMax &&
                SpawnDistance.Equals(other.SpawnDistance) &&
                Biome == other.Biome &&
                BiomeArea == other.BiomeArea &&
                MinAltitude.Equals(other.MinAltitude) &&
                MaxAltitude.Equals(other.MaxAltitude);
        }

        public override bool Equals(object? value)
        {
            return value is RuleConfiguration other && Equals(other);
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(Source);
            hash.Add(Prefab);
            hash.Add(SpawnInterval);
            hash.Add(ConfiguredSpawnChance);
            hash.Add(ConfiguredCap);
            hash.Add(GroupSizeMin);
            hash.Add(GroupSizeMax);
            hash.Add(SpawnDistance);
            hash.Add(Biome);
            hash.Add(BiomeArea);
            hash.Add(MinAltitude);
            hash.Add(MaxAltitude);
            return hash.ToHashCode();
        }
    }
}
