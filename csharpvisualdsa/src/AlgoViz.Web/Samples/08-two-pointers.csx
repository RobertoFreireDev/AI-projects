// Two pointers: a pair with a given sum in a sorted array
var a = Viz.Array("a", 1, 3, 4, 6, 8, 9, 11, 14);
int target = 17;
Viz.Var("target", target);
int left = 0, right = a.Length - 1;
while (left < right)
{
    a.Pointer("L", left);
    a.Pointer("R", right);
    int sum = a.Get(left) + a.Get(right);
    Viz.Var("sum", sum);
    if (sum == target)
    {
        a.Mark(left, VizColor.Done);
        a.Mark(right, VizColor.Done);
        Viz.Log($"{target} = a[{left}] + a[{right}]");
        break;
    }
    if (sum < target) left++;
    else right--;
}
