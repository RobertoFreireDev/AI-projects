// Binary search
var a = Viz.Array("sorted", 2, 5, 8, 12, 16, 23, 38, 56, 72, 91);
int target = 23;
Viz.Var("target", target);
int lo = 0, hi = a.Length - 1, found = -1;
while (lo <= hi)
{
    a.Pointer("lo", lo);
    a.Pointer("hi", hi);
    int mid = lo + (hi - lo) / 2;
    a.Pointer("mid", mid);
    int value = a.Get(mid);
    if (value == target) { found = mid; break; }
    if (value < target)
    {
        for (int k = lo; k <= mid; k++) a.Mark(k, VizColor.Muted);
        lo = mid + 1;
    }
    else
    {
        for (int k = mid; k <= hi; k++) a.Mark(k, VizColor.Muted);
        hi = mid - 1;
    }
}
if (found >= 0) a.Mark(found, VizColor.Done);
Viz.Log(found >= 0 ? $"Found {target} at index {found}" : $"{target} not found");
