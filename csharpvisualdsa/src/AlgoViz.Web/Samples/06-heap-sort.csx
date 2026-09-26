// Heap sort (the array is also drawn as a tree)
var a = Viz.Array("heap", 4, 10, 3, 5, 1, 8, 7, 2);
a.ShowAsTree();

void SiftDown(int i, int size)
{
    while (true)
    {
        int largest = i, l = 2 * i + 1, r = 2 * i + 2;
        if (l < size && a.Compare(l, largest) > 0) largest = l;
        if (r < size && a.Compare(r, largest) > 0) largest = r;
        if (largest == i) return;
        a.Swap(i, largest);
        i = largest;
    }
}

Viz.Step("build heap");
for (int i = a.Length / 2 - 1; i >= 0; i--) SiftDown(i, a.Length);

Viz.Step("sort");
for (int end = a.Length - 1; end > 0; end--)
{
    a.Swap(0, end);
    a.Mark(end, VizColor.Done);
    SiftDown(0, end);
}
a.Mark(0, VizColor.Done);
