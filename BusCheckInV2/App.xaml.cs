using BusCheckInV2.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.IO;

namespace BusCheckInV2
{
    public partial class App : Application
    {
        private readonly ISQLiteService _databaseService;
        private readonly IAppUpdateService _appUpdateService;

        public App(ISQLiteService databaseService, IAppUpdateService appUpdateService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _appUpdateService = appUpdateService;

            MainPage = new AppShell();
        }
        protected override async void OnStart()
        {
            base.OnStart();

            // Inicializa el database
            //var dbPath = Path.Combine(FileSystem.AppDataDirectory, "BusCheckInV2.db3");
            await _databaseService.InitializeAsync();
        }

    }
}