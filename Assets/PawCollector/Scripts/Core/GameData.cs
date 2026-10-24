using System.Collections.Generic;
using UnityEngine;

namespace PawCollector
{
    public enum Rarity { Common, Uncommon, Rare, Legendary }

    public sealed class ItemDef
    {
        public string Id, Label;
        public int Cost, Uses, UnlockLevel;
        public ItemDef(string id, string label, int cost, int uses, int unlock) { Id = id; Label = label; Cost = cost; Uses = uses; UnlockLevel = unlock; }
    }

    public sealed class CatDef
    {
        public string Id, Breed, Personality, Body = "Short";
        public float Scale = 1f;
        public Rarity Rarity;
        public Dictionary<string, float> Likes;
        public Vector2Int Gift;
        public int MinLevel, MinItemsPlaced;
        public float VoicePitch;
        public float Playful = 0.5f, Curious = 0.5f, Sleepy = 0.5f, Hungry = 0.5f, Social = 0.5f;
        public string FavouriteItem
        {
            get { string best = null; float w = -1; foreach (var kv in Likes) if (kv.Value > w) { w = kv.Value; best = kv.Key; } return best; }
        }
    }

    /// <summary>Static design data — mirrors the GDD tables.</summary>
    public static class GameData
    {
        public static readonly List<ItemDef> Items = new()
        {
            new ItemDef("Bowl", "Fish bowl", 5, 3, 1),
            new ItemDef("Yarn", "Yarn ball", 10, 4, 1),
            new ItemDef("Box", "Cardboard box", 8, 4, 1),
            new ItemDef("Cushion", "Pink cushion", 15, 5, 2),
            new ItemDef("Wand", "Feather wand", 20, 4, 3),
            new ItemDef("Blanket", "Sunny blanket", 25, 6, 4),
        };

        public static readonly List<CatDef> Cats = new()
        {
            new CatDef { Id = "Mochi", Body = "Short", Breed = "Ginger tabby", Personality = "Greedy & friendly", Rarity = Rarity.Common, Playful = 0.4f, Curious = 0.5f, Sleepy = 0.5f, Hungry = 0.9f, Social = 0.7f,
                Likes = new() { { "Bowl", 3.0f }, { "Yarn", 1.0f }, { "Blanket", 1.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 1, VoicePitch = 1.0f, Scale = 1f },
            new CatDef { Id = "Pebble", Body = "Short", Breed = "Grey tabby", Personality = "Shy & curious", Rarity = Rarity.Common, Playful = 0.5f, Curious = 0.9f, Sleepy = 0.6f, Hungry = 0.5f, Social = 0.4f,
                Likes = new() { { "Box", 3.0f }, { "Cushion", 1.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 1, VoicePitch = 0.9f, Scale = 1f },
            new CatDef { Id = "Biscuit", Body = "Chonky", Breed = "Cream chonk", Personality = "Sleepy foodie", Rarity = Rarity.Common, Playful = 0.2f, Curious = 0.3f, Sleepy = 0.9f, Hungry = 0.9f, Social = 0.6f,
                Likes = new() { { "Bowl", 2.5f }, { "Blanket", 2.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 1, VoicePitch = 0.85f, Scale = 1.04f },
            new CatDef { Id = "Toffee", Body = "Short", Breed = "Brown tabby", Personality = "Steady & sweet", Rarity = Rarity.Common, Playful = 0.4f, Curious = 0.5f, Sleepy = 0.6f, Hungry = 0.6f, Social = 0.7f,
                Likes = new() { { "Cushion", 2.0f }, { "Bowl", 1.5f } }, Gift = new Vector2Int(8, 14), MinLevel = 1, VoicePitch = 1.0f, Scale = 1f },
            new CatDef { Id = "Snowy", Body = "Fluffy", Breed = "White longhair", Personality = "Dainty & proud", Rarity = Rarity.Common, Playful = 0.3f, Curious = 0.4f, Sleepy = 0.7f, Hungry = 0.4f, Social = 0.4f,
                Likes = new() { { "Cushion", 3.0f }, { "Blanket", 1.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 2, VoicePitch = 1.2f, Scale = 1f },
            new CatDef { Id = "Pumpkin", Body = "Chonky", Breed = "Ginger chonk", Personality = "Jolly glutton", Rarity = Rarity.Common, Playful = 0.5f, Curious = 0.4f, Sleepy = 0.6f, Hungry = 1.0f, Social = 0.8f,
                Likes = new() { { "Bowl", 3.0f }, { "Box", 1.5f } }, Gift = new Vector2Int(8, 14), MinLevel = 2, VoicePitch = 0.85f, Scale = 1.04f },
            new CatDef { Id = "Pepper", Body = "Short", Breed = "Black & white", Personality = "Zoomies expert", Rarity = Rarity.Common, Playful = 0.9f, Curious = 0.7f, Sleepy = 0.3f, Hungry = 0.5f, Social = 0.6f,
                Likes = new() { { "Yarn", 2.5f }, { "Wand", 2.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 2, VoicePitch = 1.1f, Scale = 1f },
            new CatDef { Id = "Maple", Body = "Bobtail", Breed = "Red bobtail", Personality = "Bouncy explorer", Rarity = Rarity.Common, Playful = 0.8f, Curious = 0.8f, Sleepy = 0.3f, Hungry = 0.5f, Social = 0.6f,
                Likes = new() { { "Box", 2.0f }, { "Yarn", 2.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 2, VoicePitch = 1.05f, Scale = 1f },
            new CatDef { Id = "Dusty", Body = "Round", Breed = "Blue roundface", Personality = "Unbothered", Rarity = Rarity.Common, Playful = 0.2f, Curious = 0.3f, Sleepy = 0.8f, Hungry = 0.5f, Social = 0.3f,
                Likes = new() { { "Blanket", 2.5f }, { "Cushion", 2.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 3, VoicePitch = 0.8f, Scale = 1f },
            new CatDef { Id = "Clover", Body = "Kitten", Breed = "Grey & white kitten", Personality = "Tiny troublemaker", Rarity = Rarity.Common, Playful = 1.0f, Curious = 0.9f, Sleepy = 0.4f, Hungry = 0.6f, Social = 0.8f,
                Likes = new() { { "Yarn", 3.0f }, { "Wand", 2.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 3, VoicePitch = 1.4f, Scale = 0.74f },
            new CatDef { Id = "Muffin", Body = "Kitten", Breed = "Cream tabby kitten", Personality = "Cuddle bug", Rarity = Rarity.Common, Playful = 0.7f, Curious = 0.6f, Sleepy = 0.7f, Hungry = 0.6f, Social = 0.9f,
                Likes = new() { { "Blanket", 2.5f }, { "Cushion", 2.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 3, VoicePitch = 1.35f, Scale = 0.74f },
            new CatDef { Id = "Sardine", Body = "Short", Breed = "Silver tabby", Personality = "Fish fanatic", Rarity = Rarity.Common, Playful = 0.5f, Curious = 0.6f, Sleepy = 0.4f, Hungry = 1.0f, Social = 0.5f,
                Likes = new() { { "Bowl", 3.5f } }, Gift = new Vector2Int(8, 14), MinLevel = 3, VoicePitch = 1.0f, Scale = 1f },
            new CatDef { Id = "Bean", Body = "Chonky", Breed = "Brown & white chonk", Personality = "Gentle giant", Rarity = Rarity.Common, Playful = 0.3f, Curious = 0.4f, Sleepy = 0.8f, Hungry = 0.7f, Social = 0.7f,
                Likes = new() { { "Cushion", 2.0f }, { "Box", 2.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 4, VoicePitch = 0.8f, Scale = 1.04f },
            new CatDef { Id = "Tofu", Body = "Round", Breed = "Grey-capped van", Personality = "Calm observer", Rarity = Rarity.Common, Playful = 0.3f, Curious = 0.7f, Sleepy = 0.6f, Hungry = 0.4f, Social = 0.4f,
                Likes = new() { { "Box", 2.5f }, { "Blanket", 1.5f } }, Gift = new Vector2Int(8, 14), MinLevel = 4, VoicePitch = 0.9f, Scale = 1f },
            new CatDef { Id = "Waffle", Body = "Fluffy", Breed = "Golden longhair", Personality = "Sunny charmer", Rarity = Rarity.Common, Playful = 0.6f, Curious = 0.5f, Sleepy = 0.6f, Hungry = 0.6f, Social = 0.9f,
                Likes = new() { { "Blanket", 3.0f }, { "Wand", 1.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 4, VoicePitch = 1.0f, Scale = 1f },
            new CatDef { Id = "Cocoa", Body = "Short", Breed = "Chocolate", Personality = "Quiet thinker", Rarity = Rarity.Common, Playful = 0.4f, Curious = 0.8f, Sleepy = 0.6f, Hungry = 0.5f, Social = 0.3f,
                Likes = new() { { "Box", 2.0f }, { "Cushion", 1.5f } }, Gift = new Vector2Int(8, 14), MinLevel = 4, VoicePitch = 0.95f, Scale = 1f },
            new CatDef { Id = "Ziggy", Body = "Slim", Breed = "Ticked ruddy", Personality = "Restless sprinter", Rarity = Rarity.Common, Playful = 1.0f, Curious = 0.9f, Sleepy = 0.2f, Hungry = 0.4f, Social = 0.5f,
                Likes = new() { { "Wand", 3.0f }, { "Yarn", 1.5f } }, Gift = new Vector2Int(8, 14), MinLevel = 5, VoicePitch = 1.15f, Scale = 1f },
            new CatDef { Id = "Button", Body = "Fold", Breed = "Grey & white fold", Personality = "Polite owl-face", Rarity = Rarity.Common, Playful = 0.4f, Curious = 0.6f, Sleepy = 0.7f, Hungry = 0.5f, Social = 0.7f,
                Likes = new() { { "Cushion", 2.5f }, { "Bowl", 1.0f } }, Gift = new Vector2Int(8, 14), MinLevel = 5, VoicePitch = 1.1f, Scale = 1f },
            new CatDef { Id = "Oreo", Body = "Short", Breed = "Tuxedo", Personality = "Playful show-off", Rarity = Rarity.Uncommon, Playful = 0.95f, Curious = 0.6f, Sleepy = 0.3f, Hungry = 0.5f, Social = 0.8f,
                Likes = new() { { "Yarn", 3.0f }, { "Wand", 2.0f }, { "Box", 1.0f } }, Gift = new Vector2Int(12, 18), MinLevel = 2, VoicePitch = 1.1f, Scale = 1f },
            new CatDef { Id = "Suki", Body = "Slim", Breed = "Siamese", Personality = "Elegant & aloof", Rarity = Rarity.Uncommon, Playful = 0.3f, Curious = 0.4f, Sleepy = 0.8f, Hungry = 0.4f, Social = 0.3f,
                Likes = new() { { "Cushion", 3.0f }, { "Blanket", 2.0f }, { "Bowl", 0.5f } }, Gift = new Vector2Int(12, 18), MinLevel = 2, VoicePitch = 1.2f, Scale = 1f },
            new CatDef { Id = "Patches", Body = "Short", Breed = "Calico", Personality = "Easygoing", Rarity = Rarity.Uncommon, Playful = 0.6f, Curious = 0.7f, Sleepy = 0.5f, Hungry = 0.6f, Social = 0.9f,
                Likes = new() { { "Bowl", 1.2f }, { "Yarn", 1.2f }, { "Box", 1.2f }, { "Cushion", 1.2f }, { "Wand", 1.2f }, { "Blanket", 1.2f } }, Gift = new Vector2Int(12, 18), MinLevel = 2, VoicePitch = 1.05f, Scale = 1f },
            new CatDef { Id = "Nori", Body = "Rex", Breed = "Smoke rex", Personality = "Dawn wanderer", Rarity = Rarity.Uncommon, Playful = 0.6f, Curious = 0.9f, Sleepy = 0.4f, Hungry = 0.5f, Social = 0.4f,
                Likes = new() { { "Box", 2.5f }, { "Wand", 1.5f } }, Gift = new Vector2Int(12, 18), MinLevel = 3, VoicePitch = 1.0f, Scale = 1f },
            new CatDef { Id = "Hazel", Body = "Fold", Breed = "Tortoiseshell fold", Personality = "Moody & loyal", Rarity = Rarity.Uncommon, Playful = 0.4f, Curious = 0.5f, Sleepy = 0.7f, Hungry = 0.6f, Social = 0.5f,
                Likes = new() { { "Blanket", 2.5f }, { "Box", 1.5f } }, Gift = new Vector2Int(12, 18), MinLevel = 3, VoicePitch = 1.0f, Scale = 1f },
            new CatDef { Id = "Peaches", Body = "Fluffy", Breed = "Peach colourpoint", Personality = "Pampered princess", Rarity = Rarity.Uncommon, Playful = 0.3f, Curious = 0.4f, Sleepy = 0.8f, Hungry = 0.7f, Social = 0.6f,
                Likes = new() { { "Cushion", 3.0f }, { "Bowl", 1.5f } }, Gift = new Vector2Int(12, 18), MinLevel = 4, VoicePitch = 1.25f, Scale = 1f },
            new CatDef { Id = "Ash", Body = "Lynx", Breed = "Silver lynx", Personality = "Wise hunter", Rarity = Rarity.Uncommon, Playful = 0.7f, Curious = 0.8f, Sleepy = 0.4f, Hungry = 0.5f, Social = 0.4f,
                Likes = new() { { "Wand", 2.5f }, { "Box", 1.5f } }, Gift = new Vector2Int(12, 18), MinLevel = 4, VoicePitch = 0.85f, Scale = 1f },
            new CatDef { Id = "Juniper", Body = "Curl", Breed = "Brown curl", Personality = "Bookish dreamer", Rarity = Rarity.Uncommon, Playful = 0.4f, Curious = 0.8f, Sleepy = 0.7f, Hungry = 0.4f, Social = 0.5f,
                Likes = new() { { "Box", 2.0f }, { "Blanket", 2.0f } }, Gift = new Vector2Int(12, 18), MinLevel = 5, VoicePitch = 1.05f, Scale = 1f },
            new CatDef { Id = "Mittens", Body = "Bobtail", Breed = "Black with socks", Personality = "Cheeky thief", Rarity = Rarity.Uncommon, Playful = 0.8f, Curious = 0.7f, Sleepy = 0.4f, Hungry = 0.8f, Social = 0.6f,
                Likes = new() { { "Yarn", 2.5f }, { "Bowl", 2.0f } }, Gift = new Vector2Int(12, 18), MinLevel = 5, VoicePitch = 1.1f, Scale = 1f },
            new CatDef { Id = "Caramel", Body = "Round", Breed = "Cinnamon roundface", Personality = "Snack negotiator", Rarity = Rarity.Uncommon, Playful = 0.4f, Curious = 0.5f, Sleepy = 0.6f, Hungry = 0.9f, Social = 0.7f,
                Likes = new() { { "Bowl", 3.0f }, { "Cushion", 1.0f } }, Gift = new Vector2Int(12, 18), MinLevel = 5, VoicePitch = 0.95f, Scale = 1f },
            new CatDef { Id = "Koko", Body = "Kitten", Breed = "Calico kitten", Personality = "Chaos sprinkle", Rarity = Rarity.Uncommon, Playful = 1.0f, Curious = 0.9f, Sleepy = 0.3f, Hungry = 0.6f, Social = 0.8f,
                Likes = new() { { "Yarn", 3.0f }, { "Box", 2.0f } }, Gift = new Vector2Int(12, 18), MinLevel = 6, VoicePitch = 1.45f, Scale = 0.74f },
            new CatDef { Id = "Willow", Body = "Plume", Breed = "Lilac plume-tail", Personality = "Graceful drifter", Rarity = Rarity.Uncommon, Playful = 0.5f, Curious = 0.6f, Sleepy = 0.6f, Hungry = 0.4f, Social = 0.6f,
                Likes = new() { { "Blanket", 2.5f }, { "Wand", 1.5f } }, Gift = new Vector2Int(12, 18), MinLevel = 6, VoicePitch = 1.15f, Scale = 1f },
            new CatDef { Id = "Midnight", Body = "Short", Breed = "Black cat", Personality = "Mysterious", Rarity = Rarity.Rare, Playful = 0.7f, Curious = 0.8f, Sleepy = 0.4f, Hungry = 0.3f, Social = 0.2f,
                Likes = new() { { "Wand", 1.0f }, { "Blanket", 1.0f }, { "Cushion", 0.6f }, { "Bowl", 0.4f } }, Gift = new Vector2Int(30, 45), MinLevel = 3, MinItemsPlaced = 3, VoicePitch = 0.8f, Scale = 1f },
            new CatDef { Id = "Luna", Body = "Slim", Breed = "Odd-eyed white", Personality = "Moon gazer", Rarity = Rarity.Rare, Playful = 0.4f, Curious = 0.7f, Sleepy = 0.6f, Hungry = 0.3f, Social = 0.3f,
                Likes = new() { { "Cushion", 2.0f }, { "Blanket", 2.0f } }, Gift = new Vector2Int(30, 45), MinLevel = 5, MinItemsPlaced = 3, VoicePitch = 1.25f, Scale = 1f },
            new CatDef { Id = "Ember", Body = "Lynx", Breed = "Flame lynx", Personality = "Bold adventurer", Rarity = Rarity.Rare, Playful = 0.9f, Curious = 0.8f, Sleepy = 0.3f, Hungry = 0.5f, Social = 0.5f,
                Likes = new() { { "Wand", 3.0f }, { "Yarn", 1.5f } }, Gift = new Vector2Int(30, 45), MinLevel = 6, MinItemsPlaced = 3, VoicePitch = 0.9f, Scale = 1f },
            new CatDef { Id = "Sable", Body = "Fluffy", Breed = "Smoky tortie", Personality = "Velvet diva", Rarity = Rarity.Rare, Playful = 0.4f, Curious = 0.5f, Sleepy = 0.7f, Hungry = 0.5f, Social = 0.4f,
                Likes = new() { { "Cushion", 2.5f }, { "Blanket", 2.0f } }, Gift = new Vector2Int(30, 45), MinLevel = 6, MinItemsPlaced = 3, VoicePitch = 1.05f, Scale = 1f },
            new CatDef { Id = "Pip", Body = "Kitten", Breed = "Tuxedo kitten", Personality = "Pocket-sized hero", Rarity = Rarity.Rare, Playful = 1.0f, Curious = 1.0f, Sleepy = 0.3f, Hungry = 0.6f, Social = 0.9f,
                Likes = new() { { "Yarn", 2.5f }, { "Wand", 2.5f } }, Gift = new Vector2Int(30, 45), MinLevel = 7, MinItemsPlaced = 3, VoicePitch = 1.5f, Scale = 0.74f },
            new CatDef { Id = "Marble", Body = "Chonky", Breed = "Marbled chonk", Personality = "Royal loafer", Rarity = Rarity.Rare, Playful = 0.3f, Curious = 0.5f, Sleepy = 0.9f, Hungry = 0.8f, Social = 0.5f,
                Likes = new() { { "Blanket", 3.0f }, { "Bowl", 2.0f } }, Gift = new Vector2Int(30, 45), MinLevel = 7, MinItemsPlaced = 3, VoicePitch = 0.8f, Scale = 1.04f },
            new CatDef { Id = "Saffron", Body = "Rex", Breed = "Cream & white rex", Personality = "Sun chaser", Rarity = Rarity.Rare, Playful = 0.7f, Curious = 0.7f, Sleepy = 0.5f, Hungry = 0.5f, Social = 0.7f,
                Likes = new() { { "Blanket", 2.0f }, { "Wand", 2.0f } }, Gift = new Vector2Int(30, 45), MinLevel = 8, MinItemsPlaced = 3, VoicePitch = 1.1f, Scale = 1f },
            new CatDef { Id = "Lantern", Body = "Fluffy", Breed = "Lantern cat", Personality = "Keeper of the festival lights", Rarity = Rarity.Legendary, Playful = 0.6f, Curious = 0.7f, Sleepy = 0.6f, Hungry = 0.5f, Social = 0.8f,
                Likes = new() { { "Blanket", 2.0f }, { "Cushion", 2.0f }, { "Wand", 2.0f } }, Gift = new Vector2Int(60, 90), MinLevel = 9, MinItemsPlaced = 5, VoicePitch = 0.95f, Scale = 1f },
            new CatDef { Id = "Comet", Body = "Slim", Breed = "Starry night cat", Personality = "Falls from the night sky", Rarity = Rarity.Legendary, Playful = 0.8f, Curious = 0.9f, Sleepy = 0.4f, Hungry = 0.3f, Social = 0.4f,
                Likes = new() { { "Wand", 3.0f }, { "Box", 2.0f } }, Gift = new Vector2Int(60, 90), MinLevel = 10, MinItemsPlaced = 5, VoicePitch = 1.2f, Scale = 1f },
            new CatDef { Id = "Blossom", Body = "Plume", Breed = "Blossom cat", Personality = "Arrives with the spring petals", Rarity = Rarity.Legendary, Playful = 0.5f, Curious = 0.6f, Sleepy = 0.6f, Hungry = 0.5f, Social = 0.9f,
                Likes = new() { { "Cushion", 2.0f }, { "Blanket", 2.0f }, { "Bowl", 1.0f } }, Gift = new Vector2Int(60, 90), MinLevel = 10, MinItemsPlaced = 5, VoicePitch = 1.3f, Scale = 1f },
        };

        public static ItemDef Item(string id) => Items.Find(i => i.Id == id);
        public static CatDef Cat(string id) => Cats.Find(c => c.Id == id);

        public static readonly string[] Poses = { "Sit", "Stroll", "Eat", "Play", "Sleep", "Pounce" };
        public static string PoseLabel(string p) => p switch { "Sit" => "Sitting pretty", "Stroll" => "Garden stroll", "Eat" => "Snack time",
            "Play" => "Playtime", "Sleep" => "Catnap", "Pounce" => "Mid-pounce!", _ => p };
        public const int BondPerPet = 2, BondPerPlay = 4, BondPerPhoto = 3, BondPerTreat = 3;
        public const int PetBondCap = 10, PlayBondCap = 12, ShotsPerVisit = 3, MaxBond = 100;
        public const int StartFish = 30;
        public const int MaxSpots = 6;
        public static int SpotsForLevel(int level) => Mathf.Clamp(4 + (level - 1) / 2, 4, MaxSpots);   // 4 → 6 mats
        public static int XpForLevel(int level) => 60 + 40 * (level - 1);
        public const int XpNewCat = 50, XpRepeat = 10, XpSparkle = 30;
        public const float SparkleChance = 0.125f;
        public const float OfflineVisitMinutes = 5f, OfflineCapHours = 8f;
    }
}
