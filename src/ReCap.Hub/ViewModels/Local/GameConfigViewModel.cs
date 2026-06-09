using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using ReactiveUI;
using ReCap.Hub.Data;

namespace ReCap.Hub.ViewModels
{
    public class GameConfigViewModel : ViewModelBase
    {
        string _title = string.Empty;
        public string Title
        {
            get => _title;
            set => RASIC(ref _title, value);
        }

        public ReactiveCommand<SaveGameViewModel, Unit> PlayGameWithSaveCommand { get; }
        public ReactiveCommand<Unit, Unit> NewSaveGameCommand { get; }
        public ReactiveCommand<SaveGameViewModel, Unit> DeleteSaveGameCommand { get; }
        public ReactiveCommand<SaveGameViewModel, Unit> RenameSaveGameCommand { get; }

        double _lastLaunchTime = -1;
        public double LastLaunchTime
        {
            get => _lastLaunchTime;
        }


        ObservableCollection<SaveGameViewModel> _saves = new ObservableCollection<SaveGameViewModel>();
        public ObservableCollection<SaveGameViewModel> Saves
        {
            get => _saves;
            set
            {
                if (_saves != null)
                    _saves.CollectionChanged -= Saves_CollectionChanged;
                RASIC(ref _saves, value);

                if (value != null)
                    value.CollectionChanged += Saves_CollectionChanged;
            }
        }

        protected void Saves_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (SaveGameViewModel save in e.NewItems)
                {
                    //save.EnableFSWatcher = true;
                }
            }

            if (e.OldItems != null)
            {
                foreach (SaveGameViewModel save in e.OldItems)
                {
                    //save.EnableFSWatcher = false;
                }
            }
        }


        SaveGameViewModel _selectedSave = null;
        public SaveGameViewModel SelectedSave
        {
            get => _selectedSave;
            set => RASIC(ref _selectedSave, value);
        }


        string _gameInstallPath = string.Empty;
        public string GameInstallPath
        {
            get => _gameInstallPath;
            set => RASIC(ref _gameInstallPath, value);
        }

        string _savesPath = string.Empty;
        public string SavesPath
        {
            get => _savesPath;
            set => RASIC(ref _savesPath, value);
        }


        string _winePrefixPath = null;
        public string WinePrefixPath
        {
            get => _winePrefixPath;
            set => RASIC(ref _winePrefixPath, value);
        }


        string _wineExecPath = null;
        public string WineExecPath
        {
            get => _wineExecPath;
            set => RASIC(ref _wineExecPath, value);
        }

        public GameConfigViewModel(string gameInstallPath, string wineExecutable, string winePrefix) //, string savesPath)
            : this()
        {
            GameInstallPath = gameInstallPath;
            WinePrefixPath = winePrefix;
            WineExecPath = wineExecutable;
            SavesPath = HubGlobalPaths.ServerAccountsDir; //savesPath;
        }

        public GameConfigViewModel(ReCap.Hub.Domain.GameInstall install)
            : this(install.GameInstallPath, install.WineExecutable, install.WinePrefix)
        {
            Title = install.DisplayName ?? string.Empty;

            if (Directory.Exists(SavesPath))
            {
                foreach (var saveRef in install.Saves)
                {
                    if (string.IsNullOrEmpty(saveRef.Id))
                        continue;
                    string saveXmlPath = Path.Combine(SavesPath, saveRef.Id + ".xml");
                    if (!File.Exists(saveXmlPath))
                        continue;
                    Saves.Add(new SaveGameViewModel(saveXmlPath));
                }
            }

            if (TimeHelper.TryGetNewest(Saves, s => s.LastLaunchTime, out SaveGameViewModel lastPlayed))
                SelectedSave = lastPlayed;
        }

        private GameConfigViewModel()
        : base()
        {
            /*LocalServer.InstanceCreated += LocalServer_InstanceCreated;
            try
            {
                
            }
            catch (NullReferenceException ex)
            {
                
            }*/
            EnsureServerExitedHandler(LocalServer.Instance);

            PlayGameWithSaveCommand = ReactiveCommand.CreateFromTask<SaveGameViewModel>(PlayGameWithSave);
            NewSaveGameCommand = ReactiveCommand.CreateFromTask(async () => { await CreateSaveGame(true); });
            DeleteSaveGameCommand = ReactiveCommand.CreateFromTask<SaveGameViewModel>(DeleteSaveGame);
            RenameSaveGameCommand = ReactiveCommand.CreateFromTask<SaveGameViewModel>(RenameSaveGame);

            Observable.Merge(
                    PlayGameWithSaveCommand.ThrownExceptions,
                    NewSaveGameCommand.ThrownExceptions,
                    DeleteSaveGameCommand.ThrownExceptions,
                    RenameSaveGameCommand.ThrownExceptions)
                .Subscribe(ex => Debug.WriteLine($"Command failed: {ex}"));
        }

        /*private void LocalServer_InstanceCreated(object sender, EventArgs e)
        {
            
            LocalServer.InstanceCreated -= LocalServer_InstanceCreated;
        }*/
        void EnsureServerExitedHandler(LocalServer server)
        {
            LocalServer.ServerExited += (s, e) =>
            {
                /*foreach (var save in Saves)
                {
                    save.ReadFromXml();
                }*/
            };
        }

        public async Task DeleteSaveGame(SaveGameViewModel saveGame)
        {
            if (!Saves.Contains(saveGame))
                return;
            
            if (await DialogDisplay.ShowDialog(new YesNoDialogViewModel("Delete save game", "Are you sure you want to DELETE this save game?")))
            {
                Saves.Remove(saveGame);
                saveGame.Delete();
                HubData.Instance.Save();
            }
        }

        public async Task RenameSaveGame(SaveGameViewModel saveGame)
        {
            if (!Saves.Contains(saveGame))
                return;
            
            string oldTitle = saveGame.Title;
            string newTitle = await DialogDisplay.ShowDialog(new TextBoxDialogViewModel("Rename save game", string.Empty, oldTitle, true));
            if ((newTitle != null) && (newTitle != oldTitle) && (!(string.IsNullOrEmpty(newTitle) || string.IsNullOrWhiteSpace(newTitle))))
            {
                saveGame.Rename(newTitle);
                HubData.Instance.Save();
            }
        }

        public async Task<SaveGameViewModel> CreateSaveGame(bool isCloseable = true)
        {
            var saveGame = await DialogDisplay.ShowDialog(new NewSaveGameViewModel(SavesPath, isCloseable));
            if (saveGame != null)
            {
                Saves.Add(saveGame);
                SelectedSave = saveGame;
                HubData.Instance.Save();
            }
            return saveGame;
        }

        public async Task PlayGameWithSave(SaveGameViewModel save)
        {
            double now = DateTime.UtcNow.ToUniversalTime().Subtract(DateTime.UnixEpoch.ToUniversalTime()).TotalMilliseconds;
            
            
            string installPath = GameInstallPath;
            bool installPathOK = IsPathOK(installPath);
            Debug.WriteLine($"installPath: '{installPath}' {installPathOK}");

            bool useWine = !OperatingSystem.IsWindows();
            bool wineExecPathOK = useWine ? IsPathOK(WineExecPath, false) : true;
            Debug.WriteLine($"wineExecPath: '{WineExecPath}' {wineExecPathOK}");
            bool winePrefixPathOK = useWine ? IsPathOK(WinePrefixPath) : true;
            Debug.WriteLine($"winePrefixPath: '{WinePrefixPath}' {winePrefixPathOK}");
            if (!(
                installPathOK
                && wineExecPathOK
                && winePrefixPathOK
            ))
            {
                var task = DialogDisplay.ShowDialog(new LocateDarksporeViewModel());
                var paths = await task;
                if (task.IsFaulted || (task.Exception != null))
                    throw task.Exception;
                
                if (!installPathOK)
                {
                    installPath = paths.DarksporeInstallPath;
                    GameInstallPath = installPath;
                }
                
                if (!wineExecPathOK)
                    WineExecPath = paths.WineExecutable;
                
                if (!winePrefixPathOK)
                    WinePrefixPath = paths.WinePrefix;
                
                HubData.Instance.Save();
            }

            string appDataDir = useWine
                ? WineHelper.GetWINEEnvironmentVariable(WinePrefixPath, WineExecPath, "appdata")
                : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            ;
            string prefsDirPath = Path.Combine(appDataDir, "DarksporeData", "Preferences");

            if (!Directory.Exists(prefsDirPath))
                Directory.CreateDirectory(prefsDirPath);
            
            string loginPropPath = Path.Combine(prefsDirPath, "login.prop");
            string loginPropText = $"UserName \"{save.EmailAddress}\"\n";
            Debug.WriteLine($"Launching Darkspore for save: '{save.Title}'");
            File.WriteAllText(loginPropPath, loginPropText);
            //Debug.WriteLine($"path: '{loginPropPath}'\n\ntext:\n{loginPropText}");

            string gameBinPath = Path.Combine(GameInstallPath, "DarksporeBin");
            string patchedExeName = "Darkspore_ReCapPatched.exe";
            string gameExePath = Path.Combine(gameBinPath, patchedExeName);

            string gameDataPath = Path.Combine(GameInstallPath, "Data");
            string autoLoginPackageDestPath = Path.Combine(gameDataPath, Patcher.AUTO_LOGIN_PACKAGE_NAME);
            bool exeMissing = !File.Exists(gameExePath);
            bool autoLoginPackageMissing = !File.Exists(autoLoginPackageDestPath);

            string gameOriginalExePath = Path.Combine(gameBinPath, "Darkspore.exe");

            save.UpdateUserDisplayName(HubData.Instance.UserDisplayName);

            var session = Composition.HubServices.Get<Services.IGameSession>();
            var result = await session.PlayAsync(new Services.GameSessionRequest
            {
                GameExePath = gameExePath,
                GameOriginalExePath = gameOriginalExePath,
                GameBinDir = gameBinPath,
                AutoLoginPackageDestPath = autoLoginPackageDestPath,
                WinePrefix = WinePrefixPath,
                WineExecutable = WineExecPath,
                ExeMissing = exeMissing,
                AutoLoginPackageMissing = autoLoginPackageMissing,
                AutoCloseServer = HubData.Instance.AutoCloseServer,
            }, CancellationToken.None);
            if (!result.Success)
            {
                string message = result.Error?.Message ?? "The game failed to launch.";
                await DialogDisplay.ShowDialog(new OkDialogViewModel("Launch failed", message, true));
                return; // Hub stays alive so the user can see the error — no kill on failure.
            }

            Process.GetCurrentProcess().Kill(); //HACK  (success path unchanged; removed in full Step 2b)
            ApplyPostGameSessionState(save, now);
        }

        private void ApplyPostGameSessionState(SaveGameViewModel save, double now)
        {
            save.ReadFromXml(true);
            HubData.Instance.GameConfigs.Remove(this);
            HubData.Instance.GameConfigs.Insert(0, this);
            //TODO: HubData.Instance.SelectedGameConfig = this;
            _lastLaunchTime = now;
            save.LastLaunchTime = now;
            //save.ReadFromXml()

            Saves.Remove(save);
            Saves.Insert(0, save);
            SelectedSave = save;
            HubData.Instance.Save();
        }

        static bool IsPathOK(string path, bool isDirectory = true)
        {
            return
                (!string.IsNullOrEmpty(path))
                && (!string.IsNullOrWhiteSpace(path))
                && (
                    isDirectory
                        ? Directory.Exists(path)
                        : File.Exists(path)
                )
            ;
        }

        public ReCap.Hub.Domain.GameInstall ToGameInstall()
            => new ReCap.Hub.Domain.GameInstall
            {
                GameInstallPath = GameInstallPath,
                SavesPath = SavesPath,
                WinePrefix = WinePrefixPath,
                WineExecutable = WineExecPath,
                DisplayName = Title,
                Saves = Saves.OrderBy(x => x.LastLaunchTime).Select(s => s.ToSaveRef()).ToList(),
            };
    }
}
