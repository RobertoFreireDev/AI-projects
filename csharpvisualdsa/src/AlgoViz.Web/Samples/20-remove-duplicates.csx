// Remove duplicates with a set
var input = Viz.Array("input", 4, 2, 4, 1, 2, 3, 1, 5);
var seen = Viz.Set<int>("seen");
var output = Viz.List<int>("output");
for (int i = 0; i < input.Length; i++)
{
    input.Pointer("i", i);
    int v = input.Get(i);
    if (seen.Add(v)) output.Add(v);
    else input.Mark(i, VizColor.Muted);
}
