using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace DbSizeCheker
{
    class Configurator
    {
        private readonly static List<ConnectionStringSettings> _connectionStrings;

        static Configurator()
        {
            var collection = ConfigurationManager.ConnectionStrings;
            _connectionStrings = new List<ConnectionStringSettings>();
            for (int i = 0; i < collection.Count; i++)
            {
                _connectionStrings.Add(collection[i]);
            }
        }

        //Возвращаем копию коллекции, для ограничения изменения содержимого внутренней коллекции извне
        public static List<ConnectionStringSettings> ConnectionStrings { get => new List<ConnectionStringSettings>(_connectionStrings); }
        public static List<string> ServerNames { get => _connectionStrings.Select(cs => cs.Name).ToList(); }

        public static string GoogleLogin { get => ConfigurationManager.AppSettings ["GoogleUser"] ?? "default"; }

        public static string GoogleTableId
        {
            get => ConfigurationManager.AppSettings["GoogleTableId"] ?? "";

            set
            {
                updateAppSettings("GoogleTableId", value);
            }
        }

        public static int TimerInterval
        {
            get 
            {
                if (int.TryParse(ConfigurationManager.AppSettings.Get("TimerIn"), out int seconds))
                    return seconds;
                else return 30;

            }
        }

        public static bool IsConfigurationCorrect()
        {
            return _connectionStrings.Count > 0;
        }

        public static int GetDiskSize(string serverName)
        {
            if (int.TryParse(ConfigurationManager.AppSettings.Get(serverName), out int result))
                return result;
            else
                return -1;

        }

        private static void updateAppSettings(string key, string value)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = config.AppSettings.Settings;
            if (settings[key] == null)
            {
                settings.Add(key, value);
            }
            else
            {
                settings[key].Value = value;
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);
        }

    }
}
