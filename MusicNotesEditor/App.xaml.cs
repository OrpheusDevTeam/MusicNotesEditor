using Microsoft.Extensions.Configuration;
using MusicNotesEditor.Models.Config;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Data;
using System.Windows;

namespace MusicNotesEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static AppSettings Settings { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureApp();
        }


        private void ConfigureApp()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            Settings = configuration.GetSection("AppSettings").Get<AppSettings>();

            if (Settings == null)
                throw new InvalidOperationException("Failed to load 'AppSettings' section from appsettings.json.");

            var context = new ValidationContext(Settings);
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(Settings, context, results, validateAllProperties: true))
            {
                var messages = string.Join(", ", results.Select(r => r.ErrorMessage));
                throw new InvalidOperationException($"Invalid AppSettings configuration: {messages}");
            }

            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(Settings));
        }
    }

}
