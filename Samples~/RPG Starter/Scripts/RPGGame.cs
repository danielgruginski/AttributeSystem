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
    /// JSON files under Resources/Data/.../RPGStarter): templates, characters, items, StatBlocks, effects and status effects.
    /// Plain C#, so tests can run it too; RPGStarterDemo drives it from a MonoBehaviour.
    /// </summary>
    public sealed class RPGGame : IDisposable
    {
        /// <summary>
        /// The folder of the sample's data, inside Data/EntityProfiles, Data/StatBlocks, Data/Effects and Data/StatusEffects.
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
        private IDisposable _blessing;

        // What happens is data: each effect's condition, costs, formulas and chances are in its JSON file, and so are
        // the poison's duration, stacks and ticks.
        private readonly Effect _weaponHit = EffectJsonLoader.Load(Data + "Combat/WeaponHit");
        private readonly Effect _fireball = EffectJsonLoader.Load(Data + "Spells/Fireball");
        private readonly Effect _healingPotion = EffectJsonLoader.Load(Data + "Consumables/HealingPotion");
        private readonly Effect _antidote = EffectJsonLoader.Load(Data + "Consumables/Antidote");
        private readonly Effect _levelUp = EffectJsonLoader.Load(Data + "Progression/LevelUp");
        private readonly StatusEffect _poison = StatusEffectJsonLoader.Load(Data + "Debuffs/Poison");
        private readonly System.Random _random;

        /// <param name="random">Rolls the critical hits and the venom. Pass one with a seed to make a game repeatable.</param>
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

            // Status effects end on their own: when they run out, when their condition stops holding, or when removed.
            _disposables.Add(entity.StatusEffects.ObserveAdd().Subscribe(added =>
            {
                var status = added.Value;
                status.Ended.Subscribe(reason => Log(
                    reason == StatusEndReason.Expired ? $"The {status.Name} on {entity.Name} wears off. {HealthText(entity)}" :
                    reason == StatusEndReason.Removed ? $"The {status.Name} on {entity.Name} is cured." :
                    $"The {status.Name} on {entity.Name} ends."));
            }));
            return entity;
        }

        /// <summary>An attribute's value, or 0 if the entity doesn't have it.</summary>
        public static float Get(Entity entity, SemanticKey attribute) =>
            entity?.GetAttribute(attribute)?.ObservableValue.Value ?? 0f;

        public static bool IsAlive(Entity entity) => entity.GetPool(RPGStats.Health) is ResourcePool health && !health.IsEmpty;

        // ---------------------------------------------------------------- Combat

        /// <summary>
        /// A weapon attack (the Weapon Hit effect): the attacker's AttackPower reduced by the target's Defense, and on a
        /// critical hit (the attacker's CritChance), the same damage again. A Venomous weapon (the Rusty Dagger) poisons
        /// 30% of the time. Returns the damage dealt.
        /// </summary>
        public float Attack(Entity attacker, Entity target)
        {
            var hit = _weaponHit.Apply(attacker, target, _random);
            if (!hit.Applied) return 0f; // One of them has fallen: the effect's condition.

            float dealt = -hit.ChangeOf(target, RPGStats.Health);
            string critical = hit.Changes.Count > 1 ? " (critical hit)" : "";
            Log($"{attacker.Name} hits {target.Name} for {dealt:0.#}{critical}. {HealthText(target)}");
            LogStatuses(hit.Statuses);
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

        /// <summary>The Antidote effect: removes the drinker's Debuffs, such as the Poison.</summary>
        public void DrinkAntidote(Entity drinker)
        {
            var antidote = _antidote.Apply(drinker, drinker);
            if (antidote.Applied) Log($"{drinker.Name} drinks an antidote.");
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
        /// Applies the Poison status effect: for 5 seconds, the Poison StatBlock (the Poisoned tag, slower movement) and
        /// 3 damage a second. Poisoning again adds a stack (up to 3), each one as much again, and restarts the 5 seconds.
        /// </summary>
        public void Poison(Entity target)
        {
            var poison = _poison?.Apply(null, target, _random);
            if (poison != null) LogStatuses(new[] { poison });
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

        /// <summary>Advances time (in seconds): the entities' status effects tick, and run out.</summary>
        public void Tick(float deltaTime)
        {
            foreach (var entity in _spawned.ToArray())
            {
                entity.TickStatusEffects(deltaTime, _random);
            }
        }

        public void Dispose()
        {
            Logged = null; // Nothing to report while the game is taken down.
            _blessing?.Dispose();
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

        /// <summary>The status effects an entity has, e.g. "Poison x2 (3.5 left)".</summary>
        public static string StatusText(Entity entity) =>
            entity.StatusEffects.Count == 0 ? "none" : string.Join(", ", entity.StatusEffects.Select(status => status.ToString()));

        public static string WeightText(Entity entity) =>
            $"(Carrying {Get(entity, RPGStats.CarriedWeight):0.#}/{Get(entity, RPGStats.CarryCapacity):0.#})";

        private void LogStatuses(IEnumerable<ActiveStatusEffect> statuses)
        {
            foreach (var status in statuses)
            {
                Log($"{status.Target.Name} is affected by {status.Name}{(status.Stacks > 1 ? $" ({status.Stacks} stacks)" : "")}.");
            }
        }

        private void Log(string message) => Logged?.Invoke(message);
    }
}
