// Selection sort
var a = Viz.Array("a", 29, 10, 14, 37, 13, 5, 42);
for (int i = 0; i < a.Length - 1; i++)
{
    int min = i;
    a.Pointer("min", min);
    for (int j = i + 1; j < a.Length; j++)
    {
        if (a.Compare(j, min) < 0)
        {
            min = j;
            a.Pointer("min", min);
        }
    }
    if (min != i) a.Swap(i, min);
    a.Mark(i, VizColor.Done);
}
a.Mark(a.Length - 1, VizColor.Done);
a.RemovePointer("min");
