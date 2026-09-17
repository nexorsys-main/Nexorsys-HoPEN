using System;
using System.Windows;
using System.Windows.Threading;

namespace Pinede.NFC.APP.Vues
{
    public partial class EcranDiagnostic : Window
    {
        private readonly DispatcherTimer _timer;
        private int _elapsedTime = 0;
        private const int TotalTime = 6000; // 6 seconds
        private const int Interval = 100;

        public EcranDiagnostic(string nomUtilisateur, string logiciels)
        {
            InitializeComponent();

            TxtUtilisateur.Text = $"Utilisateur : {nomUtilisateur}";
            TxtLogiciels.Text = logiciels;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(Interval);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _elapsedTime += Interval;
            ProgressTimer.Value = _elapsedTime;

            if (_elapsedTime >= TotalTime)
            {
                _timer.Stop();
                this.Close();
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            this.Close();
        }
    }
}
