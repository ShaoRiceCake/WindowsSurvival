using System;
using System.Runtime.InteropServices;

public static class SaveFileDialog
{
    public static string Open()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel("导入世界线", "", "wssave");
#elif UNITY_STANDALONE_WIN
        var dialog = new OpenFileName();
        dialog.structSize = Marshal.SizeOf(dialog);
        dialog.filter = "麦麦的世界线 (*.wssave)\0*.wssave\0\0";
        dialog.file = new string('\0', 4096); dialog.maxFile = dialog.file.Length;
        dialog.title = "导入世界线"; dialog.flags = 0x00001000 | 0x00000800 | 0x00080000 | 0x00000008;
        return GetOpenFileName(dialog) ? dialog.file : null;
#else
        return null; // The same screen also accepts a pasted absolute path.
#endif
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class OpenFileName
    {
        public int structSize; public IntPtr owner, instance;
        public string filter, customFilter; public int maxCustomFilter, filterIndex;
        public string file; public int maxFile; public string fileTitle; public int maxFileTitle;
        public string initialDirectory, title; public int flags; public short fileOffset, fileExtension;
        public string defaultExtension; public IntPtr customData, hook; public string templateName;
        public IntPtr reserved; public int reservedInt, flagsEx;
    }
    [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
}
