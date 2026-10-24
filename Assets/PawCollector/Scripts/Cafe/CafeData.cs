using System.Collections.Generic;
using UnityEngine;

namespace PawCollector.Cafe
{
    public enum FurnCat { Seating, CatFurniture, Decor }
    public enum SpotKind { Floor, Bed, Cushion, High, Window }

    public sealed class MenuItem
    {
        public string Id, Label, Prop; public int Price, Unlock; public float Prep;
        public MenuItem(string id, string label, string prop, int price, float prep, int unlock) { Id = id; Label = label; Prop = prop; Price = price; Prep = prep; Unlock = unlock; }
    }

    /// <summary>A cat perch on a piece of furniture (local position, local yaw).</summary>
    public struct CatPerch { public Vector3 Pos; public float Yaw; public SpotKind Kind; public CatPerch(Vector3 p, float yaw, SpotKind k) { Pos = p; Yaw = yaw; Kind = k; } }

    public sealed class FurnDef
    {
        public string Id, Label, Model; public FurnCat Cat; public int Cost, Unlock, Cozy, Seats, Beds;
        public float Radius;                 // footprint for placement / obstacle
        public bool WallMounted, Carve = true, Table;
        public CatPerch[] Perches = new CatPerch[0];
    }

    public sealed class RoomDef
    {
        public string Id, Label, Model; public int Unlock, Cost;
        public RoomDef(string id, string label, string model, int unlock, int cost) { Id = id; Label = label; Model = model; Unlock = unlock; Cost = cost; }
    }

    public static class CafeData
    {
        public const float S = 2.1f;   // Blender -> Unity scale

        public static readonly List<MenuItem> Menu = new()
        {
            new MenuItem("tea",     "Jasmine tea",       "Prop_Cup",     4, 2.0f, 1),
            new MenuItem("cookies", "Paw cookies",       "Prop_Cookies", 6, 2.5f, 1),
            new MenuItem("latte",   "Kitty latte",       "Prop_Cup",     9, 3.0f, 2),
            new MenuItem("cake",    "Strawberry cake",   "Prop_Cake",   14, 3.5f, 4),
            new MenuItem("matcha",  "Matcha float",      "Prop_Matcha", 18, 3.5f, 6),
        };

        public static readonly List<RoomDef> Rooms = new()
        {
            new RoomDef("counter", "Corner Counter", "Room_Counter", 1, 0),
            new RoomDef("nook",    "Window Nook",    "Room_Nook",    3, 400),
            new RoomDef("reading", "Reading Room",   "Room_Reading", 6, 1200),
            new RoomDef("terrace", "Terrace",        "Room_Terrace", 10, 2500),
            new RoomDef("lounge",  "Cat Lounge",     "Room_Lounge",  15, 5000),
        };
        public static string RoomBlurb(string id) => id switch
        {
            "nook" => "A sunny bay window with a seat for cats — and stray cats start visiting the window ledge.",
            "reading" => "Armchairs by a crackling fireplace. Quiet customers stay longer and tip well.",
            "terrace" => "Parasol tables out in the sunshine — lots of extra seats.",
            "lounge" => "Cat trees, tunnels and beanbags: a playground that makes every cat happier.",
            _ => "More space for tables and cats."
        };

        public static readonly List<FurnDef> Furniture = new()
        {
            new FurnDef { Id = "TableRound",  Label = "Round table",   Model = "Furn_TableRound",  Cat = FurnCat.Seating, Cost = 60,  Unlock = 1, Cozy = 1, Radius = 0.75f, Table = true },
            new FurnDef { Id = "TableSquare", Label = "Table for two", Model = "Furn_TableSquare", Cat = FurnCat.Seating, Cost = 50,  Unlock = 1, Cozy = 1, Radius = 0.7f,  Table = true },
            new FurnDef { Id = "Chair",       Label = "Pink chair",    Model = "Furn_Chair",       Cat = FurnCat.Seating, Cost = 25,  Unlock = 1, Seats = 1, Radius = 0.38f, Carve = false },
            new FurnDef { Id = "Armchair",    Label = "Comfy armchair",Model = "Furn_Armchair",    Cat = FurnCat.Seating, Cost = 140, Unlock = 3, Cozy = 3, Radius = 0.6f,
                          Perches = new[] { new CatPerch(new Vector3(0, 0.64f, -0.05f), 180, SpotKind.Cushion) } },
            new FurnDef { Id = "CatBed",      Label = "Cat bed",       Model = "Furn_CatBed",      Cat = FurnCat.CatFurniture, Cost = 80, Unlock = 1, Beds = 1, Cozy = 1, Radius = 0.5f, Carve = false,
                          Perches = new[] { new CatPerch(new Vector3(0, 0.07f, 0), 180, SpotKind.Bed) } },
            new FurnDef { Id = "Cushion",     Label = "Floor cushion", Model = "Furn_Cushion",     Cat = FurnCat.CatFurniture, Cost = 35, Unlock = 1, Cozy = 1, Radius = 0.5f, Carve = false,
                          Perches = new[] { new CatPerch(new Vector3(0, 0.14f, 0), 180, SpotKind.Cushion) } },
            new FurnDef { Id = "CatTree",     Label = "Cat tree",      Model = "Furn_CatTree",     Cat = FurnCat.CatFurniture, Cost = 180, Unlock = 2, Cozy = 2, Beds = 1, Radius = 0.7f,
                          Perches = new[] { new CatPerch(new Vector3(-0.25f, 1.9f, 0f), 180, SpotKind.High), new CatPerch(new Vector3(0.32f, 1.15f, 0.1f), 180, SpotKind.High) } },
            new FurnDef { Id = "WindowSeat",  Label = "Window seat",   Model = "Furn_WindowSeat",  Cat = FurnCat.CatFurniture, Cost = 160, Unlock = 3, Cozy = 3, Radius = 1.6f,
                          Perches = new[] { new CatPerch(new Vector3(-0.8f, 0.78f, 0f), 180, SpotKind.Window), new CatPerch(new Vector3(0.8f, 0.78f, 0f), 180, SpotKind.Window) } },
            new FurnDef { Id = "FoodBowl",    Label = "Food bowl",     Model = "Furn_FoodBowl",    Cat = FurnCat.CatFurniture, Cost = 20, Unlock = 1, Radius = 0.3f, Carve = false },
            new FurnDef { Id = "Plant",       Label = "Potted plant",  Model = "Furn_Plant",       Cat = FurnCat.Decor, Cost = 30,  Unlock = 1, Cozy = 2, Radius = 0.35f },
            new FurnDef { Id = "Lamp",        Label = "Floor lamp",    Model = "Furn_Lamp",        Cat = FurnCat.Decor, Cost = 45,  Unlock = 1, Cozy = 2, Radius = 0.3f },
            new FurnDef { Id = "Rug",         Label = "Round rug",     Model = "Furn_Rug",         Cat = FurnCat.Decor, Cost = 40,  Unlock = 1, Cozy = 2, Radius = 1.4f, Carve = false },
            new FurnDef { Id = "Bookshelf",   Label = "Bookshelf",     Model = "Furn_Bookshelf",   Cat = FurnCat.Decor, Cost = 120, Unlock = 4, Cozy = 4, Radius = 0.85f },
            new FurnDef { Id = "Shelf",       Label = "Wall shelf",    Model = "Furn_Shelf",       Cat = FurnCat.Decor, Cost = 40,  Unlock = 2, Cozy = 2, Radius = 0.3f, WallMounted = true, Carve = false },
            new FurnDef { Id = "ParasolTable",Label = "Parasol table", Model = "Furn_ParasolTable",Cat = FurnCat.Seating, Cost = 90, Unlock = 10, Cozy = 2, Radius = 0.7f, Table = true },
            new FurnDef { Id = "CatTunnel",   Label = "Cat tunnel",    Model = "Furn_CatTunnel",   Cat = FurnCat.CatFurniture, Cost = 110, Unlock = 5, Cozy = 2, Radius = 0.55f, Carve = true,
                          Perches = new[] { new CatPerch(new Vector3(0, 0.05f, 0.95f), 0, SpotKind.Floor) } },
            new FurnDef { Id = "Beanbag",     Label = "Beanbag",       Model = "Furn_Beanbag",     Cat = FurnCat.CatFurniture, Cost = 70, Unlock = 4, Cozy = 2, Radius = 0.6f, Carve = false,
                          Perches = new[] { new CatPerch(new Vector3(0, 0.6f, 0), 180, SpotKind.Cushion) } },
            new FurnDef { Id = "ToyBasket",   Label = "Toy basket",    Model = "Furn_ToyBasket",   Cat = FurnCat.CatFurniture, Cost = 45, Unlock = 2, Cozy = 1, Radius = 0.35f },
            new FurnDef { Id = "Fern",        Label = "Big fern",      Model = "Furn_Fern",        Cat = FurnCat.Decor, Cost = 60, Unlock = 3, Cozy = 3, Radius = 0.45f },
            new FurnDef { Id = "PhotoFrame",  Label = "Photo frame",   Model = "Furn_PhotoFrame",  Cat = FurnCat.Decor, Cost = 30,  Unlock = 1, Cozy = 1, Radius = 0.2f, WallMounted = true, Carve = false },
        };

        public sealed class UpgradeDef { public string Id, Label, Text; public int MaxLevel, BaseCost; }
        public static readonly List<UpgradeDef> Upgrades = new()
        {
            new UpgradeDef { Id = "speed",   Label = "Barista training", Text = "Orders are made 15% faster",       MaxLevel = 5, BaseCost = 150 },
            new UpgradeDef { Id = "menu",    Label = "Fancier menu",     Text = "Every item sells for 10% more",    MaxLevel = 5, BaseCost = 250 },
            new UpgradeDef { Id = "tips",    Label = "Tip jar charm",    Text = "Customers tip 15% more",           MaxLevel = 5, BaseCost = 200 },
            new UpgradeDef { Id = "offline", Label = "Night shift",      Text = "+2 hours of earnings while away",  MaxLevel = 4, BaseCost = 300 },
            new UpgradeDef { Id = "strays",  Label = "Window treats",    Text = "Strays visit 20% more often",      MaxLevel = 3, BaseCost = 400 },
        };
        public static int UpgradeCost(UpgradeDef u, int level) => Mathf.RoundToInt(u.BaseCost * Mathf.Pow(2.2f, level));

        public static FurnDef Furn(string id) => Furniture.Find(f => f.Id == id);
        public static MenuItem MenuItem(string id) => Menu.Find(m => m.Id == id);
        public static RoomDef Room(string id) => Rooms.Find(r => r.Id == id);

        public static readonly string[] CustomerVariants = { "A", "B", "C", "D", "E", "F", "G", "H" };

        // ---- economy
        public static int XpForLevel(int lv) => 20 + 15 * (lv - 1);
        public static float ArrivalInterval(float popularity) => 20f / (1f + popularity / 50f);
        public const int StartCoins = 150, StartTickets = 1, MaxOfflineHoursBase = 4;
        public const float TipMin = 0.1f, TipMax = 0.3f, CatTableBonus = 0.1f;
        public static readonly int[] TicketOdds = { 70, 22, 7, 1 };   // Common, Uncommon, Rare, Legendary
    }
}
