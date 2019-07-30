using Npgsql;
using NpgsqlTypes;
using System;               

namespace EmercitClient
{
  public sealed class DatabaseWorker
  {
    private string connectionString;
    private static NpgsqlConnection conn;

    public static DatabaseWorker Instance { get; } = new DatabaseWorker();

    /// <summary>
    /// connect to the database
    /// </summary>
    /// <param name="connectionString">connection string</param>
    public void Connect(string connectionString)
    {
      this.connectionString = connectionString;
      conn = new NpgsqlConnection(connectionString);
      conn.Open();
      if (conn.State == System.Data.ConnectionState.Open)
      {
        conn.TypeMapper.UseJsonNet();
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
  }
}
