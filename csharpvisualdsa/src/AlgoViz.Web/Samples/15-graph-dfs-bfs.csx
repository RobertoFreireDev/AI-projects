// Graph DFS (recursive) and BFS (distances)
var g = Viz.Graph("g");
g.AddNode(1, 0, 100);
g.AddNode(2, 100, 0);
g.AddNode(3, 100, 200);
g.AddNode(4, 200, 0);
g.AddNode(5, 200, 200);
g.AddNode(6, 300, 100);
(int, int)[] edges = [(1, 2), (1, 3), (2, 3), (2, 4), (3, 5), (4, 6), (5, 6)];
foreach (var (a, b) in edges) g.AddEdge(a, b);

var seen = new HashSet<int>();
void Dfs(int u)
{
    seen.Add(u);
    g.Visit(u);
    g.MarkNode(u, VizColor.Active);
    foreach (var w in g.Neighbors(u))
    {
        if (seen.Contains(w)) continue;
        g.MarkEdge(u, w, VizColor.Path);
        Dfs(w);
    }
    g.MarkNode(u, VizColor.Done);
}
Viz.Step("DFS");
Dfs(1);

Viz.Step("BFS");
foreach (var v in g.Nodes) g.UnmarkNode(v);
foreach (var (a, b) in edges) g.UnmarkEdge(a, b);
var dist = Viz.Map<int, int>("dist");
var queue = new Queue<int>();
dist.Set(1, 0);
queue.Enqueue(1);
while (queue.Count > 0)
{
    int u = queue.Dequeue();
    g.Visit(u);
    foreach (var w in g.Neighbors(u))
    {
        if (dist.ContainsKey(w)) continue;
        dist.Set(w, dist.Get(u) + 1);
        g.MarkEdge(u, w, VizColor.Path);
        queue.Enqueue(w);
    }
    g.MarkNode(u, VizColor.Done);
}
