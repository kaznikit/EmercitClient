using Microsoft.Extensions.Logging;
using MiradaStdSDK;
using MiradaStdSDK.Enums;
using MiradaStdSDK.Interfaces;
using MiradaStdSDK.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Xml.Serialization;

namespace EmercitClient
{
  public class DataProcessor : IDataProcessor
  {
    public ILogger Logger { get; }
    private Working working;

    public DataProcessor(ILogger logger, Working working)
    {
      Logger = logger;
      this.working = working;
    }

    public void ConnectionChanged(ConnectionArgs args)
    {
      if (args.State)
        Logger.LogInformation($"Соединение с контроллером {args.Serial} " +
                      $"установлено [{args.Endpoint}, {args.Protocol.ToString()}]. " +
                      $"Версия ПО: {args.Revision}");

      else
        Logger.LogInformation($"Соединение с контроллером {args.Serial} разорвано");
    }

    public uint GetSecondsUntilNextConnection(uint controllerSerial)
    {
      //активируется постоянный режим подключения для контроллера 2120001
      if (controllerSerial == 2120001)
        return 0;

      //для остальных контроллеров активируется режим работы по расписанию
      //контроллер подключается каждый час
      Logger.LogInformation($"Вызвана функция получения расписания. {controllerSerial}");
      return (uint)working.sendingTimeout;
    }

    public void ProcessData(Archive archive)
    {
      Logger.LogInformation($"Получены данные от {archive.Controller}." +
                                        $"Данные {archive.Data.Count}, " +
                                        $"статусы {archive.States.Count}, " +
                                        $"оповещения: {archive.Notifications.Count}.");
      try
      {
        //create json string
        string json = JsonConvert.SerializeObject(archive);
        //write to database
        if (!DatabaseWorker.Instance.isConnected)
        {
          DatabaseWorker.Instance.Connect();
        }
        Logger.LogInformation($"JSON = {json}");
        var response = DatabaseWorker.Instance.WriteToDb(Working.write_sqlQuery, json);
        Logger.LogInformation($"Response from writting query = {response}");
      }
      catch (Exception ex)
      {
        Logger.LogError($"Json parse error. {ex.Message}");
      }
    }
  }
}
