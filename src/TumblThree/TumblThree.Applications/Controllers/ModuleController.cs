﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Waf.Applications;
using System.Windows.Threading;

using TumblThree.Applications.Properties;
using TumblThree.Applications.Services;
using TumblThree.Applications.ViewModels;
using TumblThree.Domain;
using TumblThree.Domain.Queue;

namespace TumblThree.Applications.Controllers
{
    [Export(typeof(IModuleController))]
    [Export]
    internal class ModuleController : IModuleController
    {
        private const string AppSettingsFileName = "Settings.json";
        private const string ManagerSettingsFileName = "Manager.json";
        private const string QueueSettingsFileName = "Queuelist.json";
        private const string CookiesFileName = "Cookies.json";

        private IHttpRequestFactory _httpRequestFactory { get; }
        private readonly ISharedCookieService _cookieService;
        private readonly IEnvironmentService _environmentService;
        private readonly Lazy<ShellService> _shellService;

        private readonly Lazy<CrawlerController> _crawlerController;
        private readonly Lazy<DetailsController> _detailsController;
        private readonly Lazy<ManagerController> _managerController;
        private readonly Lazy<QueueController> _queueController;

        private readonly QueueManager _queueManager;
        private readonly ISettingsProvider _settingsProvider;
        private readonly IConfirmTumblrPrivacyConsent _confirmTumblrPrivacyConsent;

        private readonly Lazy<ShellViewModel> _shellViewModel;

        private AppSettings _appSettings;
        private ManagerSettings _managerSettings;
        private QueueSettings _queueSettings;
        private List<Cookie> _cookieList;

        [ImportingConstructor]
        public ModuleController(
            Lazy<ShellService> shellService,
            IEnvironmentService environmentService,
            IConfirmTumblrPrivacyConsent confirmTumblrPrivacyConsent,
            ISettingsProvider settingsProvider,
            ISharedCookieService cookieService,
            IHttpRequestFactory httpRequestFactory,
            Lazy<ManagerController> managerController,
            Lazy<QueueController> queueController,
            Lazy<DetailsController> detailsController,
            Lazy<CrawlerController> crawlerController,
            Lazy<ShellViewModel> shellViewModel)
        {
            _shellService = shellService;
            _environmentService = environmentService;
            _confirmTumblrPrivacyConsent = confirmTumblrPrivacyConsent;
            _settingsProvider = settingsProvider;
            _cookieService = cookieService;
            _httpRequestFactory = httpRequestFactory;
            _detailsController = detailsController;
            _managerController = managerController;
            _queueController = queueController;
            _crawlerController = crawlerController;
            _shellViewModel = shellViewModel;
            _queueManager = new QueueManager();
        }

        private ShellService ShellService => _shellService.Value;

        private ManagerController ManagerController => _managerController.Value;

        private QueueController QueueController => _queueController.Value;

        private DetailsController DetailsController => _detailsController.Value;

        private CrawlerController CrawlerController => _crawlerController.Value;

        private ShellViewModel ShellViewModel => _shellViewModel.Value;

        public void Initialize()
        {
            string savePath = _environmentService.AppSettingsPath;
            if (CheckIfPortableMode(AppSettingsFileName))
            {
                savePath = AppDomain.CurrentDomain.BaseDirectory;
            }

            _appSettings = LoadSettings<AppSettings>(Path.Combine(savePath, AppSettingsFileName));
            _queueSettings = LoadSettings<QueueSettings>(Path.Combine(savePath, QueueSettingsFileName));
            _managerSettings = LoadSettings<ManagerSettings>(Path.Combine(savePath, ManagerSettingsFileName));
            _cookieList = LoadSettings<List<Cookie>>(Path.Combine(savePath, CookiesFileName));

            ShellService.Settings = _appSettings;
            ShellService.ShowErrorAction = ShellViewModel.ShowError;
            ShellService.ShowDetailsViewAction = ShowDetailsView;
            ShellService.ShowQueueViewAction = ShowQueueView;
            ShellService.UpdateDetailsViewAction = UpdateDetailsView;
            ShellService.SettingsUpdatedHandler += OnSettingsUpdated;
            ShellService.InitializeOAuthManager();

            ManagerController.QueueManager = _queueManager;
            ManagerController.ManagerSettings = _managerSettings;
            ManagerController.BlogManagerFinishedLoadingLibrary += OnBlogManagerFinishedLoadingLibrary;
            QueueController.QueueSettings = _queueSettings;
            QueueController.QueueManager = _queueManager;
            DetailsController.QueueManager = _queueManager;
            CrawlerController.QueueManager = _queueManager;

            Task managerControllerInit = ManagerController.InitializeAsync();
            QueueController.Initialize();
            DetailsController.Initialize();
            CrawlerController.Initialize();
            _cookieService.SetUriCookie(_cookieList);
        }

        public async void Run()
        {
            ShellViewModel.IsQueueViewVisible = true;
            ShellViewModel.Show();

            // Let the UI to initialize first before loading the queuelist.
            await Dispatcher.CurrentDispatcher.InvokeAsync(ManagerController.RestoreColumn, DispatcherPriority.ApplicationIdle);
            await Dispatcher.CurrentDispatcher.InvokeAsync(QueueController.Run, DispatcherPriority.ApplicationIdle);
            await _confirmTumblrPrivacyConsent.ConfirmPrivacyConsentAsync();
        }

        public void Shutdown()
        {
            DetailsController.Shutdown();
            QueueController.Shutdown();
            ManagerController.Shutdown();
            CrawlerController.Shutdown();

            SaveSettings();
        }

        private void SaveSettings()
        {
            string savePath = _environmentService.AppSettingsPath;
            if (_appSettings.PortableMode)
            {
                savePath = AppDomain.CurrentDomain.BaseDirectory;
            }

            SaveSettings(Path.Combine(savePath, AppSettingsFileName), _appSettings);
            SaveSettings(Path.Combine(savePath, QueueSettingsFileName), _queueSettings);
            SaveSettings(Path.Combine(savePath, ManagerSettingsFileName), _managerSettings);
            SaveSettings(Path.Combine(savePath, CookiesFileName), new List<Cookie>(_cookieService.GetAllCookies(_cookieService.CookieContainer)));
        }

        private void OnSettingsUpdated(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void OnBlogManagerFinishedLoadingLibrary(object sender, EventArgs e)
        {
            QueueController.LoadQueue();
        }

        private static bool CheckIfPortableMode(string fileName)
        {
            return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName));
        }

        private T LoadSettings<T>(string fileName) where T : class, new()
        {
            try
            {
                return _settingsProvider.LoadSettings<T>(fileName);
            }
            catch (Exception ex)
            {
                Logger.Error("Could not read the settings file: {0}", ex);
                return new T();
            }
        }

        private void SaveSettings(string fileName, object settings)
        {
            try
            {
                _settingsProvider.SaveSettings(fileName, settings);
            }
            catch (Exception ex)
            {
                Logger.Error("Could not save the settings file: {0}", ex);
            }
        }

        private void ShowDetailsView()
        {
            ShellViewModel.IsDetailsViewVisible = true;
        }

        private void ShowQueueView()
        {
            ShellViewModel.IsQueueViewVisible = true;
        }

        private void UpdateDetailsView()
        {
            if (!ShellViewModel.IsQueueViewVisible)
            {
                ShellViewModel.IsDetailsViewVisible = true;
            }
        }
    }
}
