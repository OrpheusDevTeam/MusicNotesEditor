using QRCoder;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MusicNotesEditor.Views
{
    public partial class QrConnectWindow : Window
    {
        public QrConnectWindow(string jsonPayload)
        {
            InitializeComponent();
            QrImg.Source = GenerateQr(jsonPayload);
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
    }
}
