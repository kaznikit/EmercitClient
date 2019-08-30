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
    public string connectionString = ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString;
    public static string write_sqlQuery = ConfigurationManager.AppSettings["Write_SQLQuery"];
    public static string read_sqlQuery = ConfigurationManager.AppSettings["Read_SQLQuery"];
    public static string get_xml_Query = ConfigurationManager.AppSettings["Get_XML_Query"];
    public static string serviceUrl = ConfigurationManager.AppSettings["ServiceUrl"];
    public static string sendingDataTimeout = ConfigurationManager.AppSettings["SendDataTimeout"];
    public static string updatesPath = ConfigurationManager.AppSettings["Updates_Path"];
    public static string reconnectControllersTimeout = ConfigurationManager.AppSettings["ReconnectControllersTimeout"];
    public static string controllerPort = ConfigurationManager.AppSettings["ControllerPort"];
    public static string queue = ConfigurationManager.AppSettings["QueueSize"];

    public int sendingTimeout = 0;
    public int reconnectTimeout = 0;
    public int port = 0;
    public int queueSize = 0;
    public Logger logger = new Logger();
    private EmercitAdapterService emercitService;
    private List<Controller> controllers;
    private Timer sendingDataTimer;

    private bool isDataSending = false;  

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

      int controllerValue = 0;
      if (int.TryParse(reconnectControllersTimeout, out controllerValue))
      {
        reconnectTimeout = controllerValue;
      }
      else
      {
        logger.LogError("Проблема при считывании таймаута переподключения контроллеров. Установлено значение 3600 секунд.");
        reconnectTimeout = 3600;
      }

      int portValue = 0;
      if(int.TryParse(controllerPort, out portValue))
      {
        port = portValue;
      }
      else
      {
        logger.LogError("Проблема при считывании порта контроллеров. Установлен порт 8081.");
        port = 8081;
      }

      int q = 0;
      if(int.TryParse(queue, out q))
      {
        queueSize = q;
      }
      else
      {
        logger.LogError("Проблема при считывании количества контроллеров в очереди. Установлено значение 5.");
        queueSize = 5;
      }

      sendingDataTimer = new Timer(Callback, null, TimeSpan.FromSeconds(sendingTimeout), Timeout.InfiniteTimeSpan);

      //Cоздание загрузчика и хранилища обновлений. При добавлении/удалении файлов обновлений в указанную
      //директорию автоматически будет выполенено добавление или удаление данного обновления в хранилище.
      //Это позволяет выполнять добавление файла обновления не перезапуская сервис. 
      //const string updatesPath = @"C:\";

      FilesFirmwaresStore store = null;
      try
      {
        store = new FilesFirmwaresStore(new FirmwaresWatcher(logger, NotifyFilters.FileName, updatesPath), new FileLoader(), logger);
        logger.LogInformation($"Обновление загружено. Версии в store:");
        foreach (var s in store.GetVersions)
        {
          logger.LogInformation(s.ToString());
        }                     
      }
      catch (Exception ex)
      {
        logger.LogError($"Ошибка при обновлении. {ex.Message}. Путь из конфига: {updatesPath}");
      }

      if (store == null)
      {
        logger.LogInformation($"По указанному пути обновления не найдены, проверка директории С:.");
        updatesPath = @"C:\";
        store = new FilesFirmwaresStore(new FirmwaresWatcher(logger, NotifyFilters.FileName, updatesPath), new FileLoader(), logger);
      }

      //Создание экземпляра класса сервера (прослушивание всех интерфейсов и TCP порта 4090, размер очереди
      //подключения контроллеров равен 5 
      var exchangeServer =
          ExchangeServer.CreateInstance(dataProcessor, store, new IPEndPoint(IPAddress.Any, port), queueSize);

      logger.LogInformation("Добавление контроллеров в хранилище");

      try
      {   
        controllers = DatabaseWorker.GetInstance(connectionString, logger).ReadData(read_sqlQuery);

        foreach (var controller in controllers)
        {
          exchangeServer.KeyStore.AddOrUpdate(controller.Id, controller.Key, false);
          logger.LogInformation($"Контроллер {controller.Key} добавлен.");
        }
        logger.LogInformation("Запуск сервера");
      }
      catch (Exception ex)
      {
        logger.LogError($"Проблема при добавлении контроллеров в Exchange Server. {ex.Message}");
      }
      exchangeServer.Start();


      Console.WriteLine("Нажмите клавишу Enter для выхода...");
      Console.ReadLine();
      logger.LogInformation("Останов сервера");

      try
      {
        Dispose();

        exchangeServer.Stop();
      }
      catch (Exception ex)
      {
        logger.LogError($"Ошибка при остановке сервера. {ex.Message}. {ex.InnerException}");
      }
    }

    private void Callback(object state)
    {
      sendingDataTimer.Change(TimeSpan.FromSeconds(sendingTimeout), Timeout.InfiniteTimeSpan);
      isDataSending = true;
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
        if (!DatabaseWorker.GetInstance(connectionString, logger).isConnected)
        {
          DatabaseWorker.GetInstance(connectionString, logger).Connect();
        }
        //string result = DatabaseWorker.GetInstance(connectionString, logger).GetXml(get_xml_Query);
        var result = DatabaseWorker.GetInstance(connectionString, logger).GetXml(get_xml_Query);

        //if (!string.IsNullOrEmpty(result))
        if (result != null && result.Count != 0)
        {
          logger.LogInformation($"Got xml string. Result count = {result.Count}");
          foreach (string s in result)
          {
            XmlSerializer serializer = new XmlSerializer(typeof(ier));
            ier ier = new ier();
            using (TextReader reader = new StringReader(s))
            {
              ier = (ier)serializer.Deserialize(reader);
              // iers.Add((ier)serializer.Deserialize(reader));
            }
            logger.LogInformation($"Xml deserialized successfully.");

            var res = emercitService.newIER(ier);
            logger.LogInformation($"Data sent to the service. Result = {res}");
          }
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
      finally
      {
        isDataSending = false;
      }
    }

    private void Dispose()
    {
      while (isDataSending)
      {
        Thread.Sleep(20);
      }
      sendingDataTimer.Dispose();
    }
  }
}
