using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace QvGamsConnector
{
    public class Logger
    {
        private System.IO.StreamWriter myfile;

        
        public Logger()
        {
            string localpath = Directory.GetCurrentDirectory();
            myfile = File.CreateText(localpath + Path.DirectorySeparatorChar + "GamsConnectorLog.txt");
        }

        public void AppendText(string text)
        {
            if (myfile != null)
            {
                myfile.WriteLine(text);

            }
        }

        ~Logger()
        {
            if (myfile != null)
            {
               // myfile.Close();
            }
        }
    }


}
