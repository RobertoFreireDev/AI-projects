// Dijkstra's shortest paths
var g = Viz.Graph("roads");
g.AddNode(0, 0, 100);
g.AddNode(1, 120, 0);
g.AddNode(2, 120, 200);
g.AddNode(3, 260, 0);
g.AddNode(4, 260, 200);
g.AddNode(5, 380, 100);
g.AddEdge(0, 1, 4);
g.AddEdge(0, 2, 2);
g.AddEdge(1, 2, 1);
g.AddEdge(1, 3, 5);
g.AddEdge(2, 4, 10);
g.AddEdge(3, 4, 3);
g.AddEdge(3, 5, 6);
g.AddEdge(4, 5, 1);

var dist = Viz.Map<int, double>("dist");
var prev = new Dictionary<int, int>();
var done = new HashSet<int>();
var pq = new PriorityQueue<int, double>();
dist.Set(0, 0);
pq.Enqueue(0, 0);
while (pq.TryDequeue(out int u, out double d))
{
    if (!done.Add(u)) continue;
    g.Visit(u);
    g.MarkNode(u, VizColor.Done);
    foreach (int w in g.Neighbors(u))
    {
        if (done.Contains(w)) continue;
        double candidate = d + g.Weight(u, w);
        if (!dist.ContainsKey(w) || candidate < dist.Get(w))
        {
            dist.Set(w, candidate);
            prev[w] = u;
            pq.Enqueue(w, candidate);
            g.MarkEdge(u, w, VizColor.Active);
        }
    }
}

Viz.Step("path to 5");
for (int v = 5; prev.ContainsKey(v); v = prev[v])
{
    g.MarkEdge(prev[v], v, VizColor.Path);
    g.MarkNode(v, VizColor.Path);
}
g.MarkNode(0, VizColor.Path);
Viz.Log($"shortest 0 → 5 = {dist.Get(5)}");
