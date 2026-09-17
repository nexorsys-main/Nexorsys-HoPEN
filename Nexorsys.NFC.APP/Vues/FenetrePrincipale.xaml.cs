using System;
using System.Windows;
using Pinede.NFC.APP.VueModeles;

namespace Pinede.NFC.APP.Vues
{
    public partial class FenetrePrincipale : Window
    {
        public bool IsWelcomeMode { get; set; }
        public bool IsKioskMode { get; set; }

        private IntPtr _hookId = IntPtr.Zero;
        private NativeMethods.LowLevelKeyboardProc _proc;

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
            {
                var hookStruct = (NativeMethods.KBDLLHOOKSTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(NativeMethods.KBDLLHOOKSTRUCT));
                bool alt = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Alt) != 0;
                bool ctrl = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Control) != 0;

                // LWin, RWin, Alt+Tab, Alt+Esc, Ctrl+Esc
                if (hookStruct.vkCode == 0x5B || hookStruct.vkCode == 0x5C ||
                    (hookStruct.vkCode == 0x09 && alt) ||
                    (hookStruct.vkCode == 0x1B && alt) ||
                    (hookStruct.vkCode == 0x1B && ctrl))
                {
                    return (IntPtr)1; // Block
                }
            }
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public FenetrePrincipale(FenetrePrincipaleVueModele vueModele)
        {
            InitializeComponent();
            DataContext = vueModele;
            _proc = HookCallback;
        }

        public void VerrouillerEcran()
        {
            Dispatcher.Invoke(() =>
            {
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.WindowState = WindowState.Maximized;
                this.Topmost = true;

                if (_hookId == IntPtr.Zero)
                {
                    using (System.Diagnostics.Process curProcess = System.Diagnostics.Process.GetCurrentProcess())
                    using (System.Diagnostics.ProcessModule curModule = curProcess.MainModule)
                    {
                        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc,
                            NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
                    }
                }
            });
        }

        public void DeverrouillerEcranEnPanneau()
        {
            Dispatcher.Invoke(() =>
            {
                if (_hookId != IntPtr.Zero)
                {
                    NativeMethods.UnhookWindowsHookEx(_hookId);
                    _hookId = IntPtr.Zero;
                }

                this.WindowState = WindowState.Normal;
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;

                var workArea = SystemParameters.WorkArea;
                this.Width = 350;
                this.Height = workArea.Height;
                this.Top = workArea.Top;
                this.Left = workArea.Right - 350;

                this.Topmost = true; // Always on top as a panel
            });
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && !IsWelcomeMode)
            {
                this.Hide();
            }
            base.OnStateChanged(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (IsWelcomeMode)
            {
                // In welcome mode, prevent closing entirely to ensure panel remains visible
                e.Cancel = true;
                return;
            }

            // Instead of closing the application entirely, we hide it to the system tray.
            // The user must right-click the tray icon and select "Quitter" to actually close it.
            e.Cancel = true;
            this.Hide();
            base.OnClosing(e);
        }
    }
}
