using System.Text.RegularExpressions;

namespace Bookshelf.Features.Genres;

/// <param name="Key">Stable id stored on books (labels can change without touching data).</param>
public sealed record Genre(string Key, string Label, string Group);

/// <summary>
/// The household's fixed genre list, and rules that turn the messy categories and subjects from
/// Google Books and Open Library ("Fiction / Mystery &amp; Detective / General", "Detective and
/// mystery stories") into up to <see cref="MaxPerBook"/> of these genres.
/// </summary>
public static class GenreCatalogue
{
    public const int MaxPerBook = 3;

    public const string Fiction = "Fiction";
    public const string NonFiction = "Non-fiction";
    public const string ForYoungerReaders = "Younger readers & more";

    public static IReadOnlyList<Genre> All { get; } =
    [
        new("literary", "Literary", Fiction),
        new("classics", "Classics", Fiction),
        new("crime", "Crime & Thriller", Fiction),
        new("fantasy", "Fantasy", Fiction),
        new("scifi", "Sci-Fi", Fiction),
        new("historical-fiction", "Historical", Fiction),
        new("romance", "Romance", Fiction),
        new("horror", "Horror", Fiction),
        new("humour", "Humour", Fiction),
        new("poetry", "Poetry & Plays", Fiction),
        new("biography", "Biography & Memoir", NonFiction),
        new("history", "History", NonFiction),
        new("science", "Science & Nature", NonFiction),
        new("travel", "Travel", NonFiction),
        new("food", "Food & Drink", NonFiction),
        new("home-garden", "Home & Garden", NonFiction),
        new("art", "Art & Design", NonFiction),
        new("self-help", "Self-help", NonFiction),
        new("religion", "Religion & Philosophy", NonFiction),
        new("reference", "Reference", NonFiction),
        new("children", "Children's", ForYoungerReaders),
        new("young-adult", "Young Adult", ForYoungerReaders),
        new("graphic", "Graphic Novels", ForYoungerReaders),
        new("other", "Other", ForYoungerReaders),
    ];

    private static readonly Dictionary<string, Genre> ByKey = All.ToDictionary(g => g.Key);

    public static Genre? Get(string key) => ByKey.GetValueOrDefault(key);

    public static string Label(string key) => Get(key)?.Label ?? key;

    /// <summary>Known keys only, no duplicates, catalogue order, at most three.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? keys)
    {
        var wanted = (keys ?? []).ToHashSet();
        return [.. All.Where(g => wanted.Contains(g.Key)).Select(g => g.Key).Take(MaxPerBook)];
    }

    // Each genre's patterns, matched as whole words/phrases against a lower-cased category.
    // Order matters only for ties: earlier rules win.
    private static readonly (string Key, Regex Pattern, bool FictionOnly)[] Rules =
    [
        ("children", Words("juvenile", "children's", "childrens", "children", "picture books?", "bedtime"), false),
        ("young-adult", Words("young adult", "teen", "teenagers?"), false),
        ("graphic", Words("comics?", "graphic novels?", "manga"), false),
        ("crime", Words("mystery", "mysteries", "detective", "detectives", "crime", "thrillers?", "suspense", "murder", "espionage", "spy stories"), false),
        ("fantasy", Words("fantasy", "fantasy fiction", "magic", "dragons", "wizards", "fairy tales"), false),
        ("scifi", Words("science fiction", "sci-fi", "dystopias?", "dystopian", "space opera", "time travel"), false),
        ("historical-fiction", Words("historical fiction", "historical"), true),
        ("romance", Words("romance", "love stories", "romantic"), false),
        ("horror", Words("horror", "ghost stories", "vampires", "supernatural"), false),
        ("humour", Words("humor", "humour", "humorous", "comic fiction", "satire"), false),
        ("poetry", Words("poetry", "poems", "drama", "plays", "verse"), false),
        ("classics", Words("classics", "classic literature"), false),
        ("literary", Words("literary", "literary fiction", "psychological fiction", "domestic fiction"), false),
        ("biography", Words("biography", "biographies", "autobiography", "autobiographies", "memoirs?", "diaries", "letters"), false),
        ("history", Words("history", "world war", "military history"), false),
        ("science", Words("science", "nature", "natural history", "mathematics", "medical", "medicine", "technology", "physics", "biology", "astronomy", "animals", "birds", "ecology"), false),
        ("travel", Words("travel", "guidebooks", "voyages and travels", "description and travel"), false),
        ("food", Words("cooking", "cookery", "cookbooks?", "food", "recipes", "baking", "wine", "drinks?"), false),
        ("home-garden", Words("gardening", "gardens", "house & home", "crafts", "hobbies", "pets", "handicraft", "diy", "interior decoration"), false),
        ("art", Words("art", "arts", "design", "photography", "architecture", "music", "painting"), false),
        ("self-help", Words("self-help", "self help", "psychology", "health & fitness", "personal growth", "success", "business & economics", "parenting"), false),
        ("religion", Words("religion", "religions", "philosophy", "spirituality", "theology", "bible", "christianity", "buddhism"), false),
        ("reference", Words("reference", "dictionaries", "dictionary", "encyclopedias", "atlases", "language arts", "study aids", "foreign language study", "grammar"), false),
    ];

    /// <summary>
    /// Suggests up to three genres. A Google category's first part ("Art" in "Art / History") counts 3,
    /// its later parts 2; each Open Library subject (many, and noisy) counts 1. Non-fiction rules don't
    /// fire on fiction categories ("Fiction / Historical" is a historical novel, not History). Ties go
    /// to the rule listed first.
    /// </summary>
    public static IReadOnlyList<string> Suggest(IEnumerable<string>? googleCategories, IEnumerable<string>? openLibrarySubjects = null)
    {
        var parts = (googleCategories ?? []).SelectMany(category =>
            {
                var segments = category.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                return segments.Select((segment, i) => (Text: segment, Whole: category, Weight: i == 0 ? 3 : 2));
            })
            .Concat((openLibrarySubjects ?? []).Take(40).Select(s => (Text: s, Whole: s, Weight: 1)));

        var scores = new Dictionary<string, int>();
        var anyFiction = false;
        foreach (var (text, whole, weight) in parts)
        {
            var isFiction = IsFiction(whole);
            anyFiction |= isFiction;
            foreach (var (key, pattern, fictionOnly) in Rules)
            {
                if ((fictionOnly && !isFiction) || (isFiction && ByKey[key].Group == NonFiction))
                {
                    continue;
                }

                if (pattern.IsMatch(text))
                {
                    scores[key] = scores.GetValueOrDefault(key) + weight;
                }
            }
        }

        if (scores.Count == 0 && anyFiction)
        {
            return ["literary"];
        }

        var ruleOrder = Rules.Select((r, i) => (r.Key, i)).ToDictionary(x => x.Key, x => x.i);
        return [.. scores.OrderByDescending(s => s.Value).ThenBy(s => ruleOrder[s.Key]).Select(s => s.Key).Take(MaxPerBook)];
    }

    private static bool IsFiction(string category)
    {
        var lower = category.ToLowerInvariant();
        return lower.Contains("fiction") && !lower.Contains("nonfiction") && !lower.Contains("non-fiction");
    }

    private static Regex Words(params string[] phrases) =>
        new($@"(?<![\w-])({string.Join('|', phrases.Select(p => p.Replace(" ", @"\s+")))})(?![\w-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
