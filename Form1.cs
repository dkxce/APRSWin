using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Xml;

namespace RTTSWin
{
    public partial class Form1 : Form
    {
        public tsRemoteConfig remCFG = null;
        public byte Mode = 0; // 0 - user; 1 - manager; 2 - admin

        public Form1()
        {
            InitializeComponent();
            this.AcceptButton = button1;
            ReadMRU();
            if (MRU.Count > 0)
            {
                for(int i=Math.Max(0,MRU.Count-15);i<MRU.Count;i++)
                    UrlBox.Items.Insert(0, MRU[i]);
                UrlBox.Text = MRU[MRU.Count - 1];
                MRUD = MRU.Count - 1;
            };
        }

        public void Grab()
        {
            remCFG = null;
            string apt = UrlBox.Text.ToLower();
            if (apt.StartsWith("aprs") || apt.StartsWith("agw") || apt.StartsWith("kiss"))
            {
                Regex r = new Regex(@"aprs://((?<user>[\w\d\-]+)(:(?<apss>[\d\-]+))?@)?(?<server>[\w\d\.\-_]+):(?<port>\d+)/{0,1}(?<filter>.*)");
                Match m = r.Match(apt);
                if (m.Success)
                {
                    remCFG = new tsRemoteConfig();
                    remCFG.aprs = m.Groups["server"].Value + ":" + m.Groups["port"].Value;
                    remCFG.APRSuser = m.Groups["user"].Value;
                    remCFG.APRSpass = m.Groups["pass"].Value;
                    remCFG.APRSfilter = m.Groups["filter"].Value;
                    remCFG.url = "aprs://" + remCFG.aprs + "/";
                    remCFG.APRSOnly = true;
                    button1.Enabled = true;
                    return;
                };
                r = new Regex(@"agw://(?<server>[\w\d\.\-_]+):(?<port>\d+)/?(?<radio>\d*)");
                m = r.Match(apt);
                if (m.Success)
                {
                    remCFG = new tsRemoteConfig();
                    remCFG.aprs = m.Groups["server"].Value + ":" + m.Groups["port"].Value;
                    remCFG.APRSuser = m.Groups["user"].Value;
                    remCFG.APRSpass = m.Groups["pass"].Value;
                    remCFG.AGWRadio = m.Groups["radio"].Value;
                    remCFG.url = "aprs://" + remCFG.aprs + "/";
                    remCFG.AGWOnly = true;
                    button1.Enabled = true;
                    return;
                };
                r = new Regex(@"kiss://(?<server>[\w\d\.\-_]+):(?<port>\d+)");
                m = r.Match(apt);
                if (m.Success)
                {
                    remCFG = new tsRemoteConfig();
                    remCFG.aprs = m.Groups["server"].Value + ":" + m.Groups["port"].Value;
                    remCFG.APRSuser = m.Groups["user"].Value;
                    remCFG.APRSpass = m.Groups["pass"].Value;
                    remCFG.url = "aprs://" + remCFG.aprs + "/";
                    remCFG.KISSOnly = true;
                    button1.Enabled = true;
                    return;
                };
            };
            MessageBox.Show("Неверный формат строки подключения", "Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Error);
            button1.Enabled = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            UrlBox.Enabled = false;
            Grab();
            if (remCFG != null)
            {
                this.DialogResult = DialogResult.OK;
                Add2MRU(UrlBox.Text);
                SaveMRU();
            }
            else
            {
                
            };
            button1.Enabled = true;
            UrlBox.Enabled = true;
        }

        private List<string> MRU = new List<string>();
        private int MRUD = -1;

        private void ReadMRU()
        {
            string f = XMLSaved<int>.GetCurrentDir() + @"urls.mru";
            if(!File.Exists(f)) return;

            FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.GetEncoding(1251));
            while (!sr.EndOfStream)
                MRU.Add(sr.ReadLine());
            sr.Close();
            fs.Close();
        }

        private void Add2MRU(string url)
        {
            if (MRU.IndexOf(url) >= 0) MRU.RemoveAt(MRU.IndexOf(url));
            MRU.Add(url);            
        }

        private void SaveMRU()
        {
            FileStream fs = new FileStream(XMLSaved<int>.GetCurrentDir() + @"urls.mru", FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
            foreach(string s in MRU)
                sw.WriteLine(s);
            sw.Close();
            fs.Close();
        }

        private void managerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Mode = 1;
            //Text = "RTTS Manager";
        }

        private void adminToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Mode = 2;
            //Text = "RTTS Admin";
        }

        private void userToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Mode = 0;
            //Text = "RTTS";
        }

        private void UrlBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            
        }

        private void UrlBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
            {
                MRUD++;
                if (MRUD > (MRU.Count - 1)) MRUD = 0;
                if (MRU.Count > 0)
                    UrlBox.Text = MRU[MRUD];
                e.Handled = true;
            };
            if (e.KeyCode == Keys.Down)
            {
                MRUD--;
                if (MRUD < 0) MRUD = MRU.Count - 1;
                if (MRU.Count > 0)
                    UrlBox.Text = MRU[MRUD];
                e.Handled = true;
            };
        }
    }
}