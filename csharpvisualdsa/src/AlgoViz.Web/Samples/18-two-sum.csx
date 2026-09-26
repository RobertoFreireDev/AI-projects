// Two sum with a hash map
var nums = Viz.Array("nums", 2, 7, 11, 15, 1, 8);
int target = 16;
Viz.Var("target", target);
var seen = Viz.Map<int, int>("value → index");
for (int i = 0; i < nums.Length; i++)
{
    nums.Pointer("i", i);
    int x = nums.Get(i);
    int need = target - x;
    Viz.Var("need", need);
    if (seen.ContainsKey(need))
    {
        int j = seen.Get(need);
        nums.Mark(j, VizColor.Done);
        nums.Mark(i, VizColor.Done);
        Viz.Log($"nums[{j}] + nums[{i}] = {target}");
        break;
    }
    seen.Set(x, i);
}
