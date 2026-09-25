using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.Scripting;

namespace ReactiveSolutions.AttributeSystem.Core.Modifiers
{
    /// <summary>How <see cref="GroupTotalLogic"/> combines the members' values.</summary>
    public enum GroupOperation
    {
        Sum,
        Average,
        Min,
        Max,
        /// <summary>The number of members (the attribute isn't read).</summary>
        Count
    }

    /// <summary>
    /// An attribute totaled over the members of a link group: the Weight of everything in the Inventory (Sum), how many
    /// members the Party has (Count), and so on. It updates as members join, leave or change. A member without the
    /// attribute counts as 0, and an empty group totals 0.
    /// </summary>
    [Serializable, Preserve]
    public class GroupTotalLogic : ModifierLogic
    {
        [Tooltip("The link group: on the entity (e.g. Inventory), or on the one at the end of a provider path (e.g. Owner / Party).")]
        public AttributeReference Group;

        [Tooltip("The attribute read from each member. Count doesn't read it.")]
        public SemanticKey Attribute;

        [Tooltip("How the members' values are combined.")]
        public GroupOperation Operation = GroupOperation.Sum;

        public override IObservable<float> Observe(Entity context)
        {
            if (context == null || Group.Name == SemanticKey.None) return Observable.Return(0f);

            // The group is created if it doesn't exist yet, so members added later are counted.
            return ObserveHolder(context, Group.Path, 0)
                .Select(holder => holder == null ? Observable.Return(0f) : ObserveGroup(holder.GetOrCreateLinkGroup(Group.Name)))
                .Switch()
                .DistinctUntilChanged();
        }

        private IObservable<float> ObserveGroup(LinkGroup group)
        {
            var members = group.Members;
            return Observable.Merge(
                    members.ObserveAdd().AsUnitObservable(),
                    members.ObserveRemove().AsUnitObservable(),
                    members.ObserveReplace().AsUnitObservable(),
                    members.ObserveReset())
                .StartWith(Unit.Default)
                .Select(_ => Total(members.ToList()))
                .Switch();
        }

        private IObservable<float> Total(List<Entity> members)
        {
            if (Operation == GroupOperation.Count) return Observable.Return((float)members.Count);
            if (members.Count == 0) return Observable.Return(0f);

            var input = ValueSource.FromAttribute(Attribute);
            return Observable.CombineLatest(members.Select(member => input.GetObservable(member))).Select(Combine);
        }

        private float Combine(IList<float> values)
        {
            switch (Operation)
            {
                case GroupOperation.Average: return values.Average();
                case GroupOperation.Min: return values.Min();
                case GroupOperation.Max: return values.Max();
                default: return values.Sum();
            }
        }

        private static IObservable<Entity> ObserveHolder(Entity entity, List<SemanticKey> path, int index)
        {
            if (path == null || index >= path.Count) return Observable.Return(entity);

            return entity.ObserveProvider(path[index])
                .Select(next => next == null ? Observable.Return<Entity>(null) : ObserveHolder(next, path, index + 1))
                .Switch();
        }
    }
}
