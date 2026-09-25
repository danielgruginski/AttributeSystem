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
    /// JSON files under Resources/Data/.../RPGStarter): templates, characters, items and StatBlocks.
    /// Plain C#, so tests can run it too; RPGStarterDemo drives it from a MonoBehaviour.
    /// </summary>
    public sealed class RPGGame : IDisposable
    {
        /// <summary>The folder of the sample's profiles and StatBlocks, inside Data/EntityProfiles and Data/StatBlocks.</summary>
        public const string Data = "RPGStarter/";

        public const float FireballCost = 15f;
        public const float PotionHealing = 40f;
        public const float PoisonDamagePerSecond = 3f;

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

        private sealed class Poisoning
        {
            public ActiveStatBlock Debuff;
            public float SecondsLeft;
        }

        public RPGGame()
        {
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

        /// <summary>A weapon attack: the attacker's AttackPower, reduced by the target's Defense.</summary>
        public float Attack(Entity attacker, Entity target)
        {
            if (!IsAlive(attacker) || !IsAlive(target)) return 0f;

            float damage = Get(attacker, RPGStats.AttackPower) * 100f / (100f + Get(target, RPGStats.Defense));
            float dealt = target.GetPool(RPGStats.Health).Reduce(damage);
            Log($"{attacker.Name} hits {target.Name} for {dealt:0.#}. {HealthText(target)}");
            return dealt;
        }

        /// <summary>A spell: costs Mana, and deals 1.5 x SpellPower.</summary>
        public bool CastFireball(Entity caster, Entity target)
        {
            if (!IsAlive(caster) || !IsAlive(target)) return false;

            var mana = caster.GetPool(RPGStats.Mana);
            if (mana == null || !mana.TrySpend(FireballCost))
            {
                Log($"{caster.Name} doesn't have the Mana for a fireball.");
                return false;
            }

            float dealt = target.GetPool(RPGStats.Health).Reduce(Get(caster, RPGStats.SpellPower) * 1.5f);
            Log($"{caster.Name}'s fireball burns {target.Name} for {dealt:0.#}. {HealthText(target)}");
            return true;
        }

        public float DrinkPotion(Entity drinker)
        {
            if (!IsAlive(drinker)) return 0f;

            float healed = drinker.GetPool(RPGStats.Health).Restore(PotionHealing);
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

        /// <summary>Level is a base value; the Character template's formulas turn it into MaxHealth.</summary>
        public void LevelUp(Entity character)
        {
            character.SetOrUpdateBaseValue(RPGStats.Level, Get(character, RPGStats.Level) + 1f);
            Log($"{character.Name} reaches level {Get(character, RPGStats.Level)}. {HealthText(character)}");
        }

        /// <summary>
        /// Poisons the target for a while: the Poison StatBlock (the Poisoned tag, slower movement) while it lasts,
        /// and damage over time in <see cref="Tick"/>. Poisoning a poisoned target restarts the timer.
        /// </summary>
        public void Poison(Entity target, float seconds)
        {
            if (!IsAlive(target)) return;

            if (!_poisoned.TryGetValue(target, out var poisoning))
            {
                poisoning = new Poisoning { Debuff = StatBlockJsonLoader.Load(Data + "Debuffs/Poison").ApplyToEntity(target) };
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

        /// <summary>Advances time: poison damage, and poisons wearing off.</summary>
        public void Tick(float deltaTime)
        {
            foreach (var pair in _poisoned.ToList())
            {
                var target = pair.Key;
                var poisoning = pair.Value;

                float seconds = Math.Min(deltaTime, poisoning.SecondsLeft);
                if (IsAlive(target)) target.GetPool(RPGStats.Health).Reduce(PoisonDamagePerSecond * seconds);

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
