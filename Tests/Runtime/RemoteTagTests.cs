using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System.Collections.Generic;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class RemoteTagTests
    {
        private AttributeProcessor _weaponProcessor;
        private AttributeProcessor _playerProcessor;
        private AttributeProcessor _newPlayerProcessor;

        [SetUp]
        public void Setup()
        {
            _weaponProcessor = new AttributeProcessor();
            _playerProcessor = new AttributeProcessor();
            _newPlayerProcessor = new AttributeProcessor();
        }

        [Test]
        public void RemoteTag_AppliedToTarget_ViaPath()
        {
            // 1. Link Weapon -> Player
            var ownerKey = TestKeys.Mock("Owner");
            _weaponProcessor.RegisterExternalProvider(ownerKey, _playerProcessor);

            // 2. Define StatBlock with Remote Tag
            var blessedTag = TestKeys.Mock("Blessed");
            var block = new StatBlock
            {
                BlockName = "Holy Sword",
                RemoteTags = new List<TagModifierSpec>
                {
                    new TagModifierSpec
                    {
                        Tag = blessedTag,
                        TargetPath = new List<SemanticKey> { ownerKey }
                    }
                }
            };

            // 3. Apply
            var handle = block.ApplyToProcessor(_weaponProcessor, null);

            // 4. Assert Player has Tag
            Assert.IsTrue(_playerProcessor.HasTag(blessedTag), "Player should have 'Blessed' tag");
            Assert.IsFalse(_weaponProcessor.HasTag(blessedTag), "Weapon itself should NOT have the tag");
        }

        [Test]
        public void RemoteTag_Removed_OnDispose()
        {
            // 1. Link & Apply
            var ownerKey = TestKeys.Mock("Owner");
            _weaponProcessor.RegisterExternalProvider(ownerKey, _playerProcessor);

            var blessedTag = TestKeys.Mock("Blessed");
            var block = new StatBlock
            {
                RemoteTags = new List<TagModifierSpec>
                {
                    new TagModifierSpec { Tag = blessedTag, TargetPath = new List<SemanticKey> { ownerKey } }
                }
            };

            var handle = block.ApplyToProcessor(_weaponProcessor, null);
            Assert.IsTrue(_playerProcessor.HasTag(blessedTag));

            // 2. Dispose (Unequip)
            handle.Dispose();

            // 3. Assert Removed
            Assert.IsFalse(_playerProcessor.HasTag(blessedTag), "Player should lose 'Blessed' tag after unequip");
        }

        [Test]
        public void RemoteTag_Moves_WhenPathChanges()
        {
            var ownerKey = TestKeys.Mock("Owner");
            var blessedTag = TestKeys.Mock("Blessed");

            // 1. Apply to Weapon (initially no owner)
            var block = new StatBlock
            {
                RemoteTags = new List<TagModifierSpec>
                {
                    new TagModifierSpec { Tag = blessedTag, TargetPath = new List<SemanticKey> { ownerKey } }
                }
            };
            var handle = block.ApplyToProcessor(_weaponProcessor, null);

            Assert.IsFalse(_playerProcessor.HasTag(blessedTag));

            // 2. Link Owner 1
            _weaponProcessor.RegisterExternalProvider(ownerKey, _playerProcessor);
            Assert.IsTrue(_playerProcessor.HasTag(blessedTag), "Tag should appear on Player 1");

            // 3. Swap Owner (Link Owner 2)
            _weaponProcessor.RegisterExternalProvider(ownerKey, _newPlayerProcessor);

            // 4. Assert Moved
            Assert.IsFalse(_playerProcessor.HasTag(blessedTag), "Tag should be removed from Player 1");
            Assert.IsTrue(_newPlayerProcessor.HasTag(blessedTag), "Tag should move to Player 2");
        }

        [Test]
        public void RemoteTags_Stack_ReferenceCount()
        {
            var ownerKey = TestKeys.Mock("Owner");
            var stunnedTag = TestKeys.Mock("Stunned");
            _weaponProcessor.RegisterExternalProvider(ownerKey, _playerProcessor);

            // Source 1: Stun Baton
            var block1 = new StatBlock
            {
                RemoteTags = new List<TagModifierSpec>
                {
                    new TagModifierSpec { Tag = stunnedTag, TargetPath = new List<SemanticKey> { ownerKey } }
                }
            };

            // Source 2: Flashbang (applied to same weapon processor context for simplicity, or another item)
            // Let's assume another item on the same connection logic
            var block2 = new StatBlock
            {
                RemoteTags = new List<TagModifierSpec>
                {
                    new TagModifierSpec { Tag = stunnedTag, TargetPath = new List<SemanticKey> { ownerKey } }
                }
            };

            var handle1 = block1.ApplyToProcessor(_weaponProcessor, null);
            var handle2 = block2.ApplyToProcessor(_weaponProcessor, null);

            // Assert Count 2
            Assert.IsTrue(_playerProcessor.HasTag(stunnedTag));
            Assert.AreEqual(2, _playerProcessor.Tags[stunnedTag]);

            // Remove 1
            handle1.Dispose();
            Assert.IsTrue(_playerProcessor.HasTag(stunnedTag), "Should still be stunned (Count 1)");
            Assert.AreEqual(1, _playerProcessor.Tags[stunnedTag]);

            // Remove 2
            handle2.Dispose();
            Assert.IsFalse(_playerProcessor.HasTag(stunnedTag), "Should no longer be stunned");
        }
    }
}