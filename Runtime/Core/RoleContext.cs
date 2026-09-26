using ReactiveSolutions.AttributeSystem.Core.Data;
using System;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// The entity through which effects and status effects read their formulas and conditions: its providers are the
    /// source and the target (see <see cref="EffectRoles"/>), so "Source/AttackPower" and "Target/Health" resolve like
    /// any provider path.
    /// </summary>
    internal static class RoleContext
    {
        public static Entity Create(string name, Entity source, Entity target)
        {
            var context = new Entity { Name = name };
            if (source != null && !source.IsDisposed) context.RegisterExternalProvider(EffectRoles.Source, source);
            if (target != null && !target.IsDisposed) context.RegisterExternalProvider(EffectRoles.Target, target);
            return context;
        }

        /// <summary>Whether the condition holds now (null and Always do).</summary>
        public static bool Holds(StatBlockCondition condition, Entity context)
        {
            if (condition == null || condition.Type == StatBlockCondition.Mode.Always) return true;

            bool holds = false;
            ConditionEvaluator.Observe(condition, context).Take(1).Subscribe(value => holds = value).Dispose();
            return holds;
        }

        /// <summary>The value now (0 for a null source).</summary>
        public static float Read(ValueSource value, Entity context) => value == null ? 0f : ReadNow(value.GetObservable(context));

        /// <summary>The current value of a stream that emits it on subscribe (as attributes and logic do).</summary>
        public static float ReadNow(IObservable<float> stream)
        {
            float value = 0f;
            stream.Take(1).Subscribe(v => value = v).Dispose();
            return value;
        }
    }
}
