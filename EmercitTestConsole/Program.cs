namespace EmercitClient
{
  class Program
  { 
    static void Main(string[] args)
    {
      //XmlSerializer formatter = new XmlSerializer(typeof(ier));

      //ier obj = new ier();

      //using (FileStream fs = new FileStream("file.xml", FileMode.Open, FileAccess.Read))
      //{
      //  obj = (ier)formatter.Deserialize(fs);
      //}

      //string jss = JsonConvert.SerializeObject(obj);  

      Working working = new Working();
      working.Initialize();
    }   
  }
}
