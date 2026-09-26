// Bubble sort
var arr = Viz.Array("arr", 5, 1, 4, 2, 8, 3);
for (int i = 0; i < arr.Length - 1; i++)
{
    for (int j = 0; j < arr.Length - 1 - i; j++)
    {
        if (arr.Compare(j, j + 1) > 0)
            arr.Swap(j, j + 1);
    }
    arr.Mark(arr.Length - 1 - i, VizColor.Done);
}
arr.Mark(0, VizColor.Done);
