using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbSizeCheker
{
    class PostgreSqlService
    {
        //Запрос для получения списка всех БД с их размерами + общий размер всех БД
        const string GET_DATABASES_SIZE =
            @"SELECT pg_database.datname AS  db_name,
                     pg_database_size(pg_database.datname) AS db_size
              FROM pg_database
              UNION ALL
              SELECT 'Свободно',
                     sum (pg_database_size(pg_database.datname))
              FROM pg_database";

        public async Task<List<DbSize>> GetDbsSizeAsync(string connectionString)
        {
            var dbs = new List<DbSize>();
            using (var connection = new NpgsqlConnection(connectionString))
            using (var command = new NpgsqlCommand(GET_DATABASES_SIZE, connection))           
            {
                await connection.OpenAsync();
                var lastUpdated = DateTime.Now.ToString("dd.MM.yyyy");

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var dbInfo = new DbSize
                        {
                            LastUpdated = lastUpdated,
                            DataBaseName = reader.GetString(0),
                            DataBaseSize = formatOutput(reader.GetInt64(1))
                        };

                        dbs.Add(dbInfo);
                    }
                }
            }

            return dbs;
        }

        // Байты в гигабайты
        private decimal formatOutput(decimal bytes)
        {
            return Math.Round(bytes / 1024 / 1024 / 1024, 3);
        }
    }
}
