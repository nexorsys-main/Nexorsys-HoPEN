using System;
using System.Drawing;
using System.Reflection;

namespace Pinede.NFC.APP.Services
{
    public class TrayIconService : ITrayIconService, IDisposable
    {
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _disposed;

        public event EventHandler? RestaurerDemande;

        public void Initialiser()
        {
            if (_notifyIcon != null) return;

            _notifyIcon = new System.Windows.Forms.NotifyIcon();

            // On tente de charger l'icône de l'application
            try
            {
                var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app_icon.ico")).Stream;
                _notifyIcon.Icon = new System.Drawing.Icon(iconStream);
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.Text = "Pinede NFC Kiosque - En écoute...";
            _notifyIcon.Visible = true;

            // Menu contextuel
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Ouvrir le Kiosque", null, (s, e) => RestaurerDemande?.Invoke(this, EventArgs.Empty));
            contextMenu.Items.Add("-"); // Séparateur
            contextMenu.Items.Add("Quitter", null, (s, e) => System.Windows.Application.Current.Shutdown());

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Double clic sur l'icône pour restaurer
            _notifyIcon.DoubleClick += (s, e) => RestaurerDemande?.Invoke(this, EventArgs.Empty);
        }

        public void AfficherNotification(string titre, string message)
        {
            _notifyIcon?.ShowBalloonTip(3000, titre, message, System.Windows.Forms.ToolTipIcon.Info);
        }

        public void Nettoyer()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Nettoyer();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
