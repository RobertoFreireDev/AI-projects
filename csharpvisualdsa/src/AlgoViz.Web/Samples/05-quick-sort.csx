// Quick sort (Lomuto partition)
var a = Viz.Array("a", 10, 80, 30, 90, 40, 50, 70);

int Partition(int lo, int hi)
{
    a.Mark(hi, VizColor.Warn); // the pivot
    int i = lo - 1;
    for (int j = lo; j < hi; j++)
    {
        a.Pointer("j", j);
        if (a.Compare(j, hi) < 0)
        {
            i++;
            a.Swap(i, j);
        }
    }
    a.Unmark(hi);
    a.Swap(i + 1, hi);
    a.Mark(i + 1, VizColor.Done);
    return i + 1;
}

void QuickSort(int lo, int hi)
{
    if (lo > hi) return;
    if (lo == hi) { a.Mark(lo, VizColor.Done); return; }
    int p = Partition(lo, hi);
    QuickSort(lo, p - 1);
    QuickSort(p + 1, hi);
}

QuickSort(0, a.Length - 1);
a.RemovePointer("j");
