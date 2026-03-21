using Pasty.ViewModels;

namespace Pasty.Services;

public class FuzzySearchService
{
    public List<ClipboardItemViewModel> Filter(
        IReadOnlyList<ClipboardItemViewModel> allItems,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return allItems.ToList();

        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return allItems.ToList();

        return allItems
            .Where(item => MatchesAllTokens(item.SearchText, tokens))
            .ToList();
    }

    private static bool MatchesAllTokens(string text, string[] tokens)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var token in tokens)
        {
            if (!text.Contains(token, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }
}
