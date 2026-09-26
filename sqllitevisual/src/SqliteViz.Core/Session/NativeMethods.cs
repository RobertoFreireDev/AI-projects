using System.Runtime.InteropServices;
using SQLitePCL;

namespace SqliteViz.Core.Session;

/// <summary>
/// Direct calls into the bundled e_sqlite3 library for APIs SQLitePCLRaw 2.x does not wrap.
/// Same native library, no extra package.
/// </summary>
internal static class NativeMethods
{
    private static bool _errorOffsetUnavailable;

    [DllImport("e_sqlite3", EntryPoint = "sqlite3_error_offset", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_error_offset(IntPtr db);

    /// <summary>Byte offset of the token that caused the last error (SQLite 3.38+), or null when unknown.</summary>
    public static int? ErrorOffset(sqlite3 db)
    {
        if (_errorOffsetUnavailable) return null;
        try
        {
            var offset = sqlite3_error_offset(db.DangerousGetHandle());
            return offset >= 0 ? offset : null;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            _errorOffsetUnavailable = true;
            return null;
        }
    }
}
