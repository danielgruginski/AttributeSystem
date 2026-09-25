using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using UniRx;

namespace RPGStarter
{
    /// <summary>
    /// The game code a project writes on top of the Attribute System: spawning characters from their profiles,
    /// combat, equipment, inventory, the party, leveling up and a poison. The rules and numbers are in the data (the
    /// JSON files under Resources/Data/.../RPGStarter): templates, characters, items, StatBlocks and effects.
    /// Plain C#, so tests can run it too; RPGStarterDemo drives it from a MonoBehaviour.
    /// </summary>
    public sealed class RPGGame : IDisposable
    {
        /// <summary>
        /// The folder of the sample's data, inside Data/EntityProfiles, Data/StatBlocks and Data/Effects.
        /// </summary>
        public const string Data = "RPGStarter/";

        public Entity Knight { get; }
        public Entity Mage { get; }
        public Entity Goblin { get; }

        /// <summary>The party the Knight leads. Its members get the Leadership aura.</summary>
        public LinkGroup Party { get; }

        /// <summary>What happens in the game, for the demo's log.</summary>
        public event Action<string> Logged;

        private readonly List<Entity> _spawned = new List<Entity>();
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly Dictionary<Entity, Poisoning> _poisoned = new Dictionary<Entity, Poisoning>();
        private IDisposable _blessing;

        // What happens is data: each effect's condition, costs, formulas and chances are in its JSON file.
        private readonly Effect _weaponHit = EffectJsonLoader.Load(Data + "Combat/WeaponHit");
        private readonly Effect _fireball = EffectJsonLoader.Load(Data + "Spells/Fireball");
        private readonly Effect _healingPotion = EffectJsonLoader.Load(Data + "Consumables/HealingPotion");
        private readonly Effect _poisonTick = EffectJsonLoader.Load(Data + "Debuffs/PoisonTick");
        private readonly Effect _levelUp = EffectJsonLoader.Load(Data + "Progression/LevelUp");
        private readonly System.Random _random;

        private sealed class Poisoning
        {
            public ActiveStatBlock Debuff;
            public float SecondsLeft;
            public float UntilTick;
        }

        /// <param name="random">Rolls the critical hits. Pass one with a seed to make a game repeatable.</param>
        public RPGGame(System.Random random = null)
        {
            _random = random ?? new System.Random();

            Knight = Spawn("Heroes/Knight");
            Mage = Spawn("Heroes/Mage");
            Goblin = Spawn("Monsters/Goblin");

            // The Knight carries a spare dagger.
            Knight.GetLinkGroup(RPGGroups.Inventory).AddMember(Spawn("Items/RustyDagger"));

            // A link group applies a StatBlock to each of its members, including members that join later.
            Party = Knight.GetOrCreateLinkGroup(RPGGroups.Party);
            Party.AddMember(Knight);
            Party.AddMember(Mage);
            _disposables.Add(Party.ApplyStatBlock(StatBlockJsonLoader.Load(Data + "Auras/Leadership")));
        }

        /// <summary>Creates an entity from a profile of the sample (e.g. "Heroes/Knight"). The game disposes it.</summary>
        public Entity Spawn(string profileId)
        {
            var entity = new Entity();
            entity.ApplyProfile(EntityProfileJsonLoader.Load(Data + profileId));
            _spawned.Add(entity);

            var health = entity.GetPool(RPGStats.Health);
            if (health != null) _disposables.Add(health.Depleted.Subscribe(_ => Log($"{entity.Name} falls.")));
            return entity;
        }

        /// <summary>An attribute's value, or 0 if the entity doesn't have it.</summary>
        public static float Get(Entity entity, SemanticKey attribute) =>
            entity?.GetAttribute(attribute)?.ObservableValue.Value ?? 0f;

        public static bool IsAlive(Entity entity) => entity.GetPool(RPGStats.Health) is ResourcePool health && !health.IsEmpty;

        // ---------------------------------------------------------------- Combat

        /// <summary>
        /// A weapon attack (the Weapon Hit effect): the attacker's AttackPower reduced by the target's Defense, and on a
        /// critical hit (the attacker's CritChance), the same damage again. Returns the damage dealt.
        /// </summary>
        public float Attack(Entity attacker, Entity target)
        {
            var hit = _weaponHit.Apply(attacker, target, _random);
            if (!hit.Applied) return 0f; // One of them has fallen: the effect's condition.

            float dealt = -hit.ChangeOf(target, RPGStats.Health);
            string critical = hit.Changes.Count > 1 ? " (critical hit)" : "";
            Log($"{attacker.Name} hits {target.Name} for {dealt:0.#}{critical}. {HealthText(target)}");
            return dealt;
        }

        /// <summary>A spell (the Fireball effect): costs 15 Mana, and deals 1.5 x SpellPower.</summary>
        public bool CastFireball(Entity caster, Entity target)
        {
            var fireball = _fireball.Apply(caster, target, _random);
            if (fireball.Status == EffectStatus.CannotPay)
            {
                Log($"{caster.Name} doesn't have the {fireball.UnpaidCost.Name} for a fireball.");
                return false;
            }
            if (!fireball.Applied) return false;

            float dealt = -fireball.ChangeOf(target, RPGStats.Health);
            Log($"{caster.Name}'s fireball burns {target.Name} for {dealt:0.#}. {HealthText(target)}");
            return true;
        }

        /// <summary>The Healing Potion effect: +40 Health, never above MaxHealth. Returns the Health restored.</summary>
        public float DrinkPotion(Entity drinker)
        {
            var potion = _healingPotion.Apply(drinker, drinker);
            if (!potion.Applied) return 0f;

            float healed = potion.ChangeOf(drinker, RPGStats.Health);
            Log($"{drinker.Name} drinks a potion and heals {healed:0.#}. {HealthText(drinker)}");
            return healed;
        }

        // ---------------------------------------------------------------- Inventory and equipment

        /// <summary>Puts an item in the character's Inventory: its Weight counts toward CarriedWeight.</summary>
        public void PickUp(Entity character, Entity item)
        {
            character.GetLinkGroup(RPGGroups.Inventory).AddMember(item);
            Log($"{character.Name} picks up the {item.Name}. {WeightText(character)}");
        }

        public void Drop(Entity character, Entity item)
        {
            character.GetLinkGroup(RPGGroups.Inventory).RemoveMember(item);
            Log($"{character.Name} drops the {item.Name}. {WeightText(character)}");
        }

        /// <summary>
        /// Equips an item from the Inventory in a slot (e.g. MainHand). The item in the slot goes back to the Inventory.
        /// Attach links both ways: the item reaches the character as its Owner, so its "Wielded" block applies.
        /// </summary>
        public void Equip(Entity character, SemanticKey slot, Entity item)
        {
            var inventory = character.GetLinkGroup(RPGGroups.Inventory);
            var previous = character.Detach(slot);
            if (previous != null) inventory.AddMember(previous);

            inventory.RemoveMember(item);
            character.Attach(slot, item);
            Log($"{character.Name} equips the {item.Name}. AttackPower {Get(character, RPGStats.AttackPower):0.#}.");
        }

        // ---------------------------------------------------------------- Progression and effects

        /// <summary>
        /// The Level Up effect: +1 Level, a base value. The Character template's formulas turn it into MaxHealth.
        /// </summary>
        public void LevelUp(Entity character)
        {
            _levelUp.Apply(character, character);
            Log($"{character.Name} reaches level {Get(character, RPGStats.Level)}. {HealthText(character)}");
        }

        /// <summary>
        /// Poisons the target for a while: the Poison StatBlock (the Poisoned tag, slower movement) while it lasts, and
        /// the Poison Tick effect (3 damage) every second, in <see cref="Tick"/>. Poisoning a poisoned target restarts
        /// the timer.
        /// </summary>
        public void Poison(Entity target, float seconds)
        {
            if (!IsAlive(target)) return;

            if (!_poisoned.TryGetValue(target, out var poisoning))
            {
                poisoning = new Poisoning
                {
                    Debuff = StatBlockJsonLoader.Load(Data + "Debuffs/Poison").ApplyToEntity(target),
                    UntilTick = 1f
                };
                _poisoned.Add(target, poisoning);
            }
            poisoning.SecondsLeft = seconds;
            Log($"{target.Name} is poisoned for {seconds:0.#} seconds.");
        }

        /// <summary>The party's Blessing (+MaxHealth) on or off. Health pools keep their percentage.</summary>
        public void ToggleBlessing()
        {
            if (_blessing == null)
            {
                _blessing = Party.ApplyStatBlock(StatBlockJsonLoader.Load(Data + "Buffs/Blessing"));
                Log("The party is blessed: +20 MaxHealth.");
            }
            else
            {
                _blessing.Dispose();
                _blessing = null;
                Log("The blessing fades.");
            }
        }

        public bool IsBlessed => _blessing != null;

        /// <summary>Advances time: poison ticks, and poisons wearing off.</summary>
        public void Tick(float deltaTime)
        {
            foreach (var pair in _poisoned.ToList())
            {
                var target = pair.Key;
                var poisoning = pair.Value;

                // A tick every whole second the poison lasts.
                poisoning.UntilTick -= Math.Min(deltaTime, poisoning.SecondsLeft);
                while (poisoning.UntilTick <= 0f && IsAlive(target))
                {
                    _poisonTick.Apply(null, target);
                    poisoning.UntilTick += 1f;
                }

                poisoning.SecondsLeft -= deltaTime;
                if (poisoning.SecondsLeft <= 0f || !IsAlive(target))
                {
                    poisoning.Debuff.Dispose();
                    _poisoned.Remove(target);
                    Log($"The poison on {target.Name} wears off. {HealthText(target)}");
                }
            }
        }

        public void Dispose()
        {
            _blessing?.Dispose();
            foreach (var poisoning in _poisoned.Values) poisoning.Debuff.Dispose();
            _poisoned.Clear();
            _disposables.Dispose();
            foreach (var entity in _spawned) entity.Dispose();
            _spawned.Clear();
        }

        // ---------------------------------------------------------------- Text

        public static string HealthText(Entity entity)
        {
            var health = entity.GetPool(RPGStats.Health);
            return health == null ? "" : $"({entity.Name}: {health.Current:0.#}/{health.Max:0.#} Health)";
        }

        public static string WeightText(Entity entity) =>
            $"(Carrying {Get(entity, RPGStats.CarriedWeight):0.#}/{Get(entity, RPGStats.CarryCapacity):0.#})";

        private void Log(string message) => Logged?.Invoke(message);
    }
}
