namespace Notes.Services;

public class DiffService : IDiffService
{
    public DiffResult ComputeDiff(string left, string right)
    {
        var leftLines = SplitLines(left);
        var rightLines = SplitLines(right);

        var result = new DiffResult();
        var diffLines = ComputeLcsDiff(leftLines, rightLines);

        result.Lines = diffLines;
        result.LinesAdded = diffLines.Count(l => l.Type == DiffLineType.Added);
        result.LinesRemoved = diffLines.Count(l => l.Type == DiffLineType.Removed);
        result.LinesUnchanged = diffLines.Count(l => l.Type == DiffLineType.Unchanged);

        return result;
    }

    private static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<string>();

        return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
    }

    private List<DiffLine> ComputeLcsDiff(string[] left, string[] right)
    {
        int m = left.Length;
        int n = right.Length;

        // Build LCS table
        int[,] lcs = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (left[i - 1] == right[j - 1])
                    lcs[i, j] = lcs[i - 1, j - 1] + 1;
                else
                    lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
            }
        }

        // Backtrack to build diff
        var diffLines = new List<DiffLine>();
        int li = m, ri = n;

        while (li > 0 || ri > 0)
        {
            if (li > 0 && ri > 0 && left[li - 1] == right[ri - 1])
            {
                // Unchanged line
                diffLines.Add(new DiffLine
                {
                    Type = DiffLineType.Unchanged,
                    LeftContent = left[li - 1],
                    RightContent = right[ri - 1],
                    LeftLineNumber = li,
                    RightLineNumber = ri
                });
                li--;
                ri--;
            }
            else if (ri > 0 && (li == 0 || lcs[li, ri - 1] >= lcs[li - 1, ri]))
            {
                // Added line (exists in right but not in left)
                diffLines.Add(new DiffLine
                {
                    Type = DiffLineType.Added,
                    RightContent = right[ri - 1],
                    RightLineNumber = ri
                });
                ri--;
            }
            else if (li > 0)
            {
                // Removed line (exists in left but not in right)
                diffLines.Add(new DiffLine
                {
                    Type = DiffLineType.Removed,
                    LeftContent = left[li - 1],
                    LeftLineNumber = li
                });
                li--;
            }
        }

        // Reverse since we built it backwards
        diffLines.Reverse();

        return diffLines;
    }
}
