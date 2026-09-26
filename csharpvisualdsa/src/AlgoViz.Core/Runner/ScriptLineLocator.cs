using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace AlgoViz.Core.Runner;

/// <summary>Maps stack frames of the script assembly to script lines using its in-memory portable PDB.</summary>
internal sealed class ScriptLineLocator(Assembly assembly, ImmutableArray<byte> pdb) : IDisposable
{
    private readonly MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbImage(pdb);

    /// <summary>The line of the innermost frame that belongs to the script, if any.</summary>
    public int? FindLine(StackTrace trace)
    {
        foreach (var frame in trace.GetFrames())
        {
            var method = frame.GetMethod();
            if (method is null || method.Module.Assembly != assembly) continue;
            if (Lookup(method.MetadataToken, frame.GetILOffset()) is { } line) return line;
        }
        return null;
    }

    public void Dispose() => provider.Dispose();

    private int? Lookup(int methodToken, int ilOffset)
    {
        var reader = provider.GetMetadataReader();
        var handle = MetadataTokens.MethodDefinitionHandle(methodToken & 0x00FFFFFF);
        SequencePoint? best = null;
        foreach (var point in reader.GetMethodDebugInformation(handle.ToDebugInformationHandle()).GetSequencePoints())
        {
            if (point.IsHidden) continue;
            if (ilOffset == StackFrame.OFFSET_UNKNOWN) return point.StartLine;
            if (point.Offset <= ilOffset && (best is null || point.Offset >= best.Value.Offset)) best = point;
        }
        return best?.StartLine;
    }
}
