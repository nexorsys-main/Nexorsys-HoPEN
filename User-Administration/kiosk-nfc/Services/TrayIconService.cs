using System;
using System.Drawing;

namespace Nexorsys.NFC.APP.Services
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
            
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;

            _notifyIcon.Text = "NexorSys Kiosk - En écoute...";
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
