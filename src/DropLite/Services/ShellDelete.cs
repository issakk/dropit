using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DropLite.Services;

/// <summary>Deletes items to the Recycle Bin through the shell, in one call for the whole batch.</summary>
internal static class ShellDelete
{
    private const uint FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCTW
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCTW op);

    public static bool ToRecycleBin(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return true;
        }

        var op = new SHFILEOPSTRUCTW
        {
            hwnd = IntPtr.Zero,
            wFunc = FO_DELETE,
            // Double-null terminated multi-string.
            pFrom = string.Join('\0', paths) + "\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };

        return SHFileOperation(ref op) == 0 && op.fAnyOperationsAborted == 0;
    }
}
