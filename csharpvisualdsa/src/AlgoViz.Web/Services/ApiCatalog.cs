using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using AlgoViz.Api;

namespace AlgoViz.Web.Services;

public sealed record Completion(string Caption, string Value, string Meta, string Doc);

/// <summary>
/// Autocomplete entries for the editor, built once by reflecting over the public Viz API in the host
/// (never in user code). Engine-facing types (ops, kinds) are left out.
/// </summary>
public sealed class ApiCatalog
{
    private static readonly HashSet<Type> Hidden = [typeof(VizOp), typeof(OpKind), typeof(StructureKind), typeof(VizStructure)];

    public ApiCatalog()
    {
        var entries = new List<Completion>();
        foreach (var type in typeof(Viz).Assembly.GetExportedTypes().Where(t => !Hidden.Contains(t) && !typeof(Exception).IsAssignableFrom(t)))
        {
            var typeName = DisplayName(type);
            if (type.IsEnum)
            {
                entries.Add(new Completion(type.Name, type.Name, "type", typeName));
                entries.AddRange(Enum.GetNames(type).Select(n => new Completion(n, n, type.Name, $"{type.Name}.{n}")));
                continue;
            }
            if (!type.IsAbstract || type.IsSealed) entries.Add(new Completion(type.Name.Split('`')[0], type.Name.Split('`')[0], "type", typeName));

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            foreach (var method in type.GetMethods(flags).Where(m => !m.IsSpecialName && Visible(m)))
                entries.Add(new Completion(method.Name, method.Name, typeName, Signature(method)));
            foreach (var property in type.GetProperties(flags).Where(Visible))
                entries.Add(new Completion(property.Name, property.Name, typeName, $"{DisplayName(property.PropertyType)} {property.Name}"));
        }
        Completions = [.. entries.GroupBy(e => (e.Caption, e.Meta, e.Doc)).Select(g => g.First()).OrderBy(e => e.Caption)];
    }

    public IReadOnlyList<Completion> Completions { get; }

    private static bool Visible(MemberInfo member) =>
        member.DeclaringType != typeof(object)
        && !member.Name.StartsWith("__", StringComparison.Ordinal)
        && member.GetCustomAttribute<EditorBrowsableAttribute>()?.State != EditorBrowsableState.Never
        && member.Name is not (nameof(Equals) or nameof(GetHashCode) or nameof(ToString) or nameof(GetType));

    private static string Signature(MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Where(p => p.GetCustomAttribute<CallerLineNumberAttribute>() is null)
            .Select(p => (p.GetCustomAttribute<ParamArrayAttribute>() is null ? "" : "params ")
                + DisplayName(p.ParameterType) + " " + p.Name + (p.HasDefaultValue ? " = …" : ""));
        var generic = method.IsGenericMethod ? $"<{string.Join(", ", method.GetGenericArguments().Select(a => a.Name))}>" : "";
        return $"{DisplayName(method.ReturnType)} {method.Name}{generic}({string.Join(", ", parameters)})";
    }

    private static string DisplayName(Type type)
    {
        if (type == typeof(void)) return "void";
        if (Nullable.GetUnderlyingType(type) is { } inner) return DisplayName(inner) + "?";
        if (type.IsArray) return DisplayName(type.GetElementType()!) + "[]";
        if (!type.IsGenericType) return type.Name switch
        {
            "Int32" => "int", "Int64" => "long", "Double" => "double", "Boolean" => "bool",
            "String" => "string", "Object" => "object", "Char" => "char", _ => type.Name,
        };
        return $"{type.Name.Split('`')[0]}<{string.Join(", ", type.GetGenericArguments().Select(DisplayName))}>";
    }
}
