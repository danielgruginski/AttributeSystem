using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System.Collections.Generic;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class AttributeTagTests
    {
        private AttributeProcessor _processor;

        [SetUp]
        public void Setup()
        {
            _processor = new AttributeProcessor();
        }

        // ========================================================================
        // 1. ATTRIBUTE TAGS (Simple HashSet)
        // ========================================================================

        /*
         * This is a test for Attribute-level tags, which have not been implemented.
         * [Test]
        public void Attribute_AddRemoveTag_WorksCorrectly()
        {
            var key = TestKeys.Mock("Strength");
            var attr = _processor.GetOrCreateAttribute(key);

            // 1. Add Tag
            attr.AddTag(TestKeys.Mock("Primary"));
            Assert.IsTrue(attr.HasTag(TestKeys.Mock("Primary")));
            Assert.IsFalse(attr.HasTag(TestKeys.Mock("Secondary")));

            // 2. Remove Tag
            attr.RemoveTag(TestKeys.Mock("Primary"));
            Assert.IsFalse(attr.HasTag(TestKeys.Mock("Primary")));
        }*/

        // ========================================================================
        // 2. ENTITY TAGS (Reference Counting via AttributeTagManager)
        // ========================================================================

        [Test]
        public void Processor_AddTag_IncrementsRefCount()
        {
            var tag = TestKeys.Mock("Stunned");

            // 1. First Add
            _processor.AddTag(tag);
            Assert.IsTrue(_processor.HasTag(tag));
            Assert.AreEqual(1, _processor.Tags[tag]);

            // 2. Second Add (Overlapping Effect)
            _processor.AddTag(tag);
            Assert.IsTrue(_processor.HasTag(tag));
            Assert.AreEqual(2, _processor.Tags[tag]);
        }

        [Test]
        public void Processor_RemoveTag_DecrementsRefCount()
        {
            var tag = TestKeys.Mock("Stunned");
            _processor.AddTag(tag); // Count 1
            _processor.AddTag(tag); // Count 2

            // 1. Remove one instance
            _processor.RemoveTag(tag);
            Assert.IsTrue(_processor.HasTag(tag), "Tag should still exist (Count 1)");
            Assert.AreEqual(1, _processor.Tags[tag]);

            // 2. Remove last instance
            _processor.RemoveTag(tag);
            Assert.IsFalse(_processor.HasTag(tag), "Tag should be removed (Count 0)");
            Assert.IsFalse(_processor.Tags.ContainsKey(tag));
        }

        // ========================================================================
        // 3. STAT BLOCK INTEGRATION
        // ========================================================================

        [Test]
        public void StatBlock_Apply_AddsTags()
        {
            // Setup StatBlock
            var block = new StatBlock
            {
                BlockName = "Cursed Item",
                Tags = new List<SemanticKey> { TestKeys.Mock("Cursed"), TestKeys.Mock("Magical") }
            };

            // Apply
            var handle = block.ApplyToProcessor(_processor, null);

            // Assert Tags Applied
            Assert.IsTrue(_processor.HasTag(TestKeys.Mock("Cursed")));
            Assert.IsTrue(_processor.HasTag(TestKeys.Mock("Magical")));
            Assert.AreEqual(1, _processor.Tags[TestKeys.Mock("Cursed")]);
        }

        [Test]
        public void StatBlock_Dispose_RemovesTags()
        {
            // Setup StatBlock with tags
            var block = new StatBlock
            {
                Tags = new List<SemanticKey> { TestKeys.Mock("Buffed") }
            };

            // Pre-condition: Tag might already exist from another source
            // Let's test the overlap scenario directly here too
            _processor.AddTag(TestKeys.Mock("Buffed")); // Count 1 (Base state)

            // Apply Block
            var handle = block.ApplyToProcessor(_processor, null);
            Assert.AreEqual(2, _processor.Tags[TestKeys.Mock("Buffed")], "Count should be 2 (Base + Block)");

            // Dispose Block (Unequip)
            handle.Dispose();

            // Assert
            Assert.AreEqual(1, _processor.Tags[TestKeys.Mock("Buffed")], "Count should revert to 1");
            Assert.IsTrue(_processor.HasTag(TestKeys.Mock("Buffed")));

            // Clean up base
            _processor.RemoveTag(TestKeys.Mock("Buffed"));
            Assert.IsFalse(_processor.HasTag(TestKeys.Mock("Buffed")));
        }
    }
}