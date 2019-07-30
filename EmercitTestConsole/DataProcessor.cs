using Microsoft.Extensions.Logging;
using MiradaStdSDK;
using MiradaStdSDK.Enums;
using MiradaStdSDK.Interfaces;
using MiradaStdSDK.Models;
using System;               

namespace EmercitClient
{
  public class DataProcessor : IDataProcessor
  {
    public ILogger Logger { get; }

    public DataProcessor(ILogger logger)
    {
      Logger = logger;
    }

    public void ConnectionChanged(ConnectionArgs args)
    {
      if (args.State)
        Console.Write($"Соединение с контроллером {args.Serial} " +
                      $"установлено [{args.Endpoint}, {args.Protocol.ToString()}]. " +
                      $"Версия ПО: {args.Revision}");
      else
        Console.WriteLine($"Соединение с контроллером {args.Serial} разорвано");
    }

    public uint GetSecondsUntilNextConnection(uint controllerSerial)
    {
      //активируется постоянный режим подключения для контроллера 2120001
      if (controllerSerial == 2120001)
        return 0;

      //для остальных контроллеров активируется режим работы по расписанию
      //контроллер подключается каждый час
      return 3600;
    }

    public void ProcessData(Archive archive)
    {
      Console.WriteLine($"Получены данные от {archive.Controller}." +
                                        $"Данные {archive.Data.Count}, " +
                                        $"статусы {archive.States.Count}, " +
                                        $"оповещения: {archive.Notifications.Count}.");

      //обработка данных от контроллера
      foreach (var data in archive.Data)
      {
        Console.WriteLine($"Данные: адрес {data.Mac}/{data.ExtId}/{data.Point}/{data.Sequence}," +
                          $"значение {data.Value} " +
                          $"от {data.Time}. Номер запроса {data.RequestId}.");
      }

      //обработка статусов от контроллера
      //получение статуса говорит о неработоспособности сигнала
      //можно получить несколько разных статусов
      foreach (var state in archive.States)
      {
        Console.WriteLine($"Получен статус {state.Value} от сигнала" +
                          $"{state.Mac}/{state.ExtId}/{state.Point}/{state.Sequence}: " +
                          $"{(state.Enabled ? "активен" : "не активен")}");
      }

      foreach (var note in archive.Notifications)
      {
        if (note.Type == SignalNotificationType.ENOTIFY_ALL_STAT_OFF)
        {
          //статус отмены всех статусов с сигналов
          //все поля в адресе устройства (MAC/ExtId/Point/Sequence) могут быть со значением 0xFF
          //это означает, что нужно снять статусы по маске со всех устройств,
          //которые содержат этот подадрес 
        }
      }
    }
  }
}
