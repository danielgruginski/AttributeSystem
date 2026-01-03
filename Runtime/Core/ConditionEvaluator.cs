using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public static class ConditionEvaluator
    {
        public static IObservable<bool> Observe(StatBlockCondition condition, AttributeProcessor context)
        {
            if (condition == null) return Observable.Return(true);

            switch (condition.Type)
            {
                case StatBlockCondition.Mode.Always:
                    return Observable.Return(true);

                case StatBlockCondition.Mode.Tag:
                    return ObserveTag(condition, context);

                case StatBlockCondition.Mode.ValueComparison:
                    return ObserveComparison(condition, context);

                case StatBlockCondition.Mode.Composite:
                    return ObserveComposite(condition, context);

                default:
                    return Observable.Return(true);
            }
        }

        private static IObservable<bool> ObserveTag(StatBlockCondition cond, AttributeProcessor context)
        {
            // Resolve the target processor (Self or Remote)
            // Reuse the AttributeReference resolution logic, but we only need the processor, not an attribute.

            // 1. Determine Path
            var path = cond.TagTarget;

            IObservable<AttributeProcessor> targetStream;

            if (path == null || path.Count == 0)
            {
                targetStream = Observable.Return(context);
            }
            else
            {
                // Traverse path similar to AttributeProcessor.ObserveProvider recursion
                // We'll create a helper to observe the processor at the end of a path
                targetStream = ObservePath(context, path);
            }

            return targetStream.Select(target =>
            {
                if (target == null) return Observable.Return(false); // No target = No tag

                // Observe the Tag Dictionary directly
                // We map dictionary changes to a boolean check
                return target.Tags.ObserveCountChanged()
                    .StartWith(target.Tags.Count) // Trigger initial check
                    .Select(_ =>
                    {
                        bool hasTag = target.HasTag(cond.Tag);
                        return cond.InvertTag ? !hasTag : hasTag;
                    });
            })
            .Switch() // "Achata" o IObservable<IObservable<bool>> para IObservable<bool>
            .DistinctUntilChanged();
        }

        private static IObservable<bool> ObserveComparison(StatBlockCondition cond, AttributeProcessor context)
        {
            var streamA = cond.ValueA != null ? cond.ValueA.GetObservable(context) : Observable.Return(0f);
            var streamB = cond.ValueB != null ? cond.ValueB.GetObservable(context) : Observable.Return(0f);

            return Observable.CombineLatest(streamA, streamB, (a, b) =>
            {
                float diff = a - b;
                switch (cond.CompareOp)
                {
                    case StatBlockCondition.Comparison.Equal:
                        return Mathf.Abs(diff) <= cond.Tolerance;
                    case StatBlockCondition.Comparison.NotEqual:
                        return Mathf.Abs(diff) > cond.Tolerance;
                    case StatBlockCondition.Comparison.Greater:
                        return a > b; // Strict greater
                    case StatBlockCondition.Comparison.Less:
                        return a < b; // Strict less
                    case StatBlockCondition.Comparison.GreaterOrEqual:
                        return a >= b;
                    case StatBlockCondition.Comparison.LessOrEqual:
                        return a <= b;
                    default:
                        return true;
                }
            })
            .DistinctUntilChanged();
        }

        private static IObservable<bool> ObserveComposite(StatBlockCondition cond, AttributeProcessor context)
        {
            if (cond.SubConditions == null || cond.SubConditions.Count == 0)
                return Observable.Return(true);

            var streams = cond.SubConditions.Select(c => Observe(c, context)).ToList();

            return Observable.CombineLatest(streams).Select(results =>
            {
                if (cond.GroupOp == StatBlockCondition.Operator.And)
                {
                    return results.All(x => x);
                }
                else // OR
                {
                    return results.Any(x => x);
                }
            })
            .DistinctUntilChanged();
        }

        // Helper to resolve processor path reactively
        private static IObservable<AttributeProcessor> ObservePath(AttributeProcessor root, List<SemanticKey> path)
        {
            // This mimics PathConnection logic but purely functional
            return ResolvePathRecursively(root, path, 0);
        }

        private static IObservable<AttributeProcessor> ResolvePathRecursively(AttributeProcessor current, List<SemanticKey> path, int index)
        {
            if (index >= path.Count)
                return Observable.Return(current);

            SemanticKey nextKey = path[index];

            return current.ObserveProvider(nextKey)
                .Select(nextProcessor =>
                {
                    if (nextProcessor == null) return Observable.Return<AttributeProcessor>(null);
                    return ResolvePathRecursively(nextProcessor, path, index + 1);
                })
                .Switch();
        }
    }
}