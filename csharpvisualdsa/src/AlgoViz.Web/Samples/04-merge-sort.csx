// Merge sort
var a = Viz.Array("a", 38, 27, 43, 3, 9, 82, 10, 19);
var tmp = Viz.Array<int>("tmp", a.Length);

void Sort(int lo, int hi)
{
    if (hi <= lo) return;
    int mid = (lo + hi) / 2;
    Sort(lo, mid);
    Sort(mid + 1, hi);
    Merge(lo, mid, hi);
}

void Merge(int lo, int mid, int hi)
{
    Viz.Step($"merge {lo}..{hi}");
    a.Pointer("lo", lo);
    a.Pointer("hi", hi);
    int i = lo, j = mid + 1, k = lo;
    while (i <= mid && j <= hi)
        tmp.Set(k++, a.Compare(i, j) <= 0 ? a.Get(i++) : a.Get(j++));
    while (i <= mid) tmp.Set(k++, a.Get(i++));
    while (j <= hi) tmp.Set(k++, a.Get(j++));
    for (k = lo; k <= hi; k++) a.Set(k, tmp.Get(k));
}

Sort(0, a.Length - 1);
a.RemovePointer("lo");
a.RemovePointer("hi");
for (int k = 0; k < a.Length; k++) a.Mark(k, VizColor.Done);
