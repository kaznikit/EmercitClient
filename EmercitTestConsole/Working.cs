using Microsoft.Extensions.Logging;
using MiradaStdSDK;
using MiradaStdSDK.FirmwaresStore;
using System;
using System.Configuration;
using System.IO;
using System.Net;

namespace EmercitClient
{
  public class Working
  {
    string connectionString = ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString;
    public static string write_sqlQuery = ConfigurationManager.AppSettings["Write_SQLQuery"];
    public static string read_sqlQuery = ConfigurationManager.AppSettings["Read_SQLQuery"];

    public Logger logger = new Logger();

    public void Initialize()
    {
      var dataProcessor = new DataProcessor(logger);

      //Cоздание загрузчика и хранилища обновлений. При добавлении/удалении файлов обновлений в указанную
      //директорию автоматически будет выполенено добавление или удаление данного обновления в хранилище.
      //Это позволяет выполнять добавление файла обновления не перезапуская сервис. 
      const string updatesPath = @"C:\";
      var store = new FilesFirmwaresStore(new FirmwaresWatcher(logger, NotifyFilters.FileName, updatesPath),
          new FileLoader(), logger);

      //Создание экземпляра класса сервера (прослушивание всех интерфейсов и TCP порта 4090, размер очереди
      //подключения контроллеров равен 5 
      var exchangeServer =
          ExchangeServer.CreateInstance(dataProcessor, store, new IPEndPoint(IPAddress.Any, 8081), 5);

      logger.LogInformation("Добавление контроллеров в хранилище");

      DatabaseWorker.Instance.Initialize(connectionString, logger);
      DatabaseWorker.Instance.Connect();
      var controllers = DatabaseWorker.Instance.ReadData(read_sqlQuery);

      foreach(var controller in controllers)
      {
        exchangeServer.KeyStore.AddOrUpdate(controller.Id, controller.Key, false);
        exchangeServer.UnlockController(controller.Id);
        logger.LogInformation($"Контроллер {controller.Id} добавлен и разблокирован.");
      }
      //exchangeServer.KeyStore.AddOrUpdate(2120001, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA1", false);
      //exchangeServer.KeyStore.AddOrUpdate(2120002, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA2", false);
      //exchangeServer.KeyStore.AddOrUpdate(2120003, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA3", false);

      //logger.LogInformation("Блокировка контроллера 2120003");

      //exchangeServer.LockController(2120003);

      //logger.LogInformation("Разблокировка контроллера 2120003");

      //exchangeServer.UnlockController(2120003);

      logger.LogInformation("Запуск сервера");

      exchangeServer.Start();
      
      foreach(var cont in controllers)
      {
        // dataProcessor.ConnectionChanged(new ConnectionArgs(cont.Id, 2, MiradaStdSDK.Enums.Protocol.Ethernet, "10.30.45.9"));
      }

      Console.WriteLine("Нажмите клавишу Enter для выхода...");
      Console.ReadLine();

      logger.LogInformation("Останов сервера");

      exchangeServer.Stop();
    }   
  }
}
