using MusicNotesEditor.LocalServer;
using QRCoder;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicNotesEditor.Views
{
    public partial class QrConnectPage : Page
    {

        private readonly CertAndServer _server;
        private string? _currentRequestId;
        private CancellationTokenSource _cts = new();

        public QrConnectPage(string jsonPayload, CertAndServer server)
        {
            InitializeComponent();
            _server = server;
            QrImg.Source = GenerateQr(jsonPayload);

            _ = StartPollingPending();
        }



        private BitmapImage GenerateQr(string text)
        {
            var gen = new QRCodeGenerator();
            var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var qr = new PngByteQRCode(data);
            var bytes = qr.GetGraphic(20);

            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = new MemoryStream(bytes);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }

        private async Task StartPollingPending()
        {
            var http = new HttpClient();

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var list = _server.GetPending();

                    var req = list?.FirstOrDefault();

                    if (req != null)
                    {
                        _currentRequestId = req.Key;
                        DeviceNameText.Text = $"Device '{req.DeviceName}' wants to connect";

                        StepsPanel.Visibility = Visibility.Collapsed;
                        RequestPanel.Visibility = Visibility.Visible;
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Polling error: " + ex);
                }

                await Task.Delay(1000);
            }
        }


        private async void Approve_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRequestId == null) return;

            _server.ApproveDevice(_currentRequestId);

            RestoreState();
        }

        private async void Deny_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRequestId == null) return;

            _server.DenyDevice(_currentRequestId);

            RestoreState();
        }

        private void RestoreState()
        {
            StepsPanel.Visibility = Visibility.Visible;
            RequestPanel.Visibility = Visibility.Collapsed;
            _currentRequestId = null;
        }


        private class PendingRequest
        {
            public string Key { get; set; }
            public DateTime Time { get; set; }
            public string DeviceName { get; set; }
        }

    }

}
