using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace DbSizeCheker
{
    class Program
    {
        private static GoogleService googleService;
        private static PostgreSqlService postgreSqlService;
        private static Timer timer;
        private static Dictionary<string, List<DbSize>> serverInfoCollection;

        static void Main(string[] args)
        {
            Console.WriteLine("Проверка  файла конфигурации...");

            if(Configurator.IsConfigurationCorrect())
            {
                postgreSqlService = new PostgreSqlService();
                serverInfoCollection = new Dictionary<string, List<DbSize>>();

                var connectionStrings = Configurator.ConnectionStrings;

                Console.WriteLine("Проверка подключения к сервису \"Google SpreadSheets\".\n\r");
                prepareGoogleTables();

                // Таймер запускается через каждый 30 секунд, либо через промежуток времени определенный в файле конфигурации
                timer = new Timer(TimeSpan.FromSeconds(Configurator.TimerInterval).TotalMilliseconds);
                timer.AutoReset = true;
                timer.Elapsed += updateData;

                Console.WriteLine($"\n\rПрограмма запущена. Интервал обновления данных - {Configurator.TimerInterval} секунд. Для выхода из программы нажмите клавишу \"Q\"\n\r");

                // Первый запуск метода вручную
                updateData(timer, null);


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
        private static async void updateData(object sender, ElapsedEventArgs e)
        {
            //Для избежания возможного перекрытия вызова метода, управляем таймером внутри метода
            var timer = sender as Timer;
            timer.Stop();

            if (await extractDbInfoAsync())
            {
                await googleService.UpdateSpreadSheetAsync(serverInfoCollection);

                Console.WriteLine("\n\rТаблица обновлена.\n\r");
            }
            else
            {
                Console.WriteLine($"Не удалось подключиться ни к одному из серверов баз данных. Повторная попытка через {Configurator.TimerInterval} секунд.\n\r");
            }

            timer.Start();
        }

        // Извлекает данные с сервера, если успешно - возвращает true
        private async static Task<bool> extractDbInfoAsync()
        {
            serverInfoCollection.Clear();

            var tasks = new List<Task<List<DbSize>>>();
            var connectionStrings = Configurator.ConnectionStrings;

            foreach (var cs in connectionStrings)
            {
                Console.WriteLine($"Отправка запроса на {cs.Name}");
                tasks.Add(postgreSqlService.GetDbsSizeAsync(cs.ConnectionString));
            }

            while(tasks.Any())
            {
                var finished = await Task.WhenAny(tasks);
                var index = tasks.IndexOf(finished);
                var serverName = connectionStrings[index].Name;
                tasks.RemoveAt(index);
                connectionStrings.RemoveAt(index);

                try
                {
                    serverInfoCollection.Add(serverName, await finished);
                    Console.WriteLine($"Данные с сервера {serverName} успешно загружены.");
                }
                catch
                {
                    Console.WriteLine($"Сервер {serverName} недоступен, проверьте строку подключения.");
                }
            }

            countFreeDiskSpace();

            return serverInfoCollection.Any();
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
