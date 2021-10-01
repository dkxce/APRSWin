using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;

using System.Net;
using System.Net.Sockets;

namespace RTTSWin
{
    static class Program
    {        
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Form1 f1 = new Form1();
            if (f1.ShowDialog() == DialogResult.OK)
            {
                Application.Run(new Form2(f1.remCFG, f1.Mode));
                f1.Dispose();
            }
            else
                f1.Dispose();
        }        
    }
}