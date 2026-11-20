using System.Security.Cryptography;

namespace Api.Domain;

// Plain words for readable player links like "ember-moss-lantern-crow". Four words from 512
// give 2^36 (about 68 billion) links, so a readable link is still hard to guess.
public static class ShareLinkWords
{
    public const int WordsPerLink = 4;

    public static IReadOnlyList<string> All { get; } =
    [
        "acorn", "adder", "alder", "amber", "anchor", "anvil", "apple", "apron", "arch", "arrow", "ash",
        "aspen", "aster", "attic", "auger", "autumn", "axe", "badger", "bale", "balm", "bank", "barley",
        "barn", "barrow", "basin", "basket", "bat", "bay", "beacon", "bead", "beam", "bean", "bear", "beaver",
        "bee", "beech", "beet", "bell", "bench", "berry", "birch", "bird", "bison", "blade", "bloom", "boar",
        "boat", "bog", "bolt", "bone", "bonnet", "bough", "bow", "bowl", "branch", "brass", "bread", "breeze",
        "briar", "brick", "bridge", "brine", "brook", "broom", "brush", "bucket", "buckle", "bud", "burrow",
        "bush", "butter", "cabin", "cairn", "cake", "canal", "candle", "canoe", "canyon", "cap", "cape",
        "carrot", "cart", "cask", "castle", "cat", "cedar", "cellar", "chalk", "chapel", "cheese", "cherry",
        "chest", "chime", "chisel", "cider", "cinder", "clay", "cliff", "cloak", "clock", "cloud", "clover",
        "coal", "coast", "cob", "cobble", "cobweb", "comb", "comet", "copper", "coral", "cork", "corn",
        "cove", "cow", "crab", "cradle", "crane", "crate", "creek", "crest", "crow", "crown", "crumb", "cup",
        "curlew", "dairy", "daisy", "dale", "dawn", "deer", "dell", "den", "dew", "ditch", "dove", "down",
        "drake", "drift", "drum", "dune", "dusk", "eagle", "earth", "eel", "elder", "elk", "elm", "ember",
        "fable", "falcon", "fallow", "fawn", "feast", "fen", "fern", "ferry", "fiddle", "field", "fig",
        "finch", "fir", "fire", "firth", "fish", "flag", "flagon", "flame", "flax", "fleece", "flint",
        "flock", "flute", "foal", "fog", "ford", "forest", "forge", "fox", "frost", "furrow", "gale",
        "garden", "garnet", "gate", "gem", "glade", "glen", "glove", "goat", "goose", "gorge", "gorse",
        "grain", "grape", "grass", "gravel", "grotto", "grove", "gull", "hail", "hall", "hamlet", "hammer",
        "harbor", "hare", "harp", "harrow", "hawk", "hay", "hazel", "hearth", "heath", "hedge", "helm", "hen",
        "herb", "heron", "hill", "hive", "hollow", "holly", "honey", "hoof", "horn", "horse", "hound", "hut",
        "ice", "inn", "iris", "iron", "island", "ivy", "jar", "jay", "jewel", "jug", "juniper", "kale",
        "kelp", "kennel", "kernel", "kestrel", "kettle", "key", "kiln", "kite", "knoll", "knot", "lace",
        "ladder", "lagoon", "lake", "lamb", "lamp", "lance", "lantern", "larch", "lark", "leaf", "ledge",
        "leek", "lichen", "lily", "lime", "linden", "linen", "lion", "loaf", "lock", "lodge", "loft", "loom",
        "lynx", "mallet", "malt", "mantle", "maple", "marble", "mare", "marsh", "marten", "mast", "mead",
        "meadow", "mesa", "mill", "minnow", "mint", "mire", "mist", "mitten", "mole", "moon", "moor", "moose",
        "moss", "moth", "mound", "mouse", "mule", "nail", "needle", "nest", "nettle", "newt", "night",
        "north", "nutmeg", "oak", "oar", "oat", "ocean", "olive", "onion", "orchard", "orchid", "osprey",
        "otter", "owl", "ox", "paddle", "paddock", "pail", "pansy", "parsley", "parsnip", "pass", "pasture",
        "path", "peach", "pear", "pebble", "pepper", "perch", "petal", "pie", "pier", "pigeon", "pike",
        "pine", "pipe", "pitch", "plain", "plank", "plover", "plow", "plum", "plume", "pond", "pony",
        "poplar", "poppy", "porch", "post", "pot", "pottery", "prairie", "puffin", "quail", "quarry", "quill",
        "quilt", "quince", "rabbit", "radish", "raft", "rain", "ram", "rampart", "raven", "reed", "reef",
        "ribbon", "ridge", "rill", "river", "road", "robin", "rock", "rook", "root", "rope", "rose", "rowan",
        "rudder", "rune", "rush", "rye", "saddle", "saffron", "sage", "sail", "salmon", "salt", "sand",
        "sapling", "satchel", "scythe", "sea", "seal", "seed", "shadow", "shale", "sheaf", "shed", "sheep",
        "shell", "shield", "shingle", "ship", "shore", "shrew", "shrub", "sickle", "silver", "skiff", "sky",
        "slate", "sled", "sloe", "smith", "snail", "snow", "sorrel", "spade", "sparrow", "spear", "spindle",
        "spoon", "spring", "sprout", "spruce", "stable", "stag", "star", "steep", "stile", "stone", "stork",
        "storm", "stove", "straw", "stream", "stump", "summit", "sun", "swallow", "swan", "sword", "table",
        "tallow", "tansy", "tarn", "tea", "teal", "thatch", "thimble", "thistle", "thorn", "thrush", "thyme",
        "tide", "timber", "tin", "tinder", "toad", "torch", "tower", "trail", "tree", "trellis", "trout",
        "tulip", "turnip", "turtle", "twig", "vale", "valley", "vane", "vine", "violet", "vole", "wagon",
        "walnut", "walrus", "wand", "warren", "wasp", "water", "wave", "wax", "weasel", "weir", "well",
        "whale", "wheat", "wheel", "whistle", "willow", "wind", "window", "wing", "winter", "wolf", "wood",
        "wool", "wren", "yarn", "yarrow", "yew",
    ];

    public static string NewLink() =>
        string.Join('-', Enumerable.Range(0, WordsPerLink).Select(_ => All[RandomNumberGenerator.GetInt32(All.Count)]));
}
