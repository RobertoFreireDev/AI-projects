// BFS with a queue
var g = Viz.Graph("g");
for (int v = 0; v < 7; v++) g.AddNode(v);
int[][] edges = [[0, 1], [0, 2], [1, 3], [1, 4], [2, 5], [4, 6], [5, 6]];
foreach (var e in edges) g.AddEdge(e[0], e[1]);

var queue = Viz.Queue<int>("queue");
var seen = Viz.Set<int>("seen");
queue.Enqueue(0);
seen.Add(0);
while (!queue.IsEmpty)
{
    int u = queue.Dequeue();
    g.Visit(u);
    g.MarkNode(u, VizColor.Done);
    foreach (int w in g.Neighbors(u))
    {
        if (seen.Contains(w)) continue;
        seen.Add(w);
        g.MarkEdge(u, w, VizColor.Path);
        g.MarkNode(w, VizColor.Active);
        queue.Enqueue(w);
    }
}
