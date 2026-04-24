using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Flow.Launcher.Plugin.Codebases
{
    internal static class EverythingSDK
    {
        public const uint EVERYTHING_ERROR_IPC = 2;
        public const uint EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;
        public const uint EVERYTHING_SORT_DATE_MODIFIED_DESCENDING = 14;

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern uint Everything_SetSearchW(string lpSearchString);

        [DllImport("Everything64.dll")]
        public static extern void Everything_SetSort(uint dwSortType);

        [DllImport("Everything64.dll")]
        public static extern void Everything_SetRequestFlags(uint dwRequestFlags);

        [DllImport("Everything64.dll")]
        public static extern bool Everything_QueryW(bool bWait);

        [DllImport("Everything64.dll")]
        public static extern uint Everything_GetNumResults();

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern void Everything_GetResultFullPathName(uint nIndex, StringBuilder lpString, uint nMaxCount);

        [DllImport("Everything64.dll")]
        public static extern uint Everything_GetLastError();

        [DllImport("Everything64.dll")]
        public static extern uint Everything_GetMajorVersion();
    }
}
