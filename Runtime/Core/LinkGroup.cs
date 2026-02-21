using ReactiveSolutions.AttributeSystem.Core.Data;
using System;
using System.Collections.Generic;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// Represents a dynamic collection of AttributeProcessors.
    /// Manages the application of StatBlocks to all members based on reactive conditions.
    /// </summary>
    public class LinkGroup
    {
        private readonly ReactiveCollection<Entity> _members = new ReactiveCollection<Entity>();
        public IReadOnlyReactiveCollection<Entity> Members => _members;

        public void AddMember(Entity processor)
        {
            if (processor != null && !_members.Contains(processor))
            {
                _members.Add(processor);
            }
        }

        public void RemoveMember(Entity processor)
        {
            if (processor != null)
            {
                _members.Remove(processor);
            }
        }

        public bool Contains(Entity processor) => _members.Contains(processor);

        /// <summary>
        /// Applies a StatBlock to every member of this group.
        /// </summary>
        /// <param name="statBlock">The data to apply.</param>
        /// <param name="modifierFactory">Factory required to instantiate modifiers from the StatBlock.</param>
        /// <param name="condition">Optional condition (e.g. Tags) to filter application.</param>
        public IDisposable ApplyStatBlock(StatBlock statBlock, IModifierFactory modifierFactory, StatBlockCondition condition = null)
        {
            return new GroupStatBlockLogic(this, statBlock, modifierFactory, condition);
        }

        private class GroupStatBlockLogic : IDisposable
        {
            private readonly LinkGroup _group;
            private readonly StatBlock _statBlock;
            private readonly IModifierFactory _modifierFactory;
            private readonly StatBlockCondition _condition;

            // Map members to their specific logic subscription
            private readonly Dictionary<Entity, IDisposable> _memberSubscriptions = new Dictionary<Entity, IDisposable>();
            private readonly CompositeDisposable _groupLifecycle = new CompositeDisposable();

            public GroupStatBlockLogic(LinkGroup group, StatBlock statBlock, IModifierFactory modifierFactory, StatBlockCondition condition)
            {
                _group = group;
                _statBlock = statBlock;
                _modifierFactory = modifierFactory;
                _condition = condition;

                // 1. Existing members
                foreach (var member in _group.Members)
                {
                    TrackMember(member);
                }

                // 2. Future members
                _group.Members.ObserveAdd()
                    .Subscribe(evt => TrackMember(evt.Value))
                    .AddTo(_groupLifecycle);

                // 3. Removed members
                _group.Members.ObserveRemove()
                    .Subscribe(evt => UntrackMember(evt.Value))
                    .AddTo(_groupLifecycle);
            }

            private void TrackMember(Entity member)
            {
                if (_memberSubscriptions.ContainsKey(member)) return;

                // Reactive Toggle Logic:
                // Observe Condition -> Select (Observable) -> Switch
                // We wrap the ActiveStatBlock lifecycle in an Observable.Create.
                // When Switch subscribes (condition true), the block is applied.
                // When Switch unsubscribes (condition false), the block is disposed.
                var subscription = ConditionEvaluator.Observe(_condition, member)
                    .Select(matches =>
                    {
                        if (matches)
                        {
                            return Observable.Create<Unit>(observer =>
                            {
                                var activeBlock = _statBlock.ApplyToEntity(member, _modifierFactory);
                                return activeBlock; // This disposable is called when Switch unsubscribes
                            });
                        }
                        else
                        {
                            return Observable.Empty<Unit>();
                        }
                    })
                    .Switch()
                    .Subscribe();

                _memberSubscriptions.Add(member, subscription);
            }

            private void UntrackMember(Entity member)
            {
                if (_memberSubscriptions.TryGetValue(member, out var sub))
                {
                    sub.Dispose(); // This disposes the Switch(), which disposes the ActiveStatBlock
                    _memberSubscriptions.Remove(member);
                }
            }

            public void Dispose()
            {
                _groupLifecycle.Dispose();
                foreach (var sub in _memberSubscriptions.Values)
                {
                    sub.Dispose();
                }
                _memberSubscriptions.Clear();
            }
        }
    }
}