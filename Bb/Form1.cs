using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Bb
{

    public partial class Form1 : Form
    {
        private List<(string phone, string name)> contacts = new List<(string, string)>();
        private ListBox suggestionBoxMobile;
        private ListBox suggestionBoxName;
        private bool _updatingFields = false;

        private const int WM_SYSCOMMAND = 0x0112;
        private const int WM_KEYDOWN = 0x0100;
        private const int SC_MINIMIZE = 0xF020;
        private const int SC_CLOSE = 0xF060;
        private const int VK_ESCAPE = 0x1B;
        private EventWaitHandle printEvent;
        private Thread eventThread;
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        // --- Win32 imports ---
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")] private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private string dataPath = @"C:\BillBox\common\data.json";

        public Form1()
        {
            this.StartPosition = FormStartPosition.Manual;
            CenterToScreen();
            InitializeComponent();
            InitializeSuggestionBoxes();
            printEvent = new EventWaitHandle(
        false,
        EventResetMode.AutoReset,
        @"Global\BillBox_NewPrintJob"
    );  

            // Start background listener
            eventThread = new Thread(EventLoop)
            {
                IsBackground = true
            };
            eventThread.Start();

            this.MaximizeBox = false;
            this.ControlBox = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.TopMost = true;
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;


            this.Resize += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Minimized)
                    this.WindowState = FormWindowState.Normal;
            };

            button1.Click += (s, e) =>
            {
                try
                {
                    string mobile = boxMobile.Text.Trim();
                    string name = boxName.Text.Trim();
                    if (name.Count() == 0) name = "nill";
                    if (checkBox1.Checked == true)
                    {
                        // --- PRINT SECTION  ---
                        try
                        {
                            string configPath = @"C:\BillBox\common\printer_config.ini";
                            if (!File.Exists(configPath))
                            {
                                MessageBox.Show("Printer config missing", "Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            // first line = printer name
                            string printerName = File.ReadLines(configPath).First().Trim();

                            string prnPath = @"C:\BillBox\Prints\print.prn";

                            bool ok = RawPrinterHelper.PrintFile(printerName, prnPath);
                            byte[] openDrawer = { 0x1B, 0x70, 0x00, 0x19, 0xFA };

                            // Replace "Your Printer Name" with the exact Windows printer name
                            RawPrinterHelper.SendBytesToPrinter(printerName, openDrawer);


                            if (!ok)
                            {
                                MessageBox.Show("Printer Error", "Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Printing failed: " + ex.Message,
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }


                    }
                    if (mobile.Length < 10) mobile = "0000000000";
                    string paper = "0";

                    string args = $"{mobile} \"{name}\" ";

                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = @"C:\BillBox\common\runner.exe",
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };
                    var cash = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = @"C:\BillBox\common\cash.exe",
                        Arguments = null,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };

                    System.Diagnostics.Process.Start(psi);
                    System.Diagnostics.Process.Start(cash);

                    string jsonPath = @"C:\BillBox\common\res.json";
                    JObject doc;

                    if (File.Exists(jsonPath))
                        doc = JObject.Parse(File.ReadAllText(jsonPath));
                    else
                    {
                        doc = new JObject();
                        doc["records"] = new JArray();
                    }

                    JArray records = (JArray)doc["records"];
                    bool exists = records.Any(r => r["customer_phone"]?.ToString() == mobile);

                    if (!exists)
                    {
                        var newRecord = new JObject
                        {
                            ["customer_phone"] = mobile,
                            ["customer_name"] = name
                        };

                        records.Add(newRecord);
                        File.WriteAllText(jsonPath, doc.ToString(Formatting.Indented));
                    }

                    this.Hide();
                    this.ShowInTaskbar = false;

                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error running gui.exe: " + ex.Message);
                }
            };
        }
        private void EventLoop()
        {
            while (true)
            {
                printEvent.WaitOne();   // waits safely

                // Switch to UI thread
                this.BeginInvoke(new Action(() =>
                {
                    OnNewPrintJob();
                }));
            }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Prevent app exit
            e.Cancel = true;
            this.Hide();
            this.ShowInTaskbar = false;
        }

        private void OnNewPrintJob()
        {
            // Bring window up
            ResetFormState();
            this.Show();
            this.WindowState = FormWindowState.Normal;

            this.TopMost = true;
            this.Activate();
            this.TopMost = false;

            // OPTIONAL:
            // Load PRN, ask name/mobile, etc.
        }

       

      

        private void Form1_Load(object sender, EventArgs e)
        {
            this.checkBox1.Checked = false;
            LoadContacts();
            ResetFormState();
            this.Hide();              // 
            this.ShowInTaskbar = false;
        }

        private void InitializeSuggestionBoxes()
        {
            suggestionBoxMobile = CreateSuggestionBox();
            suggestionBoxName = CreateSuggestionBox();

            Controls.Add(suggestionBoxMobile);
            Controls.Add(suggestionBoxName);

            suggestionBoxMobile.BringToFront();
            suggestionBoxName.BringToFront();
        }

        private ListBox CreateSuggestionBox()
        {
            var box = new ListBox
            {
                Visible = false,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 40,
                Font = new Font("Segoe UI", 13),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            box.DrawItem += SuggestionBox_DrawItem;
            box.Click += (s, e) => SelectSuggestion((ListBox)s);
            box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    SelectSuggestion((ListBox)s);
                    e.Handled = true;
                }
            };

            return box;
        }

        private void LoadContacts()
        {
            try
            {
                string jsonPath = @"C:\BillBox\common\res.json";
                if (!File.Exists(jsonPath))
                    return;

                string json = File.ReadAllText(jsonPath);
                var doc = JObject.Parse(json);

                contacts.Clear();
                foreach (var record in doc["records"])
                {
                    string name = record["customer_name"]?.ToString()?.Trim();
                    string phone = record["customer_phone"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(phone) && !string.IsNullOrEmpty(name))
                        contacts.Add((phone, name));
                }
            }
            catch { }

            // --- MOBILE BOX EVENTS ---
            boxMobile.TextChanged += BoxMobile_TextChanged;
            boxMobile.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down && suggestionBoxMobile.Visible)
                {
                    suggestionBoxMobile.Focus();
                    if (suggestionBoxMobile.Items.Count > 0)
                        suggestionBoxMobile.SelectedIndex = 0;
                    e.Handled = true;
                }
            };

            // --- NAME BOX EVENTS ---
            boxName.TextChanged += BoxName_TextChanged;
            boxName.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down && suggestionBoxName.Visible)
                {
                    suggestionBoxName.Focus();
                    if (suggestionBoxName.Items.Count > 0)
                        suggestionBoxName.SelectedIndex = 0;
                    e.Handled = true;
                }
            };

            // --- MOBILE SUGGESTION BOX NAVIGATION ---
            suggestionBoxMobile.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Up && suggestionBoxMobile.SelectedIndex == 0)
                {
                    boxMobile.Focus();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    SelectSuggestion(suggestionBoxMobile);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    suggestionBoxMobile.Visible = false;
                    boxMobile.Focus();
                }
            };

            // --- NAME SUGGESTION BOX NAVIGATION ---
            suggestionBoxName.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Up && suggestionBoxName.SelectedIndex == 0)
                {
                    boxName.Focus();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    SelectSuggestion(suggestionBoxName);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    suggestionBoxName.Visible = false;
                    boxName.Focus();
                }
            };
        }


        private void SuggestionBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            var g = e.Graphics;
            var item = ((ListBox)sender).Items[e.Index].ToString();

            var parts = item.Split('|');
            string line1 = parts.Length > 0 ? parts[0].Trim() : "";
            string line2 = parts.Length > 1 ? parts[1].Trim() : "";

            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                g.FillRectangle(Brushes.LightBlue, e.Bounds);

            using (var font1 = new Font("Segoe UI Semibold", 14))
                g.DrawString(line1, font1, Brushes.Black, e.Bounds.X + 5, e.Bounds.Y + 2);

            using (var font2 = new Font("Segoe UI", 11))
                g.DrawString(line2, font2, Brushes.Gray, e.Bounds.X + 10, e.Bounds.Y + 22);

            e.DrawFocusRectangle();
        }

        private void BoxMobile_TextChanged(object sender, EventArgs e)
        {
            if (_updatingFields) return;
            string text = boxMobile.Text.Trim();
            if (text.Length < 2)
            {
                suggestionBoxMobile.Visible = false;
                return;
            }

            var matches = contacts
                .Where(c => c.phone.Contains(text))
                .Select(c => $"{c.phone}|{c.name}")
                .ToList();

            ShowSuggestions(suggestionBoxMobile, matches, boxMobile);
        }

        private void BoxName_TextChanged(object sender, EventArgs e)
        {
            if (_updatingFields) return;
            string text = boxName.Text.Trim();
            if (text.Length < 2)
            {
                suggestionBoxName.Visible = false;
                return;
            }

            var matches = contacts
                .Where(c => c.name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(c => $"{c.name}|{c.phone}")
                .ToList();

            ShowSuggestions(suggestionBoxName, matches, boxName);
        }
        private void ResetFormState()
        {
            _updatingFields = true;

            // Clear inputs
            boxMobile.Text = "";
            boxName.Text = "";

            // Reset checkbox (e-bill / print)
            checkBox1.Checked = false;

            // Hide suggestion boxes
            suggestionBoxMobile.Visible = false;
            suggestionBoxName.Visible = false;

            // Optional: reset focus
            boxMobile.Focus();

            _updatingFields = false;
        }


        private void ShowSuggestions(ListBox listBox, List<string> matches, TextBox source)
        {
            if (matches.Count == 0)
            {
                listBox.Visible = false;
                return;
            }

            listBox.Items.Clear();
            foreach (var m in matches)
                listBox.Items.Add(m);

            var pos = source.PointToScreen(Point.Empty);
            pos = PointToClient(pos);
            listBox.SetBounds(pos.X, pos.Y + source.Height, source.Width, 160);
            listBox.Visible = true;
        }

        private void SelectSuggestion(ListBox listBox)
        {
            if (listBox.SelectedItem == null) return;
            string selected = listBox.SelectedItem.ToString();
            string[] parts = selected.Split('|');
            _updatingFields = true;

            if (listBox == suggestionBoxMobile && parts.Length >= 2)
            {
                boxMobile.Text = parts[0].Trim();
                boxName.Text = parts[1].Trim();
            }
            else if (listBox == suggestionBoxName && parts.Length >= 2)
            {
                boxName.Text = parts[0].Trim();
                boxMobile.Text = parts[1].Trim();
            }

            _updatingFields = false;
            listBox.Visible = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SYSCOMMAND)
            {
                int wParam = m.WParam.ToInt32() & 0xFFF0;
                if (wParam == SC_MINIMIZE)
                    return;
                if (wParam == SC_CLOSE && ModifierKeys == Keys.Alt)
                    return;
            }

            if (m.Msg == WM_KEYDOWN && m.WParam.ToInt32() == VK_ESCAPE)
                return;

            base.WndProc(ref m);
        }
        private void button1_Click(object sender, EventArgs e) { }
        private void boxName_TextChanged_1(object sender, EventArgs e) { }
        private void checkBox1_CheckedChanged(object sender, EventArgs e) { }

    }
    public static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDatatype;
        }

        [DllImport("winspool.drv", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern int StartDocPrinter(IntPtr hPrinter, int level,
            [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static bool PrintFile(string printerName, string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            byte[] bytes = File.ReadAllBytes(filePath);

            if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
                return false;

            try
            {
                DOCINFOA docInfo = new DOCINFOA
                {
                    pDocName = "Raw Print Job",
                    pOutputFile = null,
                    pDatatype = "RAW"
                };

                int job = StartDocPrinter(hPrinter, 1, docInfo);
                if (job <= 0)
                    return false;

                if (!StartPagePrinter(hPrinter))
                    return false;

                IntPtr unmanagedBytes = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanagedBytes, bytes.Length);

                bool success = WritePrinter(hPrinter, unmanagedBytes, bytes.Length, out _);

                Marshal.FreeHGlobal(unmanagedBytes);

                EndPagePrinter(hPrinter);
                EndDocPrinter(hPrinter);

                return success;
            }
            finally
            {
                ClosePrinter(hPrinter);
            }
        }
        public static bool SendBytesToPrinter(string printerName, byte[] bytes)
        {
            if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
                return false;

            try
            {
                DOCINFOA docInfo = new DOCINFOA
                {
                    pDocName = "RAW_BYTES",
                    pOutputFile = null,
                    pDatatype = "RAW"
                };

                int job = StartDocPrinter(hPrinter, 1, docInfo);
                if (job <= 0)
                    return false;

                if (!StartPagePrinter(hPrinter))
                    return false;

                IntPtr unmanagedBytes = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanagedBytes, bytes.Length);

                bool success = WritePrinter(hPrinter, unmanagedBytes, bytes.Length, out _);

                Marshal.FreeHGlobal(unmanagedBytes);

                EndPagePrinter(hPrinter);
                EndDocPrinter(hPrinter);

                return success;
            }
            finally
            {
                ClosePrinter(hPrinter);
            }
        }

    }
}
