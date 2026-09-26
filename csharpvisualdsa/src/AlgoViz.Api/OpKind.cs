namespace AlgoViz.Api;

/// <summary>
/// The kind of a recorded <see cref="VizOp"/>. Structure ids start at 1; <c>StructureId = 0</c> means a global op.
/// "Sequence" below means Array, List, Stack, Queue and Deque. Colors travel in <c>Text</c> as lower-case
/// <see cref="VizColor"/> names.
/// </summary>
public enum OpKind
{
    /// <summary>
    /// A new structure. <c>A</c> = <c>(int)</c><see cref="StructureKind"/>, <c>Text</c> = label.
    /// Sequence/Map/Set: <c>B</c> = initial count, <c>Value</c> = initial values packed with <see cref="VizOp.Pack"/>.
    /// Grid: <c>B</c> = rows, <c>Value</c> = all cells packed row-major (cols = cells / rows).
    /// Graph: <c>B</c> = 1 if directed, else 0.
    /// </summary>
    Create,

    /// <summary>
    /// A recorded read (transient highlight).
    /// Sequence: <c>A</c> = index, <c>Value</c>. Grid: <c>A</c> = row, <c>B</c> = col, <c>Value</c>.
    /// Map (Get/ContainsKey): <c>NodeId</c> = entry id or null when missing, <c>Text</c> = key, <c>Value</c>.
    /// Set (Contains): <c>NodeId</c> = entry id or null when missing, <c>Value</c>.
    /// Graph (Neighbors): <c>NodeId</c> = node.
    /// </summary>
    Read,

    /// <summary>
    /// Array/List: <c>A</c> = index, <c>Value</c>. Grid: <c>A</c> = row, <c>B</c> = col, <c>Value</c>.
    /// Map: <c>NodeId</c> = entry id (new or existing), <c>Text</c> = key, <c>Value</c>.
    /// LinkedList/Tree/BinaryTree (SetValue): <c>NodeId</c>, <c>Value</c>.
    /// </summary>
    Write,

    /// <summary>Array/List: <c>A</c> = i, <c>B</c> = j, <c>Value</c> = "&lt;", "=" or "&gt;" (item i vs item j).</summary>
    Compare,

    /// <summary>Array/List: <c>A</c> = i, <c>B</c> = j.</summary>
    Swap,

    /// <summary>
    /// List: <c>A</c> = index, <c>Value</c>.
    /// LinkedList: <c>NodeId</c> = new node, <c>A</c> = node it goes after (null = new head), <c>Value</c>.
    /// Tree: <c>NodeId</c> = new node, <c>A</c> = parent (null = new root, replacing the whole tree), <c>B</c> = child index, <c>Value</c>.
    /// BinaryTree: like Tree, <c>B</c> = 0 left / 1 right; a displaced child becomes a detached subtree.
    /// Graph: <c>NodeId</c> = node id, <c>A</c> = x, <c>B</c> = y (null = automatic layout).
    /// Set: <c>NodeId</c> = entry id (an existing id means the value was already present), <c>Value</c>.
    /// </summary>
    Insert,

    /// <summary>
    /// List: <c>A</c> = index (null = <c>Remove(v)</c> found nothing), <c>Text</c> = "*" for Clear.
    /// LinkedList/Graph: <c>NodeId</c>. Tree/BinaryTree: <c>NodeId</c> (whole subtree).
    /// Map: <c>NodeId</c> = entry id (null = missing), <c>Text</c> = key. Set: <c>NodeId</c> (null = missing), <c>Value</c>.
    /// </summary>
    Remove,

    /// <summary>
    /// Tree: <c>NodeId</c>, <c>A</c> = new parent, <c>B</c> = child index.
    /// BinaryTree: <c>NodeId</c>, <c>A</c> = new parent (null = becomes the root; the old root is detached), <c>B</c> = 0/1.
    /// A displaced child becomes a detached subtree.
    /// </summary>
    Move,

    /// <summary>Stack/Deque: <c>A</c> = index inserted at, <c>Value</c>.</summary>
    Push,

    /// <summary>Stack/Deque: <c>A</c> = index removed, <c>Value</c> = removed value.</summary>
    Pop,

    /// <summary>Queue: <c>A</c> = index inserted at (the back), <c>Value</c>.</summary>
    Enqueue,

    /// <summary>Queue: <c>A</c> = index removed (0), <c>Value</c> = removed value.</summary>
    Dequeue,

    /// <summary>LinkedList/Tree/BinaryTree/Graph: <c>NodeId</c> (transient highlight).</summary>
    Visit,

    /// <summary>
    /// Array/List: <c>A</c> = index, <c>Text</c> = color; <c>A</c> = null with <c>Text</c> = "view:tree" is ShowAsTree.
    /// Grid: <c>A</c> = row, <c>B</c> = col, <c>Text</c>. Tree/BinaryTree/Graph node: <c>NodeId</c>, <c>Text</c>.
    /// Graph edge: <c>NodeId</c> = null, <c>A</c>, <c>B</c> = endpoints, <c>Text</c>.
    /// </summary>
    Mark,

    /// <summary>Same fields as <see cref="Mark"/>, without a color.</summary>
    Unmark,

    /// <summary>Array/List: <c>Text</c> = pointer name, <c>A</c> = index (null = remove the pointer).</summary>
    Pointer,

    /// <summary>
    /// LinkedList SetNext: <c>NodeId</c> = node, <c>A</c> = next node (null = none).
    /// LinkedList SetHead: <c>NodeId</c> = null, <c>A</c> = new head (null = empty).
    /// Graph AddEdge: <c>A</c> = from, <c>B</c> = to, <c>Value</c> = weight (null = unweighted); an existing edge is updated.
    /// </summary>
    Link,

    /// <summary>
    /// Graph RemoveEdge: <c>A</c>, <c>B</c>.
    /// BinaryTree SetLeft/SetRight(null): <c>NodeId</c> = parent, <c>B</c> = 0/1; the old child is detached.
    /// </summary>
    Unlink,

    /// <summary>Global: <c>Text</c> = message.</summary>
    Log,

    /// <summary>Global: <c>Text</c> = variable name, <c>Value</c>.</summary>
    Var,

    /// <summary>Global: <c>Text</c> = checkpoint label shown on the timeline.</summary>
    Step,
}
