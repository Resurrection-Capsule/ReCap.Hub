using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using ReCap.Hub.Composition;
using ReCap.Hub.Domain;
using ReCap.Hub.Services;
using ReCap.Hub.ViewModels;

namespace ReCap.Hub.Data
{
    /// <summary>
    /// TRANSITIONAL facade (Step 1). Persistence is delegated to the injected
    /// <see cref="IHubConfigStore"/>; this type only maps <see cref="HubConfig"/> to/from
    /// ViewModels and preserves the existing <c>HubData.Instance</c> surface. Slated for
    /// deletion in Step 2 once LocalPlay/Preferences VMs map config to VMs directly.
    /// </summary>
    public class HubData : ViewModelBase
    {
        public static readonly HubData Instance = new HubData(HubServices.Get<IHubConfigStore>());

        readonly IHubConfigStore _store;

        ObservableCollection<GameConfigViewModel> _gameConfigs = new ObservableCollection<GameConfigViewModel>();
        public ObservableCollection<GameConfigViewModel> GameConfigs
        {
            get => _gameConfigs;
            protected set => RASIC(ref _gameConfigs, value);
        }

        string _userDisplayName = HubConfig.DefaultUserDisplayName;
        public string UserDisplayName
        {
            get => _userDisplayName;
            set => RASIC(ref _userDisplayName, value);
        }

        bool _useManagedDecorations = HubConfig.Default.UseManagedDecorations;
        public bool UseManagedDecorations
        {
            get => _useManagedDecorations;
            set => RASIC(ref _useManagedDecorations, value);
        }

        bool _autoCloseServer = true;
        public bool AutoCloseServer
        {
            get => _autoCloseServer;
            set => RASIC(ref _autoCloseServer, value);
        }

        HubData(IHubConfigStore store)
        {
            _store = store;
            Load();
            GameConfigs.CollectionChanged += GameConfigs_CollectionChanged;
        }

        void Load()
        {
            var cfg = _store.Load();
            UserDisplayName = cfg.UserDisplayName;
            UseManagedDecorations = cfg.UseManagedDecorations;
            AutoCloseServer = cfg.AutoCloseServer;

            foreach (var install in cfg.GameInstalls)
                GameConfigs.Add(new GameConfigViewModel(install));
        }

        void GameConfigs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => Save();

        public void Save()
        {
            var cfg = new HubConfig
            {
                UserDisplayName = UserDisplayName,
                UseManagedDecorations = UseManagedDecorations,
                AutoCloseServer = AutoCloseServer,
                GameInstalls = GameConfigs
                    .OrderBy(x => x.LastLaunchTime)
                    .Select(x => x.ToGameInstall())
                    .ToList(),
            };
            _store.Save(cfg);
        }
    }
}
