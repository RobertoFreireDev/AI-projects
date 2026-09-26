// Insertion sort
var a = Viz.Array("a", 12, 11, 13, 5, 6, 7);
a.Mark(0, VizColor.Done);
for (int i = 1; i < a.Length; i++)
{
    a.Pointer("i", i);
    int j = i;
    while (j > 0 && a.Compare(j - 1, j) > 0)
    {
        a.Swap(j - 1, j);
        j--;
    }
    a.Mark(i, VizColor.Done);
}
a.RemovePointer("i");
