using System.Runtime.CompilerServices;

namespace AlgoViz.Api;

/// <summary>
/// A binary tree. Replacing or unlinking a child detaches its subtree instead of deleting it (shown faded),
/// so it can be reattached, e.g. in rotations; call <see cref="BinaryNode{T}.Remove"/> to delete it.
/// </summary>
public sealed class VizBinaryTree<T> : VizStructure
{
    internal readonly List<BinaryNode<T>> Detached = [];

    internal VizBinaryTree(string label, int line) : base(label, StructureKind.BinaryTree, 0, null, line) { }

    public BinaryNode<T>? Root { get; internal set; }

    public int Count => Root?.SubtreeSize() ?? 0;

    internal int NodeCount { get; set; }

    /// <summary>Creates the root, replacing the whole tree (including detached subtrees).</summary>
    public BinaryNode<T> SetRoot(T value, [CallerLineNumber] int line = 0)
    {
        Root?.Kill();
        foreach (var d in Detached) d.Kill();
        Detached.Clear();
        var root = new BinaryNode<T>(this, value);
        Root = root;
        Record(OpKind.Insert, line, root.Id, value: Viz.Format(value));
        return root;
    }

    /// <summary>Makes an existing node the root; the old root (if different) becomes detached.</summary>
    public void SetRoot(BinaryNode<T> node, [CallerLineNumber] int line = 0)
    {
        Own(node);
        if (node != Root)
        {
            node.Detach();
            if (Root is not null) Detached.Add(Root);
            Root = node;
        }
        Record(OpKind.Move, line, node.Id);
    }

    internal void Own(BinaryNode<T> node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Owner != this || node.Removed)
            throw new InvalidOperationException($"That node is not in '{Label}' (removed or from another tree).");
    }
}

public sealed class BinaryNode<T>
{
    internal BinaryNode(VizBinaryTree<T> owner, T value)
    {
        VizStructure.CheckSize(owner.NodeCount + 1);
        Owner = owner;
        owner.NodeCount++;
        Id = VizStructure.NewNodeId();
        Value = value;
    }

    public T Value { get; private set; }

    public BinaryNode<T>? Left { get; private set; }

    public BinaryNode<T>? Right { get; private set; }

    public BinaryNode<T>? Parent { get; private set; }

    public bool IsLeaf => Left is null && Right is null;

    internal VizBinaryTree<T> Owner { get; }

    internal int Id { get; }

    internal bool Removed { get; private set; }

    public BinaryNode<T> SetLeft(T value, [CallerLineNumber] int line = 0) => AddChild(0, value, line);

    public BinaryNode<T> SetRight(T value, [CallerLineNumber] int line = 0) => AddChild(1, value, line);

    /// <summary>Attaches an existing node (with its subtree) as the left child; null just detaches the current one.</summary>
    public void SetLeft(BinaryNode<T>? node, [CallerLineNumber] int line = 0) => Attach(0, node, line);

    /// <summary>Attaches an existing node (with its subtree) as the right child; null just detaches the current one.</summary>
    public void SetRight(BinaryNode<T>? node, [CallerLineNumber] int line = 0) => Attach(1, node, line);

    /// <summary>Deletes the left subtree.</summary>
    public void RemoveLeft([CallerLineNumber] int line = 0) =>
        (Left ?? throw new InvalidOperationException($"Node {this} has no left child.")).Remove(line);

    /// <summary>Deletes the right subtree.</summary>
    public void RemoveRight([CallerLineNumber] int line = 0) =>
        (Right ?? throw new InvalidOperationException($"Node {this} has no right child.")).Remove(line);

    /// <summary>Deletes this node and its subtree, wherever it is (attached or detached).</summary>
    public void Remove([CallerLineNumber] int line = 0)
    {
        Owner.Own(this);
        Detach();
        Owner.Detached.Remove(this);
        Kill();
        Owner.Record(OpKind.Remove, line, Id);
    }

    public void SetValue(T value, [CallerLineNumber] int line = 0)
    {
        Owner.Own(this);
        Value = value;
        Owner.Record(OpKind.Write, line, Id, value: Viz.Format(value));
    }

    public void Visit([CallerLineNumber] int line = 0)
    {
        Owner.Own(this);
        Owner.Record(OpKind.Visit, line, Id);
    }

    public void Mark(VizColor color = VizColor.Active, [CallerLineNumber] int line = 0)
    {
        Owner.Own(this);
        Owner.Record(OpKind.Mark, line, Id, text: color.Name());
    }

    public void Unmark([CallerLineNumber] int line = 0)
    {
        Owner.Own(this);
        Owner.Record(OpKind.Unmark, line, Id);
    }

    public override string ToString() => Viz.Format(Value);

    internal int SubtreeSize() => 1 + (Left?.SubtreeSize() ?? 0) + (Right?.SubtreeSize() ?? 0);

    private BinaryNode<T> AddChild(int slot, T value, int line)
    {
        Owner.Own(this);
        var child = new BinaryNode<T>(Owner, value);
        DetachChild(slot);
        SetSlot(slot, child);
        Owner.Record(OpKind.Insert, line, child.Id, a: Id, b: slot, value: Viz.Format(value));
        return child;
    }

    private void Attach(int slot, BinaryNode<T>? node, int line)
    {
        Owner.Own(this);
        if (node is null)
        {
            DetachChild(slot);
            Owner.Record(OpKind.Unlink, line, Id, b: slot);
            return;
        }
        Owner.Own(node);
        for (var n = this; n is not null; n = n.Parent)
            if (n == node)
                throw new InvalidOperationException(
                    $"Attaching {node} under {this} would create a cycle ({node} is {this} or its ancestor). Detach it first.");
        if (Child(slot) != node)
        {
            node.Detach();
            DetachChild(slot);
            SetSlot(slot, node);
        }
        Owner.Record(OpKind.Move, line, node.Id, a: Id, b: slot);
    }

    /// <summary>Unhooks this node from its parent, the root slot or the detached list.</summary>
    internal void Detach()
    {
        if (Parent is not null)
        {
            if (Parent.Left == this) Parent.Left = null;
            else Parent.Right = null;
            Parent = null;
        }
        else if (Owner.Root == this) Owner.Root = null;
        else Owner.Detached.Remove(this);
    }

    internal void Kill()
    {
        Removed = true;
        Owner.NodeCount--;
        Left?.Kill();
        Right?.Kill();
    }

    private BinaryNode<T>? Child(int slot) => slot == 0 ? Left : Right;

    private void DetachChild(int slot)
    {
        var child = Child(slot);
        if (child is null) return;
        child.Detach();
        Owner.Detached.Add(child);
    }

    private void SetSlot(int slot, BinaryNode<T> child)
    {
        if (slot == 0) Left = child;
        else Right = child;
        child.Parent = this;
    }
}
