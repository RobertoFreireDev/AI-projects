// Balanced brackets with a stack
var input = Viz.Array("input", "{[(a+b)*c]-(d)}".ToCharArray());
var stack = Viz.Stack<char>("stack");
var pairs = new Dictionary<char, char> { [')'] = '(', [']'] = '[', ['}'] = '{' };
bool ok = true;
for (int i = 0; i < input.Length && ok; i++)
{
    input.Pointer("i", i);
    char c = input.Get(i);
    if ("([{".Contains(c))
    {
        stack.Push(c);
    }
    else if (pairs.TryGetValue(c, out var open))
    {
        if (stack.IsEmpty || stack.Pop() != open)
        {
            input.Mark(i, VizColor.Warn);
            ok = false;
        }
        else input.Mark(i, VizColor.Done);
    }
}
ok = ok && stack.IsEmpty;
Viz.Log(ok ? "Balanced" : "Not balanced");
