using SemanticKeys;
using System;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public static class EntityObservableExtensions
    {
        /// <summary>
        /// Follows whichever entity the source currently holds (null = none) and emits that entity's
        /// attribute values. Switching entities drops the previous subscription, so values from a
        /// previous target never arrive after a retarget (e.g. a health bar moved to a new enemy).
        /// </summary>
        public static IObservable<float> ObserveAttributeValue(this IObservable<Entity> entities, SemanticKey attributeName)
        {
            return entities
                .Select(entity => entity == null ? Observable.Empty<float>() : entity.ObserveValue(attributeName))
                .Switch();
        }
    }
}
