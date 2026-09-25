using System.Collections.Generic;
using System.Linq;
using ReactiveSolutions.AttributeSystem.Core;
using UnityEngine;

namespace RPGStarter
{
    /// <summary>
    /// Plays the RPG Starter: add this component to an empty GameObject and press Play. The panel shows the characters'
    /// stats as the buttons change them; everything else is RPGGame and the sample's JSON data.
    /// </summary>
    public class RPGStarterDemo : MonoBehaviour
    {
        private RPGGame _game;
        private Entity _anvil;
        private readonly List<string> _log = new List<string>();
        private Vector2 _logScroll;

        private void Start()
        {
            _game = new RPGGame();
            _game.Logged += message =>
            {
                _log.Add(message);
                _logScroll.y = float.MaxValue;
                Debug.Log("[RPG Starter] " + message);
            };
            _anvil = _game.Spawn("Items/Anvil");
        }

        private void Update() => _game?.Tick(Time.deltaTime);

        private void OnDestroy() => _game?.Dispose();

        private void OnGUI()
        {
            if (_game == null) return;

            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));

            GUILayout.BeginHorizontal();
            DrawCharacter(_game.Knight);
            DrawCharacter(_game.Mage);
            DrawCharacter(_game.Goblin);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Knight attacks")) _game.Attack(_game.Knight, _game.Goblin);
            if (GUILayout.Button("Mage casts Fireball")) _game.CastFireball(_game.Mage, _game.Goblin);
            if (GUILayout.Button("Goblin attacks")) _game.Attack(_game.Goblin, _game.Knight);
            if (GUILayout.Button("Knight drinks a potion")) _game.DrinkPotion(_game.Knight);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Knight swaps weapons")) SwapWeapons();
            bool carriesAnvil = _game.Knight.GetLinkGroup(RPGGroups.Inventory).Contains(_anvil);
            if (GUILayout.Button(carriesAnvil ? "Knight drops the anvil" : "Knight picks up an anvil"))
            {
                if (carriesAnvil) _game.Drop(_game.Knight, _anvil);
                else _game.PickUp(_game.Knight, _anvil);
            }
            if (GUILayout.Button("Poison the Knight (5 s)")) _game.Poison(_game.Knight, 5f);
            if (GUILayout.Button("Level up the Knight")) _game.LevelUp(_game.Knight);
            if (GUILayout.Button(_game.IsBlessed ? "End the Blessing" : "Bless the party")) _game.ToggleBlessing();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(160));
            foreach (var message in _log) GUILayout.Label(message);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void SwapWeapons()
        {
            var inventory = _game.Knight.GetLinkGroup(RPGGroups.Inventory);
            var weapon = inventory.Members.FirstOrDefault(item => item.HasTag(RPGTags.Weapon));
            if (weapon != null) _game.Equip(_game.Knight, RPGLinks.MainHand, weapon);
        }

        private static void DrawCharacter(Entity character)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(260));
            GUILayout.Label($"{character.Name}   (level {RPGGame.Get(character, RPGStats.Level)})");
            GUILayout.Label(Pool(character, RPGStats.Health));
            if (character.GetPool(RPGStats.Mana) != null) GUILayout.Label(Pool(character, RPGStats.Mana));

            GUILayout.Label($"AttackPower {RPGGame.Get(character, RPGStats.AttackPower):0.#}   Defense {RPGGame.Get(character, RPGStats.Defense):0.#}");
            if (RPGGame.Get(character, RPGStats.SpellPower) > 0f) GUILayout.Label($"SpellPower {RPGGame.Get(character, RPGStats.SpellPower):0.#}");
            GUILayout.Label($"Crit {RPGGame.Get(character, RPGStats.CritChance) * 100f:0.#}%   Speed {RPGGame.Get(character, RPGStats.MoveSpeed):0.#}");
            if (character.GetAttribute(RPGStats.CarryCapacity) != null) GUILayout.Label($"Carrying {RPGGame.WeightText(character)}");

            var weapon = character.GetProvider(RPGLinks.MainHand);
            if (weapon != null) GUILayout.Label($"Main hand: {weapon.Name} (Damage {RPGGame.Get(weapon, RPGStats.Damage):0.#})");

            var tags = character.Tags.Select(tag => tag.Key.Value).OrderBy(name => name);
            GUILayout.Label("Tags: " + string.Join(", ", tags));
            GUILayout.EndVertical();
        }

        private static string Pool(Entity character, SemanticKeys.SemanticKey resource)
        {
            var pool = character.GetPool(resource);
            return $"{resource.Value} {pool.Current:0.#} / {pool.Max:0.#}";
        }
    }
}
