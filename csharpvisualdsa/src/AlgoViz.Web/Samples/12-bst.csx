// Binary search tree: insert, search, delete
var bst = Viz.BinaryTree<int>("bst");

void Insert(int v)
{
    if (bst.Root is null) { bst.SetRoot(v); return; }
    var n = bst.Root;
    while (true)
    {
        n.Visit();
        if (v < n.Value) { if (n.Left is null) { n.SetLeft(v); return; } n = n.Left; }
        else             { if (n.Right is null) { n.SetRight(v); return; } n = n.Right; }
    }
}

BinaryNode<int> Search(int v)
{
    var n = bst.Root;
    while (n != null)
    {
        n.Visit();
        if (v == n.Value) return n;
        n = v < n.Value ? n.Left : n.Right;
    }
    return null;
}

void Delete(int v)
{
    var n = Search(v);
    if (n is null) return;
    if (n.Left != null && n.Right != null)
    {
        // Two children: copy the in-order successor's value, then delete the successor instead.
        var s = n.Right;
        while (s.Left != null) { s.Visit(); s = s.Left; }
        n.SetValue(s.Value);
        n = s;
    }
    var child = n.Left ?? n.Right; // at most one child now
    var parent = n.Parent;
    if (parent is null)
    {
        if (child is null) { n.Remove(); return; }
        bst.SetRoot(child);
    }
    else if (parent.Left == n) parent.SetLeft(child);
    else parent.SetRight(child);
    n.Remove(); // n is detached now
}

foreach (var v in new[] { 50, 30, 70, 20, 40, 60, 80, 35, 45 }) Insert(v);

Viz.Step("search 45");
Search(45)?.Mark(VizColor.Done);

Viz.Step("delete 20");
Delete(20);
Viz.Step("delete 30");
Delete(30);
Viz.Step("delete 50");
Delete(50);
