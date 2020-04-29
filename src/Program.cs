using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QlikView.Qvx.QvxLibrary;
using System.Diagnostics;

namespace QvGamsConnector
{

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && args.Length >= 2)
            {
                new Server().Run(args[0], args[1]);
            }
        }
    }
}
