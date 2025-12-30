using System.Collections.Concurrent;

namespace Notes.Services;

public class CommandService : ICommandService
{
    private readonly ConcurrentDictionary<string, Command> _commands = new();
    private readonly List<string> _recentCommandIds = new();
    private const int MaxRecentCommands = 10;

    public event Action? CommandsChanged;
    public event Action? OpenPaletteRequested;

    public void RequestOpenPalette()
    {
        OpenPaletteRequested?.Invoke();
    }

    public void RegisterCommand(Command command)
    {
        _commands[command.Id] = command;
        CommandsChanged?.Invoke();
    }

    public void UnregisterCommand(string commandId)
    {
        _commands.TryRemove(commandId, out _);
        CommandsChanged?.Invoke();
    }

    public void ClearCategory(string category)
    {
        var toRemove = _commands.Values
            .Where(c => c.Category == category)
            .Select(c => c.Id)
            .ToList();

        foreach (var id in toRemove)
        {
            _commands.TryRemove(id, out _);
        }

        if (toRemove.Count > 0)
        {
            CommandsChanged?.Invoke();
        }
    }

    public List<Command> GetAll()
    {
        return _commands.Values
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Label)
            .ToList();
    }

    public List<Command> GetByCategory(string category)
    {
        return _commands.Values
            .Where(c => c.Category == category)
            .OrderBy(c => c.Label)
            .ToList();
    }

    public List<Command> Search(string query, int limit = 15)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            // Return recent commands first, then all commands by category
            var recent = GetRecent(5);
            var recentIds = recent.Select(r => r.Id).ToHashSet();

            var others = _commands.Values
                .Where(c => !recentIds.Contains(c.Id))
                .OrderBy(c => GetCategoryOrder(c.Category))
                .ThenBy(c => c.Label)
                .Take(limit - recent.Count);

            return recent.Concat(others).ToList();
        }

        var queryLower = query.ToLowerInvariant();
        var results = new List<(Command Command, int Score)>();

        foreach (var command in _commands.Values)
        {
            var score = CalculateMatchScore(command, queryLower);
            if (score > 0)
            {
                results.Add((command, score));
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Command.Label)
            .Take(limit)
            .Select(r => r.Command)
            .ToList();
    }

    private static int GetCategoryOrder(string category)
    {
        return category switch
        {
            "Actions" => 0,
            "Navigate" => 1,
            "Scripts" => 2,
            "Notes" => 3,
            _ => 99
        };
    }

    private static int CalculateMatchScore(Command command, string query)
    {
        var score = 0;
        var labelLower = command.Label.ToLowerInvariant();
        var categoryLower = command.Category.ToLowerInvariant();
        var keywordsLower = command.Keywords?.ToLowerInvariant() ?? "";
        var descLower = command.Description?.ToLowerInvariant() ?? "";

        // Exact match in label - highest priority
        if (labelLower == query)
            return 1000;

        // Label starts with query
        if (labelLower.StartsWith(query))
            score += 100;

        // Label contains query
        if (labelLower.Contains(query))
            score += 50;

        // Category matches
        if (categoryLower.Contains(query))
            score += 20;

        // Keywords match
        if (keywordsLower.Contains(query))
            score += 30;

        // Description contains query
        if (descLower.Contains(query))
            score += 10;

        // Fuzzy match on label (simple character matching)
        score += FuzzyScore(labelLower, query);

        return score;
    }

    private static int FuzzyScore(string text, string query)
    {
        // Simple fuzzy matching: check if all query characters appear in order
        var textIndex = 0;
        var matchedChars = 0;
        var consecutiveBonus = 0;
        var lastMatchIndex = -2;

        foreach (var queryChar in query)
        {
            var found = false;
            while (textIndex < text.Length)
            {
                if (text[textIndex] == queryChar)
                {
                    matchedChars++;
                    if (textIndex == lastMatchIndex + 1)
                    {
                        consecutiveBonus += 2; // Bonus for consecutive matches
                    }
                    lastMatchIndex = textIndex;
                    textIndex++;
                    found = true;
                    break;
                }
                textIndex++;
            }

            if (!found)
                return 0; // Query character not found in remaining text
        }

        return matchedChars + consecutiveBonus;
    }

    public async Task ExecuteAsync(string commandId)
    {
        if (_commands.TryGetValue(commandId, out var command) && command.Action != null)
        {
            // Add to recent
            lock (_recentCommandIds)
            {
                _recentCommandIds.Remove(commandId);
                _recentCommandIds.Insert(0, commandId);
                while (_recentCommandIds.Count > MaxRecentCommands)
                {
                    _recentCommandIds.RemoveAt(_recentCommandIds.Count - 1);
                }
            }

            await command.Action.Invoke();
        }
    }

    public List<Command> GetRecent(int limit = 5)
    {
        var recent = new List<Command>();
        lock (_recentCommandIds)
        {
            foreach (var id in _recentCommandIds.Take(limit))
            {
                if (_commands.TryGetValue(id, out var command))
                {
                    recent.Add(command);
                }
            }
        }
        return recent;
    }
}
