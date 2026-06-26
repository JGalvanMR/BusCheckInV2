using BusCheckInV2.Services;
using Microsoft.Maui.Controls;
using System;
using System.IO;

namespace BusCheckInV2
{
    public partial class App : Application
    {
        private readonly ISQLiteService _databaseService;

        public App(ISQLiteService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;

            MainPage = new AppShell();  // ← sin parámetros
        }

        protected override async void OnStart()
        {
            base.OnStart();
            await _databaseService.InitializeAsync();
        }
    }
}