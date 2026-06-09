using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ReCap.Hub.Domain;
using ReCap.Hub.Infrastructure;

namespace ReCap.Hub.Services
{
    public sealed class HubConfigStore : IHubConfigStore
    {
        const string ROOT_EL = "hub";
        const string GAME_CONFIGS_EL = "gameConfigs";
        const string GAME_CONFIG_EL = "gameConfig";
        const string GAME_PATH_ATTR = "gameInstallPath";
        const string SAVES_PATH_ATTR = "savesPath";
        const string WINE_PFX_ATTR = "winePrefix";
        const string WINE_EX_ATTR = "wineExecutable";
        const string DISPLAY_NAME_ATTR = "displayName";
        const string SAVE_EL = "save";
        const string SAVE_ID_ATTR = "id";
        const string SAVE_LLT_ATTR = "lastLaunchTime";
        const string USER_PREFS_EL = "preferences";
        const string USER_DISPLAY_NAME_EL = "userDisplayName";
        const string USE_MANAGED_DECORATIONS_EL = "useManagedWindowDecorations";
        const string AUTO_CLOSE_SERVER_EL = "autoCloseServer";

        readonly IFileSystem _fs;
        readonly string _cfgDir;
        readonly string _cfgPath;

        public HubConfigStore(IFileSystem fs, string configDir, string configPath)
        {
            _fs = fs;
            _cfgDir = configDir;
            _cfgPath = configPath;
        }

        public HubConfig Load()
        {
            if (!_fs.FileExists(_cfgPath))
                return HubConfig.Default;

            string rawText = _fs.ReadAllText(_cfgPath);
            XDocument doc;
            try
            {
                doc = XDocument.Parse(rawText);
            }
            catch (System.Xml.XmlException)
            {
                BackUpCorruptFile(rawText);
                return HubConfig.Default;
            }

            return Parse(doc);
        }

        public void Save(HubConfig config)
        {
            var doc = Serialize(config);

            _fs.CreateDirectory(_cfgDir);

            string tempPath = _cfgPath + ".tmp";
            _fs.WriteAllText(tempPath, doc.ToString());   // whole-document write — no incremental Add
            _fs.Move(tempPath, _cfgPath, overwrite: true); // atomic replace
        }

        static XDocument Serialize(HubConfig config)
        {
            var gameConfigsEl = new XElement(GAME_CONFIGS_EL);
            foreach (var install in config.GameInstalls)
            {
                var el = new XElement(GAME_CONFIG_EL);
                el.SetAttributeValue(GAME_PATH_ATTR, install.GameInstallPath);
                el.SetAttributeValue(SAVES_PATH_ATTR, install.SavesPath);
                el.SetAttributeValue(WINE_PFX_ATTR, install.WinePrefix);     // null => attribute omitted
                el.SetAttributeValue(WINE_EX_ATTR, install.WineExecutable);  // null => attribute omitted
                el.SetAttributeValue(DISPLAY_NAME_ATTR, install.DisplayName);
                foreach (var save in install.Saves)
                {
                    var saveEl = new XElement(SAVE_EL);
                    saveEl.SetAttributeValue(SAVE_ID_ATTR, save.Id);
                    saveEl.SetAttributeValue(SAVE_LLT_ATTR,
                        save.LastLaunchTime.ToString(CultureInfo.InvariantCulture));
                    el.Add(saveEl);
                }
                gameConfigsEl.Add(el);
            }

            var prefsEl = new XElement(USER_PREFS_EL,
                new XElement(USER_DISPLAY_NAME_EL, config.UserDisplayName),
                new XElement(USE_MANAGED_DECORATIONS_EL, config.UseManagedDecorations),
                new XElement(AUTO_CLOSE_SERVER_EL, config.AutoCloseServer));

            return new XDocument(new XElement(ROOT_EL, gameConfigsEl, prefsEl));
        }

        HubConfig Parse(XDocument doc)
        {
            var root = doc.Root;
            var installs = new List<GameInstall>();

            var gameConfigsEl = root?.Element(GAME_CONFIGS_EL);
            if (gameConfigsEl != null)
            {
                foreach (var el in gameConfigsEl.Elements(GAME_CONFIG_EL))
                {
                    var saves = el.Elements(SAVE_EL)
                        .Where(s => s.Attribute(SAVE_ID_ATTR) != null)
                        .Select(s => new SaveRef
                        {
                            Id = s.Attribute(SAVE_ID_ATTR).Value,
                            LastLaunchTime = ParseDouble(s.Attribute(SAVE_LLT_ATTR)?.Value),
                        })
                        .ToList();

                    installs.Add(new GameInstall
                    {
                        GameInstallPath = el.Attribute(GAME_PATH_ATTR)?.Value ?? string.Empty,
                        SavesPath = el.Attribute(SAVES_PATH_ATTR)?.Value ?? string.Empty,
                        WinePrefix = el.Attribute(WINE_PFX_ATTR)?.Value,
                        WineExecutable = el.Attribute(WINE_EX_ATTR)?.Value,
                        DisplayName = el.Attribute(DISPLAY_NAME_ATTR)?.Value ?? string.Empty,
                        Saves = saves,
                    });
                }
            }

            var prefsEl = root?.Element(USER_PREFS_EL);
            string displayName = HubConfig.DefaultUserDisplayName;
            bool useManaged = HubConfig.Default.UseManagedDecorations;
            bool autoClose = HubConfig.Default.AutoCloseServer;
            if (prefsEl != null)
            {
                if (prefsEl.Element(USER_DISPLAY_NAME_EL) is XElement dn && !string.IsNullOrEmpty(dn.Value))
                    displayName = dn.Value;
                if (prefsEl.Element(USE_MANAGED_DECORATIONS_EL) is XElement md && bool.TryParse(md.Value, out var mdv))
                    useManaged = mdv;
                if (prefsEl.Element(AUTO_CLOSE_SERVER_EL) is XElement ac && bool.TryParse(ac.Value, out var acv))
                    autoClose = acv;
            }

            return new HubConfig
            {
                GameInstalls = installs,
                UserDisplayName = displayName,
                UseManagedDecorations = useManaged,
                AutoCloseServer = autoClose,
            };
        }

        static double ParseDouble(string s)
            => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : -1;

        void BackUpCorruptFile(string content)
        {
            try
            {
                _fs.WriteAllText(_cfgPath + ".corrupt.bak", content);
            }
            catch
            {
                // Backup is best-effort; never let it block recovery to defaults.
            }
        }
    }
}
