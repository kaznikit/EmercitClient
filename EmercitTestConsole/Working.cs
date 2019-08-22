using Microsoft.Extensions.Logging;
using MiradaStdSDK;
using MiradaStdSDK.FirmwaresStore;
using MiradaStdSDK.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Xml.Serialization;

namespace EmercitClient
{
  public class Working
  {
    string connectionString = ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString;
    public static string write_sqlQuery = ConfigurationManager.AppSettings["Write_SQLQuery"];
    public static string read_sqlQuery = ConfigurationManager.AppSettings["Read_SQLQuery"];
    public static string get_xml_Query = ConfigurationManager.AppSettings["Get_XML_Query"];
    public static string serviceUrl = ConfigurationManager.AppSettings["ServiceUrl"];
    public static string sendingDataTimeout = ConfigurationManager.AppSettings["SendDataTimeout"];
    public static string updatesPath = ConfigurationManager.AppSettings["Updates_Path"];
    public int sendingTimeout = 0;

    public Logger logger = new Logger();
    private EmercitAdapterService emercitService;
    private List<Controller> controllers;
    private Timer sendingDataTimer;

    public void Initialize()
    {
      var dataProcessor = new DataProcessor(logger, this);
      emercitService = new EmercitAdapterService(serviceUrl);

      int value = 0;
      if (int.TryParse(sendingDataTimeout, out value))
      {
        sendingTimeout = value;
      }
      else
      {
        logger.LogError("Проблема при считывании таймаута отправки данных из конфига. Установлено значение 3600 секунд.");
        sendingTimeout = 3600;
      }

      sendingDataTimer = new Timer(Callback, null, TimeSpan.FromSeconds(sendingTimeout), Timeout.InfiniteTimeSpan);

      //Cоздание загрузчика и хранилища обновлений. При добавлении/удалении файлов обновлений в указанную
      //директорию автоматически будет выполенено добавление или удаление данного обновления в хранилище.
      //Это позволяет выполнять добавление файла обновления не перезапуская сервис. 
      //const string updatesPath = @"C:\";

      FilesFirmwaresStore store = null;
      try
      {
        var path = Path.GetDirectoryName(updatesPath);
        store = new FilesFirmwaresStore(new FirmwaresWatcher(logger, NotifyFilters.FileName, path), new FileLoader(), logger);
      }
      catch (Exception ex)
      {
        logger.LogError($"Ошибка при обновлении. {ex.Message}. Путь из конфига: {updatesPath}");
      }

      if (store == null)
      {
        updatesPath = @"C:\";
        store = new FilesFirmwaresStore(new FirmwaresWatcher(logger, NotifyFilters.FileName, updatesPath), new FileLoader(), logger);
      }

      //Создание экземпляра класса сервера (прослушивание всех интерфейсов и TCP порта 4090, размер очереди
      //подключения контроллеров равен 5 
      var exchangeServer =
          ExchangeServer.CreateInstance(dataProcessor, store, new IPEndPoint(IPAddress.Any, 8081), 5);

      logger.LogInformation("Добавление контроллеров в хранилище");

      DatabaseWorker.Instance.Initialize(connectionString, logger);
      DatabaseWorker.Instance.Connect();
      controllers = DatabaseWorker.Instance.ReadData(read_sqlQuery);

      foreach (var controller in controllers)
      {
        exchangeServer.KeyStore.AddOrUpdate(controller.Id, controller.Key, false);
        exchangeServer.UnlockController(controller.Id);
        logger.LogInformation($"Контроллер {controller.Key} добавлен и разблокирован.");
      }
      logger.LogInformation("Запуск сервера");

      exchangeServer.Start();


      Console.WriteLine("Нажмите клавишу Enter для выхода...");
      Console.ReadLine();
      logger.LogInformation("Останов сервера");

      exchangeServer.Stop();
    }

    private void Callback(object state)
    {
      sendingDataTimer.Change(TimeSpan.FromSeconds(sendingTimeout), Timeout.InfiniteTimeSpan);

      CallSendData();
    }

    private void CallSendData()
    {
      logger.LogInformation("Вызвана функция отправки данных на сервер.");
      Thread thread = new Thread(new ThreadStart(SendDataToService));
      thread.Start();
    }

    private void SendDataToService()
    {
      try
      {
        if (!DatabaseWorker.Instance.isConnected)
        {
          DatabaseWorker.Instance.Connect();
        }
        string result = DatabaseWorker.Instance.GetXml(get_xml_Query);
        if (!string.IsNullOrEmpty(result))
        {
          XmlSerializer serializer = new XmlSerializer(typeof(ier));
          logger.LogInformation($"Got xml string.");
          ier ier = new ier();
          using (TextReader reader = new StringReader(result))
          {
            ier = (ier)serializer.Deserialize(reader);
            // iers.Add((ier)serializer.Deserialize(reader));
          }
          logger.LogInformation($"Xml deserialized successfully.");
          var res = emercitService.newIER(ier);
          logger.LogInformation($"Data sent to the service. Result = {res}");
        }
        else
        {
          logger.LogError($"Xml string is empty. Result = {result}");
        }
      }
      catch (Exception ex)
      {
        logger.LogError($"Error with sending data to the service. {ex.Message} {ex.InnerException}");
      }
    }
  }
}
