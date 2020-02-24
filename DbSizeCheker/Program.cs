using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace DbSizeCheker
{
    class Program
    {
        private static GoogleService googleService;
        private static PostgreSqlService postgreSqlService;
        private static List<ConnectionStringSettings> connectionStrings;
        private static Timer timer;
        private static Dictionary<string, List<DbSize>> serverInfoCollection;

        static void Main(string[] args)
        {
            Console.WriteLine("Проверка  файла конфигурации...");

            if(Configurator.IsConfigurationCorrect())
            {
                postgreSqlService = new PostgreSqlService();
                connectionStrings = Configurator.ConnectionStrings;
                serverInfoCollection = new Dictionary<string, List<DbSize>>();

                Console.WriteLine("Проверка подключения к сервису \"Google SpreadSheets\".\n\r");
                prepareGoogleTables();

                timer = new Timer(TimeSpan.FromSeconds(Configurator.TimerInterval).TotalMilliseconds);
                timer.AutoReset = true;
                timer.Elapsed += updateData;
                timer.Enabled = true;

                Console.WriteLine("\n\rПрограмма запущена. Для выхода из программы нажмите клавишу \"Q\"\n\r");
                //Немедленный запуск метода
                updateData(null, null);
            }
            else
            {
                Console.WriteLine("\n\rВ файле конфигурации не найдена информация о базе данных. Программа остановлена. ");
            }

            while (Console.ReadKey().Key != ConsoleKey.Q)
            {
                
            }
        }

        private static void updateData(object sender, ElapsedEventArgs e)
        {
            extractDbInfo();
            googleService.UpdateSpreadSheet(serverInfoCollection);
            Console.WriteLine("Таблица обновлена.\n\r");
        }

        private static void extractDbInfo()
        {
            serverInfoCollection.Clear();

            Parallel.ForEach(connectionStrings, cs => {
                Console.WriteLine($"Извлечение данных с сервера \"{cs.Name}\"...");
                serverInfoCollection.Add(cs.Name, postgreSqlService.GetDbsSize(cs.ConnectionString));
            });

            countFreeDiskSpace();

            Console.WriteLine("Данные успешно извлечены.");
        }

        private static void countFreeDiskSpace()
        {
            foreach(var info in serverInfoCollection)
            {
                int diskSize = Configurator.GetDiskSize(info.Key);
                var targetRow = info.Value.First(v => v.DataBaseName.ToLower().Contains("свободно"));

                if ( diskSize > targetRow.DataBaseSize)
                {                   
                    targetRow.DataBaseSize =  diskSize - targetRow.DataBaseSize;
                }
                else
                {
                    targetRow.DataBaseSize = 0;
                }
            }
        }

        // Авторизация в гугл-таблицах и подготовка таблицы
        static void prepareGoogleTables()
        {
            googleService = GoogleService.LogIn(Configurator.GoogleLogin);
            Configurator.GoogleTableId = googleService.SetTable(Configurator.GoogleTableId, Configurator.ServerNames);
        }

    }
}
