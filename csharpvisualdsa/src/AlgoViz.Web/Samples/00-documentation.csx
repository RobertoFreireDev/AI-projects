// Documentation
// A tour of the whole Viz API. Run it (Ctrl+Enter) and step through: every section is a marker on the timeline.
// Every call below records one animation step, except the ones marked "silent" (reading sizes, links, values).
// Colors for Mark(...): VizColor.Active, VizColor.Done, VizColor.Warn, VizColor.Path, VizColor.Muted.

// ===== Global functions =====
Viz.Step("Global");                    // Step(label): a named marker on the timeline
Viz.Log("Hello from Viz.Log");         // Log(message): writes a line to the log panel
Viz.Var("answer", 42);                 // Var(name, value): shows/updates a value in the watch panel
string text = Viz.Format(3.14159);     // Format(value): the display text of a value (silent) -> "3.142"

// ===== Array: fixed size, index-based =====
Viz.Step("Array");
var arr = Viz.Array("arr", 5, 2, 8, 1);   // Array(label, values...): an array with these items
var empty = Viz.Array<int>("zeros", 3);   // Array<T>(label, size): an array of `size` default values
arr.Set(0, 7);                            // Set(i, value): writes item i
int first = arr.Get(0);                   // Get(i): reads item i (highlighted)
arr.Swap(0, 3);                           // Swap(i, j): swaps two items
if (arr.Compare(1, 2) < 0) Viz.Log("arr[1] < arr[2]"); // Compare(i, j): <0, 0 or >0
arr.Mark(3, VizColor.Done);               // Mark(i, color): colors item i until Unmark
arr.Unmark(3);                            // Unmark(i): removes the color
arr.Pointer("i", 1);                      // Pointer(name, i): an arrow under index i (-1..Length allowed)
arr.RemovePointer("i");                   // RemovePointer(name): hides that arrow
arr.ShowAsTree();                         // ShowAsTree(): also draws it as a binary tree (heaps)
Viz.Var("arr.Length", arr.Length);        // Length / Count / IsEmpty: sizes (silent)
int[] copy = arr.ToArray();               // ToArray(): a plain copy of the items (silent)

// ===== List: an array that can grow and shrink (has all Array operations too) =====
Viz.Step("List");
var list = Viz.List("list", 10, 20);   // List(label, values...): a growable list
list.Add(30);                          // Add(value): appends at the end
list.Insert(0, 5);                     // Insert(i, value): inserts at index i
list.RemoveAt(1);                      // RemoveAt(i): removes the item at i
list.Remove(30);                       // Remove(value): removes the first match; false if missing
int where = list.IndexOf(20);          // IndexOf(value): index or -1 (silent)
list.Clear();                          // Clear(): removes everything

// ===== Stack: last in, first out =====
Viz.Step("Stack");
var stack = Viz.Stack<char>("stack"); // Stack<T>(label): an empty stack
stack.Push('a');                      // Push(value): puts on top
stack.Push('b');
char top = stack.Peek();              // Peek(): reads the top without removing it
char popped = stack.Pop();            // Pop(): removes and returns the top

// ===== Queue: first in, first out =====
Viz.Step("Queue");
var queue = Viz.Queue<int>("queue");  // Queue<T>(label): an empty queue
queue.Enqueue(1);                     // Enqueue(value): adds at the back
queue.Enqueue(2);
int front = queue.Peek();             // Peek(): reads the front
int served = queue.Dequeue();         // Dequeue(): removes and returns the front

// ===== Deque: add/remove at both ends =====
Viz.Step("Deque");
var deque = Viz.Deque<int>("deque");  // Deque<T>(label): an empty double-ended queue
deque.PushBack(2);                    // PushBack(value) / PushFront(value): add at either end
deque.PushFront(1);
int f = deque.PeekFront();            // PeekFront() / PeekBack(): read either end
int b = deque.PeekBack();
deque.PopFront();                     // PopFront() / PopBack(): remove from either end
deque.PopBack();

// ===== Linked list: nodes with Value and Next =====
Viz.Step("LinkedList");
var ll = Viz.LinkedList<int>("linked"); // LinkedList<T>(label): an empty linked list
var n2 = ll.AddLast(2);                 // AddLast(value) / AddFirst(value): returns the new node
var n1 = ll.AddFirst(1);
var n3 = ll.InsertAfter(n2, 3);         // InsertAfter(node, value): a new node after `node`
n1.Visit();                             // node.Visit(): highlights a node
n1.SetValue(10);                        // node.SetValue(value): changes the value
Viz.Var("head", ll.Head!.Value);        // Head, Tail, node.Next, node.Value, Count: navigation (silent)
ll.SetNext(n3, n1);                     // SetNext(node, other): repoints node.Next (other may be null)
ll.SetNext(n3, null);
ll.SetHead(n2);                         // SetHead(node): makes `node` the first node
ll.Remove(n1);                          // Remove(node): unlinks a node

// ===== Tree: each node has any number of children =====
Viz.Step("Tree");
var tree = Viz.Tree<string>("tree");    // Tree<T>(label): an empty n-ary tree
var root = tree.SetRoot("root");        // SetRoot(value): creates the root node
var docs = root.AddChild("docs");       // node.AddChild(value): adds a last child
var src = root.InsertChild(0, "src");   // node.InsertChild(i, value): adds a child at position i
var tmp = src.AddChild("tmp");
tmp.SetValue("temp");                   // node.SetValue(value): renames the node
tmp.MoveTo(docs);                       // node.MoveTo(newParent, index = -1): moves the whole subtree
docs.Visit();                           // node.Visit(): highlights a node
docs.Mark(VizColor.Path);               // node.Mark(color) / node.Unmark(): persistent color
docs.Unmark();
tmp.Remove();                           // node.Remove(): deletes the node and its subtree
Viz.Var("children", root.Children.Count); // Root, Children, Parent, IsLeaf, Value, Count: navigation (silent)

// ===== Binary tree: each node has Left and Right =====
Viz.Step("BinaryTree");
var bt = Viz.BinaryTree<int>("bst");    // BinaryTree<T>(label): an empty binary tree
var r = bt.SetRoot(8);                  // SetRoot(value): creates the root node
var left = r.SetLeft(3);                // node.SetLeft(value) / SetRight(value): creates a child
var right = r.SetRight(10);
left.SetRight(6);
r.SetLeft(null);                        // SetLeft(node) / SetRight(node): attaches an existing node; null detaches
r.SetLeft(left);                        //   (a detached subtree is drawn faded until reattached or removed)
left.Visit();                           // node.Visit(), node.Mark(color), node.Unmark(), node.SetValue(value)
right.Mark(VizColor.Done);
r.RemoveRight();                        // node.RemoveLeft() / RemoveRight(): deletes that subtree
left.Right!.Remove();                   // node.Remove(): deletes the node and its subtree
Viz.Var("root", bt.Root!.Value);        // Root, Left, Right, Parent, IsLeaf, Value, Count: navigation (silent)

// ===== Graph: integer node ids, weighted or not =====
Viz.Step("Graph");
var g = Viz.Graph("graph");             // Graph(label, directed: false): an empty graph
g.AddNode(1);                           // AddNode(id, x?, y?): x/y in pixels, or automatic circle layout
g.AddNode(2);
g.AddNode(3);
g.AddEdge(1, 2);                        // AddEdge(a, b, weight?): connects two nodes
g.AddEdge(2, 3, 4.5);
foreach (var next in g.Neighbors(2))    // Neighbors(id): the connected nodes (highlighted)
    g.Visit(next);                      // Visit(id): highlights a node
g.MarkNode(1, VizColor.Path);           // MarkNode(id, color) / UnmarkNode(id)
g.MarkEdge(1, 2, VizColor.Path);        // MarkEdge(a, b, color) / UnmarkEdge(a, b)
g.UnmarkEdge(1, 2);
g.RemoveEdge(2, 3);                     // RemoveEdge(a, b) / RemoveNode(id)
g.RemoveNode(3);
Viz.Var("edges", g.EdgeCount);          // HasNode, HasEdge, Weight, Nodes, NodeCount, EdgeCount (silent)

// ===== Grid: a 2D table of cells =====
Viz.Step("Grid");
var grid = Viz.Grid("grid", 3, 4, 0);        // Grid(label, rows, cols, fill): every cell = fill
var maze = Viz.CharGrid("maze", "..#", "#.."); // CharGrid(label, rows...): one string per row
var jagged = Viz.Grid("nums", new[] { new[] { 1, 2 }, new[] { 3, 4 } }); // Grid(label, T[][] rows)
grid.Set(1, 2, 9);                           // Set(row, col, value)
int cell = grid.Get(1, 2);                   // Get(row, col)
maze.Mark(0, 0, VizColor.Path);              // Mark(row, col, color) / Unmark(row, col)
maze.Unmark(0, 0);
if (grid.InBounds(2, 3)) Viz.Log($"{grid.Rows}x{grid.Cols}"); // InBounds, Rows, Cols (silent)

// ===== Map: key -> value =====
Viz.Step("Map");
var ages = Viz.Map<string, int>("ages");  // Map<TKey, TValue>(label): an empty map
ages.Set("ann", 31);                      // Set(key, value): adds or updates
int age = ages.Get("ann");                // Get(key): reads a value
bool has = ages.ContainsKey("bob");       // ContainsKey(key): true/false (highlighted)
ages.Remove("ann");                       // Remove(key): false if it wasn't there

// ===== Set: unique values =====
Viz.Step("Set");
var seen = Viz.Set<int>("seen");          // Set<T>(label): an empty set
seen.Add(4);                              // Add(value): false if it was already there
bool dup = seen.Add(4);
bool found = seen.Contains(4);            // Contains(value): true/false (highlighted)
seen.Remove(4);                           // Remove(value)
Viz.Log("Done! Pick another sample from the dropdown.");
