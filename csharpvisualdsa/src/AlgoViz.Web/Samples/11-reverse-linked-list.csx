// Reverse a linked list
var list = Viz.LinkedList<int>("list");
foreach (var v in new[] { 1, 2, 3, 4, 5 }) list.AddLast(v);

LinkedNode<int> prev = null;
var cur = list.Head;
while (cur != null)
{
    cur.Visit();
    var next = cur.Next;
    list.SetNext(cur, prev);
    prev = cur;
    cur = next;
}
list.SetHead(prev);
