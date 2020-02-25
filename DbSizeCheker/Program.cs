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

                // Таймер запускается через каждый 30 секунд, либо через промежуток времени определенный в файле конфигурации
                timer = new Timer(TimeSpan.FromSeconds(Configurator.TimerInterval).TotalMilliseconds);
                timer.AutoReset = true;
                timer.Elapsed += updateData;
                timer.Enabled = true;

                Console.WriteLine($"\n\rПрограмма запущена. Интервал обновления данных - {Configurator.TimerInterval} секунд. Для выхода из программы нажмите клавишу \"Q\"\n\r");

                // Первый запуск метода вручную, так как таймер сработает только через указанный интервал
                updateData(null, null);


                //Закрытие программы по клавише
                while (Console.ReadKey().Key != ConsoleKey.Q)
                {
                    Console.WriteLine("\n\rДля завершения работы программы нажмите на клавишу \"Q\"\n\r");
                }

                timer.Stop();
                timer.Dispose();
            }
            else
            {
                Console.WriteLine("\n\rВ файле конфигурации не найдена информация о базе данных. Программа остановлена. Нажмите Enter для выхода.");
                Console.ReadLine();
            }

        }

        // Метод вызывается по событию timer.Elapsed
        private static void updateData(object sender, ElapsedEventArgs e)
        {
            if (extractDbInfo())
            {
                Console.WriteLine("Данные успешно извлечены.");

                googleService.UpdateSpreadSheet(serverInfoCollection);

                Console.WriteLine("Таблица обновлена.\n\r");
            }
            else
            {
                Console.WriteLine($"Не удалось подключиться ни к одному из серверов баз данных. Повторная попытка через {Configurator.TimerInterval} секунд.\n\r");
            }
        }

        // Извлекает данные с сервера, если успешно - возвращает true
        private static bool extractDbInfo()
        {
            serverInfoCollection.Clear();

            foreach (var cs in connectionStrings)
            {
                Console.WriteLine($"Извлечение данных с сервера \"{cs.Name}\"...");

                List<DbSize> dbInfo;

                try
                {
                    dbInfo = postgreSqlService.GetDbsSize(cs.ConnectionString);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при подключения к серверу \"{cs.Name}\". Проверьте корректность строки подключения.");
                    continue;
                }

                serverInfoCollection.Add(cs.Name, postgreSqlService.GetDbsSize(cs.ConnectionString));
            }

            if (serverInfoCollection.Count() > 0)
            {
                countFreeDiskSpace();
                return true;
            }

            return false;
        }

        // Рассчет свободного места на диске
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
        private static void prepareGoogleTables()
        {
            googleService = GoogleService.LogIn(Configurator.Username);
            Configurator.GoogleTableId = googleService.SetTable(Configurator.GoogleTableId, Configurator.ServerNames);
        }

    }
}
