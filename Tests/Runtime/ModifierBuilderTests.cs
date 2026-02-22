using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using sk;
using ModifierBuilder = ReactiveSolutions.AttributeSystem.Core.Builders.ModifierBuilder;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class ModifierBuilderTests
    {
        private SemanticKey _healthKey;
        private SemanticKey _damageKey;
        private SemanticKey _strengthKey;
        private SemanticKey _ownerPath;
        private SemanticKey _weaponSourceId;
        private SemanticKey _customLogic;

        [SetUp]
        public void Setup()
        {
            // Initialize dummy keys for testing
            _healthKey = new SemanticKey("Health", "Health", null);
            _damageKey = new SemanticKey("Damage", "Damage", null);
            _strengthKey = new SemanticKey("Strength", "Strength", null);
            _ownerPath = new SemanticKey("Owner", "Owner", null);
            _weaponSourceId = new SemanticKey("WeaponSource", "WeaponSource", null);
            _customLogic = new SemanticKey("CustomLogic", "CustomLogic", null);
        }

        [Test]
        public void ModifierBuilder_ManualSetup_PopulatesSpecCorrectly()
        {
            var spec = ModifierBuilder.Create()
                .SetTarget(_healthKey, _ownerPath)
                .SetSourceId(_weaponSourceId)
                .SetLogic(_customLogic, ModifierType.Additive, 50)
                .AddConstantArg(15f)
                .Build();

            Assert.AreEqual(_healthKey, spec.TargetAttribute);
            Assert.AreEqual(1, spec.TargetPath.Count);
            Assert.AreEqual(_ownerPath, spec.TargetPath[0]);

            Assert.AreEqual(_weaponSourceId.Value, spec.SourceId);
            Assert.AreEqual(_customLogic, spec.LogicType);
            Assert.AreEqual(ModifierType.Additive, spec.Type);
            Assert.AreEqual(50, spec.Priority);

            Assert.AreEqual(1, spec.Arguments.Count);
            Assert.AreEqual(ValueSource.SourceMode.Constant, spec.Arguments[0].Mode);
            Assert.AreEqual(15f, spec.Arguments[0].ConstantValue);
        }

        [Test]
        public void ModifierBuilder_AddAttributeArg_PopulatesReferenceCorrectly()
        {
            var spec = ModifierBuilder.Create()
                .AddAttributeArg(_strengthKey, _ownerPath)
                .Build();

            Assert.AreEqual(1, spec.Arguments.Count);

            var arg = spec.Arguments[0];
            Assert.AreEqual(ValueSource.SourceMode.Attribute, arg.Mode);
            Assert.AreEqual(_strengthKey, arg.AttributeRef.Name);
            Assert.AreEqual(1, arg.AttributeRef.Path.Count);
            Assert.AreEqual(_ownerPath, arg.AttributeRef.Path[0]);
        }

        [Test]
        public void ModifierBuilder_MakeFlat_GeneratesCorrectSpec()
        {
            var spec = ModifierBuilder.Create()
                .MakeFlat(_damageKey, 25f)
                .Build();

            Assert.AreEqual(_damageKey, spec.TargetAttribute);
            Assert.AreEqual(sk.Modifiers.Static, spec.LogicType);
            Assert.AreEqual(ModifierType.Additive, spec.Type);

            Assert.AreEqual(1, spec.Arguments.Count);
            Assert.AreEqual(ValueSource.SourceMode.Constant, spec.Arguments[0].Mode);
            Assert.AreEqual(25f, spec.Arguments[0].ConstantValue);
        }

        [Test]
        public void ModifierBuilder_MakeMultiplier_GeneratesCorrectSpec()
        {
            var spec = ModifierBuilder.Create()
                .MakeMultiplier(_healthKey, 0.5f) // +50%
                .Build();

            Assert.AreEqual(_healthKey, spec.TargetAttribute);
            Assert.AreEqual(sk.Modifiers.Static, spec.LogicType);
            Assert.AreEqual(ModifierType.Multiplicative, spec.Type);

            Assert.AreEqual(1, spec.Arguments.Count);
            Assert.AreEqual(ValueSource.SourceMode.Constant, spec.Arguments[0].Mode);
            Assert.AreEqual(0.5f, spec.Arguments[0].ConstantValue);
        }

        [Test]
        public void ModifierBuilder_MakeLinearScaling_GeneratesCorrectSpec()
        {
            // E.g., Damage += Owner.Strength * 1.5
            var spec = ModifierBuilder.Create()
                .MakeLinearScaling(_damageKey, _strengthKey, 1.5f, _ownerPath)
                .Build();

            Assert.AreEqual(_damageKey, spec.TargetAttribute);
            Assert.AreEqual(sk.Modifiers.Linear, spec.LogicType);
            Assert.AreEqual(ModifierType.Additive, spec.Type);

            Assert.AreEqual(2, spec.Arguments.Count);

            // Arg 0: The attribute reference (Owner.Strength)
            var attrArg = spec.Arguments[0];
            Assert.AreEqual(ValueSource.SourceMode.Attribute, attrArg.Mode);
            Assert.AreEqual(_strengthKey, attrArg.AttributeRef.Name);
            Assert.AreEqual(1, attrArg.AttributeRef.Path.Count);
            Assert.AreEqual(_ownerPath, attrArg.AttributeRef.Path[0]);

            // Arg 1: The multiplier (1.5)
            var multArg = spec.Arguments[1];
            Assert.AreEqual(ValueSource.SourceMode.Constant, multArg.Mode);
            Assert.AreEqual(1.5f, multArg.ConstantValue);
        }
    }
}