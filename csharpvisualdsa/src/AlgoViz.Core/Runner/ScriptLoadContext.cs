using System.Reflection;
using System.Runtime.Loader;

namespace AlgoViz.Core.Runner;

/// <summary>
/// Collectible context for one script assembly. Every dependency (framework and AlgoViz.Api) resolves to the
/// default context, so the script shares the recorder types with the host.
/// </summary>
internal sealed class ScriptLoadContext() : AssemblyLoadContext("AlgoViz script", isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName) => null;
}
