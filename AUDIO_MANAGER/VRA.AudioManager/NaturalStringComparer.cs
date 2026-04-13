using System.Text.RegularExpressions;

namespace VRA.AudioManager;

/// <summary>
/// Tri naturel : "MADI (9-16)" avant "MADI (17-24)" avant "MADI (105-112)".
/// </summary>
public sealed partial class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberPattern();

    public int Compare(string? x, string? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var partsX = Tokenize(x);
        var partsY = Tokenize(y);

        for (int i = 0; i < Math.Min(partsX.Count, partsY.Count); i++)
        {
            int cmp = CompareToken(partsX[i], partsY[i]);
            if (cmp != 0) return cmp;
        }

        return partsX.Count.CompareTo(partsY.Count);
    }

    private static int CompareToken(string a, string b)
    {
        bool aIsNum = int.TryParse(a, out int aNum);
        bool bIsNum = int.TryParse(b, out int bNum);

        if (aIsNum && bIsNum) return aNum.CompareTo(bNum);
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> Tokenize(string s) =>
        NumberPattern().Split(s)
            .Zip(NumberPattern().Matches(s).Select(m => m.Value).Append(""))
            .SelectMany(pair => new[] { pair.First, pair.Second })
            .Where(t => t.Length > 0)
            .ToList();
}
