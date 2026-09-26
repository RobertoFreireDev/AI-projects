namespace SqliteViz.Core.Plan;

/// <summary>How good a plan step is for performance, used for coloring.</summary>
public enum PlanTag { Neutral, Best, Good, Warn }

public sealed record PlanNode(int Id, string Detail, PlanTag Tag, IReadOnlyList<PlanNode> Children);

/// <summary>Turns EXPLAIN QUERY PLAN rows (id, parent, detail) into a tree.</summary>
public static class QueryPlanParser
{
    public static PlanNode Parse(IEnumerable<(int Id, int Parent, string Detail)> rows)
    {
        var children = new Dictionary<int, List<(int Id, string Detail)>>();
        foreach (var (id, parent, detail) in rows)
        {
            if (!children.TryGetValue(parent, out var list)) children[parent] = list = [];
            list.Add((id, detail));
        }
        var visited = new HashSet<int>();

        PlanNode Build(int id, string detail)
        {
            var kids = new List<PlanNode>();
            if (visited.Add(id) && children.TryGetValue(id, out var list))
                foreach (var (childId, childDetail) in list) kids.Add(Build(childId, childDetail));
            return new PlanNode(id, detail, Classify(detail), kids);
        }

        return Build(0, "QUERY PLAN");
    }

    public static PlanTag Classify(string detail)
    {
        if (detail.Contains("AUTOMATIC", StringComparison.Ordinal)) return PlanTag.Warn;
        if (detail.Contains("USE TEMP B-TREE", StringComparison.Ordinal)) return PlanTag.Warn;
        if (detail.Contains("COVERING INDEX", StringComparison.Ordinal)) return PlanTag.Best;
        if (detail.StartsWith("SEARCH", StringComparison.Ordinal)) return PlanTag.Good;
        if (detail.StartsWith("SCAN", StringComparison.Ordinal)) return PlanTag.Warn;
        return PlanTag.Neutral;
    }
}
