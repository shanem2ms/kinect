using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace kinectwall
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string BodyFile;
        public static string DepthFile;
        public static string CharacterFile;
        public delegate void WriteMsgDel(string msg);
        public static WriteMsgDel OnWriteMsg;

        public static void WriteLine(string msg)
        {
            OnWriteMsg(msg + "\n");
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            for (int argidx = 0; argidx < e.Args.Length;)
            {
                switch (e.Args[argidx])
                {
                    case "-c":
                        CharacterFile = e.Args[argidx + 1];
                        break;
                    case "-b":
                        BodyFile = e.Args[argidx + 1];
                        break;
                    case "-d":
                        DepthFile = e.Args[argidx + 1];
                        break;
                }
                argidx += 2;
            }
            MainWindow mw = new MainWindow();
            mw.Show();
        }
    }
}
