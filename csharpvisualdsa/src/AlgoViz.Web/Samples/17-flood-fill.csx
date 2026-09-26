// Flood fill on a grid (iterative, 4 directions)
var m = Viz.CharGrid("map",
    "..##....",
    ".#..#...",
    ".#...#..",
    "..#.#...",
    "...#....");
var stack = new Stack<(int R, int C)>();
stack.Push((2, 3));
while (stack.Count > 0)
{
    var (r, c) = stack.Pop();
    if (!m.InBounds(r, c) || m.Get(r, c) != '.') continue;
    m.Set(r, c, '~');
    m.Mark(r, c, VizColor.Active);
    stack.Push((r + 1, c));
    stack.Push((r - 1, c));
    stack.Push((r, c + 1));
    stack.Push((r, c - 1));
}
