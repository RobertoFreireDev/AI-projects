// Tree traversals: pre-, in-, post- and level-order
var t = Viz.BinaryTree<char>("t");
var root = t.SetRoot('F');
var b = root.SetLeft('B');
var g = root.SetRight('G');
b.SetLeft('A');
var d = b.SetRight('D');
d.SetLeft('C');
d.SetRight('E');
g.SetRight('I').SetLeft('H');

var order = new StringBuilder();
void Emit(BinaryNode<char> n)
{
    n.Visit();
    order.Append(n.Value);
    Viz.Var("order", order.ToString());
}

void Pre(BinaryNode<char> n) { if (n is null) return; Emit(n); Pre(n.Left); Pre(n.Right); }
void In(BinaryNode<char> n) { if (n is null) return; In(n.Left); Emit(n); In(n.Right); }
void Post(BinaryNode<char> n) { if (n is null) return; Post(n.Left); Post(n.Right); Emit(n); }

Viz.Step("pre-order");
order.Clear(); Pre(t.Root); Viz.Log("pre:   " + order);
Viz.Step("in-order");
order.Clear(); In(t.Root); Viz.Log("in:    " + order);
Viz.Step("post-order");
order.Clear(); Post(t.Root); Viz.Log("post:  " + order);

Viz.Step("level-order");
order.Clear();
var q = Viz.Queue<BinaryNode<char>>("queue");
q.Enqueue(t.Root);
while (!q.IsEmpty)
{
    var n = q.Dequeue();
    Emit(n);
    if (n.Left != null) q.Enqueue(n.Left);
    if (n.Right != null) q.Enqueue(n.Right);
}
Viz.Log("level: " + order);
