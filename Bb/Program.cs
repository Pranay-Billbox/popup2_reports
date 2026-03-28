using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using static Bb.Form1;

namespace Bb
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Global\\BillBoxGUI_SINGLE_INSTANCE", out createdNew))
            {
                if (!createdNew)
                {
                    BringExistingInstanceToFront();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
        }

        private static void BringExistingInstanceToFront()
        {
            foreach (var p in Process.GetProcessesByName("BillBoxGUI"))
            {
                IntPtr hWnd = p.MainWindowHandle;
                if (hWnd == IntPtr.Zero) continue;

                ShowWindow(hWnd, SW_RESTORE);

                FLASHWINFO fw = new FLASHWINFO();
                fw.cbSize = (uint)Marshal.SizeOf(fw);
                fw.hwnd = hWnd;
                fw.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
                fw.uCount = 5;
                fw.dwTimeout = 0;

                FlashWindowEx(ref fw);
            }
        }


        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
