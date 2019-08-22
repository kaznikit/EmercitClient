using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmercitClient
{
  public class DataSender
  {
    EmercitAdapterService adapterService;
    
    public DataSender(string url)
    {
      adapterService = new EmercitAdapterService(url);
    }

    public void SendData()
    {
      ier ier = new ier();
      info info = new info();

      adapterService.newIER(ier);
    }
  
    public void SendIer(ier ier)
    {
      adapterService.newIER(ier);
    }

  }
}
