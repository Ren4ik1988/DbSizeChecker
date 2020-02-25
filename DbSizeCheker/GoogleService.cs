using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DbSizeCheker
{
    public class GoogleService
    {
        #region Реализация паттерна "Синглтон"

        private static readonly Lazy<GoogleService> _instance = new Lazy<GoogleService>();
        public GoogleService() { }

        //Конструкция обеспечивает только один вызов метода LogIn на протяжении жизни приложения
        private string _googleLogin;
        public static GoogleService LogIn(string googleLogin)
        {
            var instance = _instance.Value;
            if (String.IsNullOrWhiteSpace(instance._googleLogin))
            {
                instance._googleLogin = googleLogin;
                instance.Auth(googleLogin);
            }
            return instance;

        }

        #endregion

        #region Private fields

        private string _applicationName;
        private UserCredential _credential;
        private SheetsService _service;
        private string _spreadSheetId;

        #endregion

        #region Private methods

        // Авторизация в google.com
        private void Auth(string googleLogin)
        {
            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                _applicationName = "DbSizeChecker";
                string[] _scopes = { SheetsService.Scope.Spreadsheets };

                // Файл [placeholder].json хранит токены пользователя (access and refresh tokens) и создается при первом логине.
                // Placeholder - название файла, значение берется из App.config по ключу GoogleUser.
                // Если данный параметр не определен в файле конфигурации, устанавливается название по умолчанию - default.json
                // При таком подходе пользователь имеет возможность переключаться между аккаунтами без необходимости повторной аутентификации, меняя параметр GoogleUser в файле конфигурации перед запуском программы
                string credPath = $"{Configurator.Username}.json";

                _credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    _scopes,
                    Configurator.Username,
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Авторизация в сервисе \"Google tables\" прошла успешно.");
                Console.WriteLine($"Авторизационные данные сохранены в файле {credPath} и будут использованы при последующих запусках программы.");
            }

            _service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = _applicationName
            });

        }

        // Создание новой таблицы с необходимыми листами
        private string createSpreadSheet(string spreadSheetName, List<string> sheetNames)
        {
            var s = new Spreadsheet();
            s.Properties = new SpreadsheetProperties
            {
                Title = spreadSheetName
            };

            s.Sheets = new List<Sheet>();

            foreach (var name in sheetNames)
            {
                s.Sheets.Add(new Sheet
                {
                    Properties = new SheetProperties()
                    {
                        Title = name
                    }
                });
            }

            s = _service.Spreadsheets.Create(s).Execute();

            _spreadSheetId = s.SpreadsheetId;
            Console.WriteLine($"Создана новая таблица. Просмотр таблицы доступен по ссылке: {s.SpreadsheetUrl}");

            return s.SpreadsheetId;
        }

        // Формирование табличных данных
        private ValueRange replaceContnet(string serverName, List<DbSize> dbSizeList)
        {
            IList<IList<object>> data = new List<IList<object>>();

            data.Add(new List<object>()
            {
                "Сервер",
                "База данных",
                "Размер в ГБ",
                "Дата обновления"
            });

            foreach( var db in dbSizeList)
            {
                data.Add(new List<object>()
                {
                    serverName,
                    db.DataBaseName,
                    db.DataBaseSize,
                    db.LastUpdated
                });
            }

            ValueRange body = new ValueRange
            {
                Values = data,
                Range = $"{serverName}!A:D",
            };

            return body;           
        }

        // Создание листа в таблице
        private void createSheets(string sheetName)
        {
            var sheetRequest = new AddSheetRequest();
            sheetRequest.Properties = new SheetProperties
            {
                Title = sheetName
            };

            var sheetsUpdate = new BatchUpdateSpreadsheetRequest();
            sheetsUpdate.Requests = new List<Request>();
            sheetsUpdate.Requests.Add(new Request
            {
                AddSheet = sheetRequest
            });

            _service.Spreadsheets.BatchUpdate(sheetsUpdate, _spreadSheetId).Execute();

        }

        #endregion

        #region Public methods

        // Если в файле конфигурации не определен ID таблицы, создается новая c листами для хранения данных из БД. 
        // Если ID определен, проверяется наличие таблицы, а так же наличие листов для хранения данных из БД, недостающие листы создаются.
        public string SetTable(string tableId, List<string> sheetNames)
        {
            if (!String.IsNullOrWhiteSpace(tableId))
            {
                try
                {
                    var s = _service.Spreadsheets.Get(tableId).Execute();
                    _spreadSheetId = s.SpreadsheetId;
                    Console.WriteLine($"Табилца из конфигурационного файла загружена. Просмотр доступен по ссылке: {s.SpreadsheetUrl}");

                    foreach(var name in sheetNames)
                    {
                        if(s.Sheets.FirstOrDefault(sh => sh.Properties.Title == name) == null)
                        {
                            createSheets(name);
                        }
                    }
                    
                    return s.SpreadsheetId;
                }
                catch
                {
                    Console.WriteLine("Не удалось найти таблицу из конфигурацинного файла.");
                }
            }
                
            return createSpreadSheet(_applicationName, sheetNames);
        }

        // Очистка старых записей(если количество БД на сервере уменьшилось, лишние строки в таблице должны быть удалены) и публикация новых.
        public void UpdateSpreadSheet(Dictionary<string, List<DbSize>> serverInfoCollection)
        {
            BatchUpdateValuesRequest addRequest = new BatchUpdateValuesRequest();
            BatchClearValuesRequest clearRequest = new BatchClearValuesRequest();

            clearRequest.Ranges = new List<string>();           

            addRequest.Data = new List<ValueRange>();
            addRequest.ValueInputOption = "USER_ENTERED";

            foreach (var server in serverInfoCollection)
            {
                clearRequest.Ranges.Add($"{server.Key}!A2:D50");
                addRequest.Data.Add(replaceContnet(server.Key, server.Value));
            }

            _service.Spreadsheets.Values.BatchClear(clearRequest, _spreadSheetId).Execute();
            _service.Spreadsheets.Values.BatchUpdate(addRequest, _spreadSheetId).Execute();
        }

        #endregion
    }
}
