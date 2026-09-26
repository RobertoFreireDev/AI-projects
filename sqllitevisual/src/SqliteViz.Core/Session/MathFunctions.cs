using Microsoft.Data.Sqlite;

namespace SqliteViz.Core.Session;

/// <summary>
/// Fallback for SQLite builds without SQLITE_ENABLE_MATH_FUNCTIONS: registers the same names as
/// deterministic application functions so the math sample works either way.
/// </summary>
public static class MathFunctions
{
    public static void Register(SqliteConnection connection)
    {
        void Unary(string name, Func<double, double> f)
            => connection.CreateFunction(name, (double? x) => x is { } v ? Nan(f(v)) : null, isDeterministic: true);
        void Binary(string name, Func<double, double, double> f)
            => connection.CreateFunction(name, (double? x, double? y) => x is { } a && y is { } b ? Nan(f(a, b)) : null, isDeterministic: true);

        Unary("acos", Math.Acos); Unary("acosh", Math.Acosh); Unary("asin", Math.Asin); Unary("asinh", Math.Asinh);
        Unary("atan", Math.Atan); Unary("atanh", Math.Atanh); Unary("cos", Math.Cos); Unary("cosh", Math.Cosh);
        Unary("sin", Math.Sin); Unary("sinh", Math.Sinh); Unary("tan", Math.Tan); Unary("tanh", Math.Tanh);
        Unary("exp", Math.Exp); Unary("ln", Math.Log); Unary("log", Math.Log10); Unary("log10", Math.Log10);
        Unary("log2", Math.Log2); Unary("sqrt", Math.Sqrt);
        Unary("ceil", Math.Ceiling); Unary("ceiling", Math.Ceiling); Unary("floor", Math.Floor); Unary("trunc", Math.Truncate);
        Unary("degrees", x => x * 180.0 / Math.PI); Unary("radians", x => x * Math.PI / 180.0);
        Unary("sign", x => Math.Sign(x));
        Binary("atan2", Math.Atan2); Binary("pow", Math.Pow); Binary("power", Math.Pow);
        Binary("mod", (a, b) => a % b);
        Binary("log", (b, x) => Math.Log(x) / Math.Log(b));
        connection.CreateFunction("pi", () => Math.PI, isDeterministic: true);
    }

    // SQLite's math functions return NULL for NaN results (e.g. sqrt(-1)).
    private static double? Nan(double value) => double.IsNaN(value) ? null : value;
}
