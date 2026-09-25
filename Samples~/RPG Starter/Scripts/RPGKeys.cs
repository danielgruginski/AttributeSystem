using SemanticKeys;

namespace RPGStarter
{
    // The sample's keys, as "Generate Static Class" writes them for the KeyDomains in the Keys folder.
    // In your game, create your own KeyDomains and generate these classes (see Documentation/Semantic Keys.md).

    public static class RPGStats
    {
        public static readonly SemanticKey Level = new SemanticKey("29ef14b1-dffd-4a15-83e4-2cb2178d5fe6", "Level", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey Strength = new SemanticKey("9e256588-17ae-4ef0-b572-f1688003a231", "Strength", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey Dexterity = new SemanticKey("1db97840-ebb3-4850-b5b5-b7173754ba03", "Dexterity", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey Intelligence = new SemanticKey("7613c1c1-7183-4a90-99a5-36dabb004f0a", "Intelligence", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey Vitality = new SemanticKey("43f08865-e8c4-4c55-bfb3-00b61e59b8f9", "Vitality", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey MaxHealth = new SemanticKey("21ded034-2146-470d-ad6c-da1fa0c0633e", "MaxHealth", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey Health = new SemanticKey("0fb4fa22-7d58-465f-9c3c-7258a14b9149", "Health", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey HealthPercent = new SemanticKey("9abad6c2-6d2e-4dff-a936-828a59f7f580", "HealthPercent", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey MaxMana = new SemanticKey("3edc201d-a87e-49d3-8983-6f06d015e647", "MaxMana", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey Mana = new SemanticKey("3181edf0-d48e-443e-954b-5066d4cb22ef", "Mana", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey AttackPower = new SemanticKey("d61b2749-7a12-44df-8748-cb3100762878", "AttackPower", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey SpellPower = new SemanticKey("baaa3e21-3883-4291-ade4-85b357f1da51", "SpellPower", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey Defense = new SemanticKey("ce8f7ca3-3a79-4411-8b98-0be06c78cd1a", "Defense", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey CritChance = new SemanticKey("eab968c8-e357-4c0d-a79a-634b2a58cee8", "CritChance", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey MoveSpeed = new SemanticKey("955ca18a-b7e0-4c37-81c3-51402ddd4457", "MoveSpeed", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey CarryCapacity = new SemanticKey("737aacbf-712f-4c17-aa34-db44a3291d16", "CarryCapacity", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey CarriedWeight = new SemanticKey("b2f544bc-b74d-45be-b7c0-d9a1ebb7a7ea", "CarriedWeight", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey Damage = new SemanticKey("dccb28f9-8c40-452f-991a-a585042406bd", "Damage", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey ArmorRating = new SemanticKey("b4e1f9eb-1bcc-49ef-aaf5-d55c5d615708", "ArmorRating", "67fe491d-2da3-41d5-b731-b486bcc093a3");
        public static readonly SemanticKey Weight = new SemanticKey("6d456038-da30-4c11-96d0-a52c92072dc5", "Weight", "67fe491d-2da3-41d5-b731-b486bcc093a3");
    }

    public static class RPGTags
    {
        public static readonly SemanticKey Character = new SemanticKey("cf0d52ba-d9ff-41ce-a2e2-f9fa0b9b7484", "Character", "b1dac926-dcc5-426c-9a2c-baa00d6792f5");
        public static readonly SemanticKey Caster = new SemanticKey("cd8ccf77-20f0-472d-b57f-c13eea18d0cc", "Caster", "b1dac926-dcc5-426c-9a2c-baa00d6792f5");
        public static readonly SemanticKey Weapon = new SemanticKey("2119e832-538b-4357-bc44-d3b3a52a06e9", "Weapon", "b1dac926-dcc5-426c-9a2c-baa00d6792f5");
        public static readonly SemanticKey Armor = new SemanticKey("36ca5108-9a46-48e9-844b-d5e8a28ac709", "Armor", "b1dac926-dcc5-426c-9a2c-baa00d6792f5");
        public static readonly SemanticKey Goblin = new SemanticKey("58e178d6-3d0a-484e-9008-c47625c71f3e", "Goblin", "b1dac926-dcc5-426c-9a2c-baa00d6792f5");
        public static readonly SemanticKey Enraged = new SemanticKey("699c7a12-3f4f-4daf-a1ae-153bb185f3d2", "Enraged", "b1dac926-dcc5-426c-9a2c-baa00d6792f5");
        public static readonly SemanticKey Encumbered = new SemanticKey("57e604d9-7bd6-4bcc-ad99-4e33853526ed", "Encumbered", "b1dac926-dcc5-426c-9a2c-baa00d6792f5");
        public static readonly SemanticKey Poisoned = new SemanticKey("b95b7004-6b13-4df1-91c7-873aa465f875", "Poisoned", "b1dac926-dcc5-426c-9a2c-baa00d6792f5");
        public static readonly SemanticKey Venomous = new SemanticKey("9e56228c-943b-4550-bc40-ac935bfe8c87", "Venomous", "b1dac926-dcc5-426c-9a2c-baa00d6792f5");
        public static readonly SemanticKey Debuff = new SemanticKey("a6b3791f-f010-4321-97ba-f622c610871f", "Debuff", "b1dac926-dcc5-426c-9a2c-baa00d6792f5");
    }

    public static class RPGLinks
    {
        public static readonly SemanticKey Owner = new SemanticKey("d4e54e90-b55a-499e-a71d-cf05e6c4935f", "Owner", "48adf587-3163-487d-b58b-38f7d6e9aa44");
        public static readonly SemanticKey MainHand = new SemanticKey("af0b9161-f594-4631-8350-0a4534a97994", "MainHand", "48adf587-3163-487d-b58b-38f7d6e9aa44");
        public static readonly SemanticKey Body = new SemanticKey("434613d2-e537-45b1-8bce-6c4e72e18a19", "Body", "48adf587-3163-487d-b58b-38f7d6e9aa44");
    }

    public static class RPGGroups
    {
        public static readonly SemanticKey Inventory = new SemanticKey("ff38bdb1-cb68-4327-a63d-ccdbecd52a35", "Inventory", "419af398-c1dc-4444-a94f-1f27650eec56");
        public static readonly SemanticKey Party = new SemanticKey("7de01405-0094-4a31-a56e-9a9a75a5e411", "Party", "419af398-c1dc-4444-a94f-1f27650eec56");
    }
}
