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

    private static DatabaseWorker Instance = null;

    private DatabaseWorker(string connectionString, Logger logger)
    {
      this.logger = logger;
      this.connectionString = connectionString;
      Connect();
    }

    public static DatabaseWorker GetInstance(string connectionString, Logger logger)
    {
      if(Instance == null)
      {
        Instance = new DatabaseWorker(connectionString, logger);
      }
      return Instance;
    }

    /// <summary>
    /// connect to the database
    /// </summary>
    public void Connect()
    {                                         
      try
      {
        if (conn == null)
        {
          conn = new NpgsqlConnection(connectionString);
        }
        if (conn.State != System.Data.ConnectionState.Open)
        {
          conn.Open();
        }
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
      NpgsqlDataReader result = null;
      try
      {
        var response = "";
        using (var cmd = new NpgsqlCommand(query, conn))
        {
          logger.LogInformation($"Write query = {query}");
          cmd.Parameters.Add(new NpgsqlParameter("d", NpgsqlDbType.Jsonb) { Value = jsonData });
          result = cmd.ExecuteReader();
          if (result.HasRows)
          {
            if (result.Read())
            {
              response = ConvertFromDBVal<string>(result["text"]);
            }
            else
            {
              logger.LogError("Response is empty");
            }
          }
          else
          {
            logger.LogError("Ответ от бд не получен.");
            return null;
          }
          return response;
        }
      }
      catch (Exception ex)
      {
        logger.LogError($"Ошибка при записи json в бд. {ex.Message}");
        return null;
      }
      finally
      {
        result.Close();
      }
    }

    public string GetXml(string query)
    {
      NpgsqlDataReader result = null;
      try
      {
        string resultXml = "";
        using (var cmd = new NpgsqlCommand(query, conn))
        {
          result = cmd.ExecuteReader();
          logger.LogDebug("Response count = " + result.VisibleFieldCount);
          if (result.HasRows)
          {
            if (result.Read())
            {
              resultXml = ConvertFromDBVal<string>(result[0]);
            }
            else
            {
              logger.LogError("Response is empty");
            }
          }
          else
          {
            logger.LogError("Ответ от бд не получен.");
            return null;
          }
        }
        return resultXml;
      }
      catch (Exception ex)
      {
        logger.LogError($"Ошибка при получении xml файла. {ex.Message}");
        return null;
      }
      finally
      {
        result.Close();
      }
    }

    public static T ConvertFromDBVal<T>(object obj)
    {
      if (obj == null || obj == DBNull.Value)
      {
        return default(T); // returns the default value for the type
      }
      else
      {
        return (T)obj;
      }
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
