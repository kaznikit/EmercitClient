using Microsoft.Extensions.Logging;
using MiradaStdSDK;
using MiradaStdSDK.FirmwaresStore;
using Npgsql;
using System;
using System.Configuration;
using System.IO;
using System.Net;               

namespace EmercitClient
{
  class Program
  {
    static string connectionString = ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString;
    static string sqlQuery = ConfigurationManager.AppSettings["SQLQuery"];

    static NpgsqlConnection conn;

    static void Main(string[] args)
    {
      //XmlSerializer formatter = new XmlSerializer(typeof(ier));

      //ier obj = new ier();

      //using (FileStream fs = new FileStream("file.xml", FileMode.Open, FileAccess.Read))
      //{
      //  obj = (ier)formatter.Deserialize(fs);
      //}

      //string jss = JsonConvert.SerializeObject(obj);  

      DatabaseWorker.Instance.Connect(connectionString);

      var logger = new Logger();

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
          ExchangeServer.CreateInstance(dataProcessor, store, new IPEndPoint(IPAddress.Any, 4090), 5);

      logger.LogInformation("Добавление контроллеров в хранилище");

      exchangeServer.KeyStore.AddOrUpdate(2120001, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA1", false);
      exchangeServer.KeyStore.AddOrUpdate(2120002, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA2", false);
      exchangeServer.KeyStore.AddOrUpdate(2120003, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA3", false);

      logger.LogInformation("Блокировка контроллера 2120003");

      exchangeServer.LockController(2120003);

      logger.LogInformation("Разблокировка контроллера 2120003");

      exchangeServer.UnlockController(2120003);

      logger.LogInformation("Запуск сервера");

      exchangeServer.Start();

      Console.WriteLine("Нажмите клавишу Enter для выхода...");
      Console.ReadLine();

      logger.LogInformation("Останов сервера");

      exchangeServer.Stop();
    }   
  }
}
