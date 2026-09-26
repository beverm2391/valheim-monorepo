using UnityEngine;

namespace BenheimQoL.WeatherVisibility;

internal static partial class BlizzardVisibilityRuntime
{
    private interface IParticleState
    {
        ParticleSystem Particle { get; }
    }

    private sealed class StormParticleState : IParticleState
    {
        internal StormParticleState(ParticleSystem particle, bool snow, bool suppressed)
        {
            Particle = particle;
            IsSnow = snow;
            IsSuppressed = suppressed;
            OriginalEmission = particle.emission.enabled;
            OriginalRate = particle.emission.rateOverTimeMultiplier;
            OriginalPlaying = particle.isPlaying;
        }

        public ParticleSystem Particle { get; }
        internal bool IsSnow { get; }
        internal bool IsSuppressed { get; }
        internal bool OriginalEmission { get; }
        internal float OriginalRate { get; }
        internal bool OriginalPlaying { get; }
    }

    private sealed class MistEmitterState
    {
        internal MistEmitterState(MistEmitter emitter)
        {
            Emitter = emitter;
            OriginalEnabled = emitter.enabled;
        }

        internal MistEmitter Emitter { get; }
        internal bool OriginalEnabled { get; }
    }

    private sealed class DistantFogState
    {
        internal DistantFogState(DistantFogEmitter emitter)
        {
            Emitter = emitter;
            OriginalEnabled = emitter.enabled;
        }

        internal DistantFogEmitter Emitter { get; }
        internal bool OriginalEnabled { get; }
    }

    private sealed class GlobalHazeState : IParticleState
    {
        internal GlobalHazeState(ParticleSystem particle)
        {
            Particle = particle;
            OriginalEmission = particle.emission.enabled;
            OriginalPlaying = particle.isPlaying;
        }

        public ParticleSystem Particle { get; }
        internal bool OriginalEmission { get; }
        internal bool OriginalPlaying { get; }
    }
}
