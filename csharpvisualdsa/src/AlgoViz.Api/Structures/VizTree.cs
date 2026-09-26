using System.Runtime.CompilerServices;

namespace AlgoViz.Api;

/// <summary>An n-ary tree.</summary>
public sealed class VizTree<T> : VizStructure
{
    internal VizTree(string label, int line) : base(label, StructureKind.Tree, 0, null, line) { }

    public TreeNode<T>? Root { get; internal set; }

    public int Count => Root?.SubtreeSize() ?? 0;

    internal int NodeCount { get; set; }

    /// <summary>Creates the root, replacing the whole tree if there was one.</summary>
    public TreeNode<T> SetRoot(T value, [CallerLineNumber] int line = 0)
    {
        Root?.Kill();
        var root = new TreeNode<T>(this, value, null);
        Root = root;
        Record(OpKind.Insert, line, root.Id, value: Viz.Format(value));
        return root;
    }
}

public sealed class TreeNode<T>
{
    private readonly List<TreeNode<T>> children = [];
    private readonly VizTree<T> owner;
    private bool removed;

    internal TreeNode(VizTree<T> owner, T value, TreeNode<T>? parent)
    {
        VizStructure.CheckSize(owner.NodeCount + 1);
        this.owner = owner;
        owner.NodeCount++;
        Id = VizStructure.NewNodeId();
        Value = value;
        Parent = parent;
    }

    public T Value { get; private set; }

    public TreeNode<T>? Parent { get; private set; }

    public IReadOnlyList<TreeNode<T>> Children => children;

    public bool IsLeaf => children.Count == 0;

    internal int Id { get; }

    public TreeNode<T> AddChild(T value, [CallerLineNumber] int line = 0) => InsertChild(children.Count, value, line);

    public TreeNode<T> InsertChild(int index, T value, [CallerLineNumber] int line = 0)
    {
        Alive();
        if ((uint)index > (uint)children.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Child index {index} is outside 0..{children.Count}.");
        var child = new TreeNode<T>(owner, value, this);
        children.Insert(index, child);
        owner.Record(OpKind.Insert, line, child.Id, a: Id, b: index, value: Viz.Format(value));
        return child;
    }

    public void SetValue(T value, [CallerLineNumber] int line = 0)
    {
        Alive();
        Value = value;
        owner.Record(OpKind.Write, line, Id, value: Viz.Format(value));
    }

    /// <summary>Removes this node and its whole subtree.</summary>
    public void Remove([CallerLineNumber] int line = 0)
    {
        Alive();
        if (Parent is null) owner.Root = null;
        else Parent.children.Remove(this);
        Kill();
        owner.Record(OpKind.Remove, line, Id);
    }

    /// <summary>Moves this subtree under <paramref name="newParent"/> at <paramref name="index"/> (-1 = last).</summary>
    public void MoveTo(TreeNode<T> newParent, int index = -1, [CallerLineNumber] int line = 0)
    {
        Alive();
        ArgumentNullException.ThrowIfNull(newParent);
        newParent.Alive();
        if (newParent.owner != owner) throw new InvalidOperationException("Can't move a node to another tree.");
        for (var n = newParent; n is not null; n = n.Parent)
            if (n == this) throw new InvalidOperationException("Can't move a node under itself or its own descendant.");
        if (Parent is null) throw new InvalidOperationException("Can't move the root.");
        Parent.children.Remove(this);
        if (index < 0) index = newParent.children.Count;
        if (index > newParent.children.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Child index {index} is outside 0..{newParent.children.Count}.");
        newParent.children.Insert(index, this);
        Parent = newParent;
        owner.Record(OpKind.Move, line, Id, a: newParent.Id, b: index);
    }

    public void Visit([CallerLineNumber] int line = 0)
    {
        Alive();
        owner.Record(OpKind.Visit, line, Id);
    }

    public void Mark(VizColor color = VizColor.Active, [CallerLineNumber] int line = 0)
    {
        Alive();
        owner.Record(OpKind.Mark, line, Id, text: color.Name());
    }

    public void Unmark([CallerLineNumber] int line = 0)
    {
        Alive();
        owner.Record(OpKind.Unmark, line, Id);
    }

    public override string ToString() => Viz.Format(Value);

    internal int SubtreeSize() => 1 + children.Sum(c => c.SubtreeSize());

    internal void Kill()
    {
        removed = true;
        owner.NodeCount--;
        foreach (var c in children) c.Kill();
    }

    private void Alive()
    {
        if (removed) throw new InvalidOperationException("That node was removed from the tree.");
    }
}
