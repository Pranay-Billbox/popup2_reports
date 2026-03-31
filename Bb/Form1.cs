using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
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

        public Form1()
        {
            this.StartPosition = FormStartPosition.Manual;
            CenterToScreen();
            InitializeComponent();

            //// Disable TAB traversal completely (POS behavior)
            //boxMobile.TabStop = false;
            //boxName.TabStop = false;
            //button1.TabStop = false;
            //checkBox1.TabStop = false;
            //checkBox2.TabStop = false;

            InitializeSuggestionBoxes();
            
            this.MaximizeBox = false;
            this.ControlBox = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.TopMost = true;
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
            this.WindowState = FormWindowState.Minimized;
            this.Shown += (s, e) =>
            {
                // ✅ FINAL authority for checkbox state
                checkBox1.Checked = Properties.Settings.Default.PaperBillChecked;
            };
            this.WindowState = FormWindowState.Normal;

            this.Resize += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Minimized)
                    this.WindowState = FormWindowState.Normal;
            };
            button1.Click += (s, e) =>
            {
                try
                {

                    // ✅ FORCE SAVE Paper‑Bill before app exit
                    Properties.Settings.Default.PaperBillChecked = checkBox1.Checked;
                    Properties.Settings.Default.Save();
                    string mobile = boxMobile.Text.Trim();
                    string name = boxName.Text.Trim();
                    string prnPath = @"C:\BillBox\Prints\print.prn";
                    string pdfPath = @"C:\BillBox\Bills\TEMP_CONVERTED.pdf";
                    string store_id_path = @"C:\BillBox\common\store_id.ini";
                    string store_id = ReadStringFromIniFile(store_id_path);

                    if (string.IsNullOrEmpty(name))
                        name = "nill";
                    if (string.IsNullOrEmpty(mobile))
               
                        mobile = "0000000000";
                    

                    // --- PRINT ---
                    if (checkBox1.Checked)
                    {
                        try
                        {
                            string configPath = @"C:\BillBox\common\printer_config.ini";
                            if (!File.Exists(configPath)) { MessageBox.Show("Printer config missing"); return; }

                            string printerName = File.ReadLines(configPath).First().Trim();

                            bool ok = RawPrinterHelper.PrintFile(printerName, prnPath);

                            byte[] openDrawer = { 0x1B, 0x70, 0x00, 0x19, 0xFA };
                            byte[] cutCommand = new byte[] { 0x1D, 0x56, 0x00 };

                            RawPrinterHelper.SendBytesToPrinter(printerName, cutCommand);

                            if (!ok) { MessageBox.Show("Printer Error"); return; }
                        }
                        catch (Exception ex) { MessageBox.Show("Printing failed: " + ex.Message); return; }
                    }

                    // --- SAVE CONTACT ---
                    string jsonPath = @"C:\BillBox\common\res.json";
                    JObject doc = File.Exists(jsonPath) ? JObject.Parse(File.ReadAllText(jsonPath)) : new JObject { ["records"] = new JArray() };
                    JArray records = (JArray)doc["records"];

                    if (!records.Any(r => r["customer_phone"]?.ToString() == mobile))
                    {
                        records.Add(new JObject { ["customer_phone"] = mobile, ["customer_name"] = name });
                        File.WriteAllText(jsonPath, doc.ToString(Formatting.Indented));
                    }

                    // Capture values for background thread before UI closes
                    string capturedMobile = mobile;
                    string capturedStoreId = store_id;
                    string capturedName = name;

                    this.Hide();

                    // --- BACKGROUND: convert + upload ---
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {

                            var psi = new ProcessStartInfo
                            {
                                FileName = @"C:\BillBox\common\convert.exe",
                                WorkingDirectory = @"C:\BillBox\common",  // <-- this is the fix
                                UseShellExecute = true,
                                CreateNoWindow = true
                            };

                            using (var proc = Process.Start(psi))
                            {
                                proc.WaitForExit();
                            }
                            if (checkBox2.Checked)
                            {
                                string owner_path = @"C:\BillBox\common\owner-mobile.ini";
                                string owner_mobile = ReadStringFromIniFile(owner_path);
                                UploadBillPdf(pdfPath, owner_mobile, store_id,"1234");
                            }
                            else
                            {
                                UploadBillPdf(pdfPath, capturedMobile, capturedStoreId,capturedName);
                            }
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(@"C:\BillBox\log.txt",
                                DateTime.Now + " ERROR: " + ex + Environment.NewLine);
                        }
                        finally
                        {
                            Application.Exit();
                        }
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            };
        }
        public static string ReadStringFromIniFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new Exception("Failed to open file");

            var content = new StringBuilder();

            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string trimmed = line.Trim();

                if (!string.IsNullOrEmpty(trimmed))
                    content.Append(trimmed);
            }

            return content.ToString();
        }
        private string UploadBillPdf(string filePath, string mobileNumber, string storeId,string fileName)
        {
            string url = "https://api.billbox.co.in/upload-bill";

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("PDF file not found.", filePath);

            if (string.IsNullOrWhiteSpace(mobileNumber))
                throw new ArgumentException("Mobile number is required.", nameof(mobileNumber));

            if (string.IsNullOrWhiteSpace(storeId))
                throw new ArgumentException("Store ID is required.", nameof(storeId));

            byte[] fileBytes = File.ReadAllBytes(filePath);
          //  string fileName = Path.GetFileName(filePath);

            Type winHttpType = Type.GetTypeFromProgID("WinHttp.WinHttpRequest.5.1");
            if (winHttpType == null)
                throw new Exception("WinHttp.WinHttpRequest.5.1 is not available on this system.");

            dynamic request = Activator.CreateInstance(winHttpType);

            request.Open("POST", url, false);
            request.SetRequestHeader("Content-Type", "application/pdf");
            request.SetRequestHeader("x-mobile", mobileNumber);
            request.SetRequestHeader("x-new-dir", storeId);
            request.SetRequestHeader("x-filename", fileName);

            request.Send(fileBytes);

            int status = request.Status;
            string responseText = request.ResponseText;

            if (status < 200 || status >= 300)
                throw new Exception($"Upload failed. HTTP {status}: {responseText}");

            return responseText;
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            
        }



        private void Form1_Load(object sender, EventArgs e)
        {
            //this.checkBox1.Checked = false;
            LoadContacts();


            // ✅ Restore Paper‑Bill (persistent)
            checkBox1.Checked = Properties.Settings.Default.PaperBillChecked;

            // ❌ Reports is session‑only
            checkBox2.Checked = false;


            // ENTER moves from Mobile → Name
            boxMobile.KeyDown += EnterMovesNext;
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
                BackColor = System.Drawing.Color.White,
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


            /* ================= MOBILE TEXTBOX ================= */

            boxMobile.TextChanged += BoxMobile_TextChanged;

            boxMobile.KeyDown += (s, e) =>
            {
                // If suggestion list is visible → handle navigation first
                if (suggestionBoxMobile.Visible)
                {
                    if (e.KeyCode == Keys.Down)
                    {
                        suggestionBoxMobile.Focus();
                        suggestionBoxMobile.SelectedIndex = 0;
                        e.Handled = true;
                        return;
                    }
                    else if (e.KeyCode == Keys.Enter)
                    {
                        if (suggestionBoxMobile.SelectedItem != null)
                        {
                            SelectSuggestion(suggestionBoxMobile);
                            e.SuppressKeyPress = true;
                            return;
                        }
                    }
                }

                // Normal ENTER → move to Name
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    boxName.Focus();
                }
            };

            /* ================= NAME TEXTBOX ================= */

            boxName.TextChanged += BoxName_TextChanged;

            boxName.KeyDown += (s, e) =>
            {
                if (suggestionBoxName.Visible)
                {
                    if (e.KeyCode == Keys.Down)
                    {
                        suggestionBoxName.Focus();
                        suggestionBoxName.SelectedIndex = 0;
                        e.Handled = true;
                        return;
                    }
                    else if (e.KeyCode == Keys.Enter)
                    {
                        if (suggestionBoxName.SelectedItem != null)
                        {
                            SelectSuggestion(suggestionBoxName);
                            e.SuppressKeyPress = true;
                            return;
                        }
                    }
                }

                // ENTER on name → submit
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    button1.PerformClick();
                }
            };

            /* ================= MOBILE SUGGESTION LIST ================= */

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

            /* ================= NAME SUGGESTION LIST ================= */

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

        private void EnterMovesNext(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // remove beep sound

                this.SelectNextControl(
                    (Control)sender,
                    forward: true,
                    tabStopOnly: false,
                    nested: true,
                    wrap: true
                );
            }
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

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Disable TAB and Shift+TAB completely
            if (keyData == Keys.Tab || keyData == (Keys.Tab | Keys.Shift))
                return true;

            return base.ProcessCmdKey(ref msg, keyData);
        }


        private void button1_Click(object sender, EventArgs e) { }
        private void boxName_TextChanged_1(object sender, EventArgs e) { }
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.PaperBillChecked = checkBox1.Checked;
            Properties.Settings.Default.Save();
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {

        }
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

            byte[] fileBytes = File.ReadAllBytes(filePath);

            // Search for "<End Doc>" in raw bytes — NO string/ASCII conversion
            byte[] marker = new byte[] { 0x3C, 0x45, 0x6E, 0x64, 0x20, 0x44, 0x6F, 0x63, 0x3E };
            // < E  n  d     D  o  c  >

            int markerIndex = IndexOfSequence(fileBytes, marker);

            byte[] trimmedBytes;
            if (markerIndex >= 0)
            {
                trimmedBytes = new byte[markerIndex];
                Buffer.BlockCopy(fileBytes, 0, trimmedBytes, 0, markerIndex);
            }
            else
            {
                trimmedBytes = fileBytes;
            }

            // Feed + cut appended AFTER trimmed content
            byte[] feedAndCut = { 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x00 };

            byte[] finalBytes = new byte[trimmedBytes.Length + feedAndCut.Length];
            Buffer.BlockCopy(trimmedBytes, 0, finalBytes, 0, trimmedBytes.Length);
            Buffer.BlockCopy(feedAndCut, 0, finalBytes, trimmedBytes.Length, feedAndCut.Length);

            if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
                return false;

            try
            {
                DOCINFOA docInfo = new DOCINFOA
                {
                    pDocName = "Raw Print Job",
                    pDatatype = "RAW"
                };

                if (StartDocPrinter(hPrinter, 1, docInfo) <= 0) return false;
                if (!StartPagePrinter(hPrinter)) return false;

                IntPtr unmanagedBytes = Marshal.AllocHGlobal(finalBytes.Length);
                Marshal.Copy(finalBytes, 0, unmanagedBytes, finalBytes.Length);

                bool success = WritePrinter(hPrinter, unmanagedBytes, finalBytes.Length, out _);

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

        private static int IndexOfSequence(byte[] source, byte[] pattern)
        {
            for (int i = 0; i <= source.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j]) { found = false; break; }
                }
                if (found) return i;
            }
            return -1;
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
