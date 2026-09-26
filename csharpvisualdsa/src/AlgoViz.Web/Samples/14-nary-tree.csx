// N-ary tree: build, rename, move, remove
var fs = Viz.Tree<string>("files");
var root = fs.SetRoot("/");
var src = root.AddChild("src");
var docs = root.AddChild("docs");
var tests = root.AddChild("tests");
src.AddChild("app.cs");
var util = src.AddChild("util.cs");
docs.AddChild("guide.md");
var notes = docs.AddChild("notes.md");
tests.AddChild("app.test");

Viz.Step("rename");
util.SetValue("helpers.cs");

Viz.Step("move");
notes.MoveTo(src, 0);

Viz.Step("remove");
docs.Remove();

int Count(TreeNode<string> n)
{
    n.Visit();
    int total = 1;
    foreach (var child in n.Children) total += Count(child);
    return total;
}
Viz.Step("count");
Viz.Log($"{Count(root)} nodes");
