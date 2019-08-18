using EmercitTestConsole;
using Microsoft.Extensions.Logging;
using MiradaStdSDK.Interfaces;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;

namespace EmercitClient
{
  public sealed class DatabaseWorker
  {
    private string connectionString;
    private static NpgsqlConnection conn;
    private Logger logger;

    public static DatabaseWorker Instance { get; } = new DatabaseWorker();

    /// <summary>
    /// initialize required parameters
    /// </summary>
    /// <param name="connectionString"></param>
    /// <param name="logger"></param>
    public void Initialize(string connectionString, Logger logger)
    {
      this.logger = logger;
      this.connectionString = connectionString;
    }

    /// <summary>
    /// connect to the database
    /// </summary>
    public void Connect()
    {                                         
      try
      {
        conn = new NpgsqlConnection(connectionString);
        conn.Open();
        if (conn.State == System.Data.ConnectionState.Open)
        {
          conn.TypeMapper.UseJsonNet();
        }
      }
      catch(Exception ex)
      {
        logger.LogError($"Ошибка при подключении к БД. {ex.Message}");
      }
    }

    /// <summary>
    /// check if connection opened
    /// </summary>
    public bool isConnected
    {
      get { return conn.State == System.Data.ConnectionState.Open; }
    }

    /// <summary>
    /// write json data to database
    /// </summary>
    /// <param name="query">sql query</param>
    /// <param name="jsonData">parameter for sql query</param>
    /// <returns></returns>
    public string WriteToDb(string query, string jsonData)
    {
      string result = "";
      try
      {
        using (var cmd = new NpgsqlCommand(query, conn))
        {
          logger.LogInformation($"Write query = {query}");
          cmd.Parameters.Add(new NpgsqlParameter("d", NpgsqlDbType.Jsonb) { Value = jsonData });
          result = cmd.ExecuteNonQuery().ToString();
        }
      }
      catch(Exception ex)
      {
        result = ex.Message;
      }
      return result;
    }

    /// <summary>
    /// read controller parameters from database
    /// </summary>
    /// <param name="query">sql query</param>
    /// <returns></returns>
    public List<Controller> ReadData(string query)
    {
      List<Controller> controllers = new List<Controller>();
      try
      {
        using (var cmd = new NpgsqlCommand(query, conn))
        {
          NpgsqlDataReader reader = cmd.ExecuteReader();
          logger.LogInformation($"Кол-во считанных строк = {reader.VisibleFieldCount}");
          while (reader.Read())
          {
            if (!string.IsNullOrEmpty((string)reader["key"]))
            {
              logger.LogInformation("Добавление строки в словарь.");
              controllers.Add(new Controller()
              {
                Id = uint.Parse(reader["id"].ToString()),
                Key = reader["key"].ToString(),
                Url = reader["url"].ToString(),
                Description = reader["description"].ToString()
              });
            }
          }
          reader.Close();
        }
      }
      catch(Exception ex)
      {
        logger.LogError($"Ошибка при чтении из БД. {ex.Message}");
      }
      return controllers;
    }
  }
}
