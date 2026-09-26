// Sliding window maximum with a deque
var a = Viz.Array("a", 1, 3, -1, -3, 5, 3, 6, 7);
int k = 3;
var window = Viz.Deque<int>("indices");
var result = Viz.List<int>("max");
for (int i = 0; i < a.Length; i++)
{
    a.Pointer("i", i);
    if (!window.IsEmpty && window.PeekFront() <= i - k) window.PopFront();
    while (!window.IsEmpty && a.Compare(window.PeekBack(), i) <= 0) window.PopBack();
    window.PushBack(i);
    if (i >= k - 1) result.Add(a.Get(window.PeekFront()));
}
