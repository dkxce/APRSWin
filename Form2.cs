using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using System.Web;
using System.Net.Sockets;

namespace RTTSWin
{
    public partial class Form2 : Form
    {
        private Graphics gDef = Graphics.FromImage(new System.Drawing.Bitmap(256, 256));
        public Color[] gColors = new Color[] {
            Color.Black, 
            Color.FromArgb(0x59,0x29,0),
            Color.FromArgb(0xC9,0,0),
            Color.FromArgb(0xFF,0x59,0),
            Color.FromArgb(0xED,0xEB,0),
            Color.FromArgb(0,0xC9,00),
            Color.FromArgb(0,0,0xC9),
            Color.FromArgb(0xff,0x00,0x94),
            Color.FromArgb(0x0,0x0,0x7b),
            Color.FromArgb(0xf0,0xf0,0xf0),
            Color.FromArgb(0x66,0x66,0x66)};

        public string NavicomURL = "http://maps.navicom.ru/nms/tile?87A930E02E584DEAB087239008C5D943;{x};{y};{z}";
        public int TimeOut = 6; // sec
        private System.Threading.Thread downThread;
        private bool downThreadKill = false;
        private List<string[]> toDown = new List<string[]>();

        public string MainEventID = "";
        public string MainServerURL = "";
        public string MainTCPURL = "localhost:5782";
        public string MainAPRSURL = "localhost:5784";
        public string MainAPRSuser = "NOCALL";
        public string MainAPRSpass = "-1";
        public string MainAPRSfilter = "";
        public byte MainAGWRadio = 0;
        public string APRSTYPE = "APRS";
        private ax25kiss.KISSTNC kiss = null;
        public string LocalPathURL = XMLSaved<int>.GetCurrentDir() + @"\Maps\{id}\{z}\{y}\{x}.png";
        public eventconfig ec;

        private NaviMapNet.MapLayer ML = new NaviMapNet.MapLayer("TRACKING");
        private NaviMapNet.MapLayer M2 = new NaviMapNet.MapLayer("TEXTING");
        private NaviMapNet.MapLayer mapSelect = new NaviMapNet.MapLayer("SELECTING");
        private NaviMapNet.MapLayer mapTail = new NaviMapNet.MapLayer("TAILING");

        private NaviMapNet.MapLayer LL = new NaviMapNet.MapLayer("LOGTRACKING");
        private NaviMapNet.MapLayer L2 = new NaviMapNet.MapLayer("LOGTEXTING");
        private NaviMapNet.MapLayer logSelect = new NaviMapNet.MapLayer("LOGSELECTING");
        private NaviMapNet.MapLayer logTail = new NaviMapNet.MapLayer("LOGTAILING");

        public Regex inputPattern = new Regex(@"RTT@A02J1:(\d{3})\/");

        private ulong AnimationCount = 0;
        private string AnimationDir = XMLSaved<int>.GetCurrentDir()+@"\Animation\";
        private string AnimatedDir = XMLSaved<int>.GetCurrentDir() + @"\Animated\";
        
        public Form2(tsRemoteConfig tsr, byte Mode)
        {
            MainEventID = tsr.Event;
            MainServerURL = tsr.url;
            MainTCPURL = tsr.tcp;
            MainAPRSURL = tsr.aprs;
            MainAPRSuser = tsr.APRSuser;
            MainAPRSpass = tsr.APRSpass;
            MainAPRSfilter = tsr.APRSfilter;
            if(!String.IsNullOrEmpty(tsr.AGWRadio)) MainAGWRadio = byte.Parse(tsr.AGWRadio);

            Init();
            tabControl1.TabPages.Remove(tabPage4);
            tabControl1.TabPages.Remove(tabPage5);
            clearLogToolStripMenuItem.Visible = false;

            if (tsr.APRSOnly)
            {
                this.Text = "APRSWin - aprs://" + (String.IsNullOrEmpty(MainAPRSuser) ? "" : MainAPRSuser + "@") + tsr.aprs + "/ " + (String.IsNullOrEmpty(MainAPRSfilter) ? "" : "with filter " + MainAPRSfilter);
                //bTrack.Enabled = 
                loadOnce.Enabled = mtHTTP.Enabled = mtTCP.Enabled = false;
                SelProto(2);
                APRSTYPE = "APRS";
            };
            if (tsr.AGWOnly)
            {
                this.Text = "APRSWin - agw://" + (String.IsNullOrEmpty(MainAPRSuser) ? "" : MainAPRSuser + "@") + tsr.aprs + "/" + MainAGWRadio.ToString();
                //bTrack.Enabled = 
                loadOnce.Enabled = mtHTTP.Enabled = mtTCP.Enabled = false;
                SelProto(3);
                APRSTYPE = "AGW";
            };
            if (tsr.KISSOnly)
            {
                this.Text = "APRSWin - kiss://" + (String.IsNullOrEmpty(MainAPRSuser) ? "" : MainAPRSuser + "@") + tsr.aprs + "/";
                //bTrack.Enabled = 
                loadOnce.Enabled = mtHTTP.Enabled = mtTCP.Enabled = false;
                SelProto(3);
                APRSTYPE = "KISS";
            };
            this.Text += " [" + System.Reflection.Assembly.GetEntryAssembly().GetName().Version+"]";
        }

        private void Init()
        {
            InitializeComponent();
            toolStripStatusLabel7.Text = "";

            map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.Custom_UserDefined;
            // map.ImageSourceUrl = XMLSaved<int>.GetCurrentDir() + @"\Maps\Navicom\{z}\{y}\{x}.png";
            map.UserDefinedGetTileUrl = GetTilePathCall;
            map.NotFoundTileColor = Color.LightYellow;
            map.OnMapUpdate += new NaviMapNet.NaviMapNetViewer.MapEvent(MapUpdate);
            map.UseDefaultContextMenu = true;            

            map.MapLayers.Add(logSelect);
            map.MapLayers.Add(logTail);
            map.MapLayers.Add(L2);
            map.MapLayers.Add(LL); 

            map.MapLayers.Add(mapSelect);
            map.MapLayers.Add(mapTail);
            map.MapLayers.Add(M2);
            map.MapLayers.Add(ML);            
        }

        private void MapUpdate()
        {
            toolStripStatusLabel1.Text = "Last Requested File: " + map.LastRequestedFile;
            toolStripStatusLabel2.Text = map.CenterDegreesLat.ToString().Replace(",", ".");
            toolStripStatusLabel3.Text = map.CenterDegreesLon.ToString().Replace(",", ".");
        }

        private string GetTilePathCall(int x, int y, int z)
        {
            string HttpURL = SelectedMapType == 0 ? NavicomURL : map.ImageSourceUrl;
            string LocalURL = LocalPathURL.Replace("{id}", MapTypes[SelectedMapType]).Replace("{z}", z.ToString()).Replace("{y}", y.ToString()).Replace("{x}", x.ToString());
            if (SelectedMapType == 12)
                HttpURL = "http://{4}.base.maps.api.here.com/maptile/2.1/maptile/newest/normal.day/{z}/{x}/{y}/256/png8?app_id=xWVIueSv6JL0aJ5xqTxb&app_code=djPZyynKsbTjIUDOBcHZ2g&lg=rus&ppi=72";
            if (SelectedMapType == 13)
                HttpURL = "http://{4}.base.maps.api.here.com/maptile/2.1/maptile/newest/normal.night/{z}/{x}/{y}/256/png8?app_id=xWVIueSv6JL0aJ5xqTxb&app_code=djPZyynKsbTjIUDOBcHZ2g&lg=rus&ppi=72";
            if (SelectedMapType == 14)
                HttpURL = "http://{s}.aerial.maps.api.here.com/maptile/2.1/maptile/newest/terrain.day/{z}/{x}/{y}/256/png8?app_id=xWVIueSv6JL0aJ5xqTxb&app_code=djPZyynKsbTjIUDOBcHZ2g&lg=rus&ppi=72";
            if (SelectedMapType == 15)
                HttpURL = "https://tile{4-1}.maps.2gis.com/tiles?x={x}&y={y}&z={z}&v=1.1";
            if (SelectedMapType == 16)
                HttpURL = "http://tiles.maps.sputnik.ru/{z}/{x}/{y}.png";

            /////
            Random rnd = new Random();
            string[] abs = new string[] { "a", "b", "c" };
            // если перекрытие не состоялось - включаем алгоритм
            string path = HttpURL;
            path = path.Replace("{x}", x.ToString()).Replace("{y}", y.ToString()).Replace("{z}", z.ToString());
            path = path.Replace("{l}", TwoZ(z < 10 ? z - 4 : z - 10).ToString()).Replace("{r}", ToHex8(y).ToString()).Replace("{c}", ToHex8(x).ToString());
            path = path.Replace("{w}", ((x % 4) + (y % 4) * 4).ToString());
            path = path.Replace("{s}", abs[rnd.Next(1, 3)]);
            path = path.Replace("{4}", rnd.Next(1, 4).ToString());
            path = path.Replace("{4-1}", rnd.Next(0, 3).ToString());
            HttpURL = path;
            /////

            if (mtDisk.Checked) // DISK
            {
                return LocalURL;
            }
            else if (ntDW.Checked) // Disk + Web
            {
                if (!System.IO.File.Exists(LocalURL))
                    lock (toDown)
                        toDown.Add(new string[] { HttpURL, LocalURL });
                return LocalURL;
            }
            else if (mtW.Checked) // Web + Disk
            {
                if (!System.IO.File.Exists(LocalURL))
                    lock (toDown)
                        toDown.Add(new string[] { HttpURL, LocalURL });
                return HttpURL;
            }
            else // Web
            {
                return HttpURL;
            };
        }

        private static string TwoZ(int val)
        {
            return val.ToString().Length > 1 ? val.ToString() : "0" + val.ToString();
        }
        private static string ToHex8(int val)
        {
            string res = val.ToString("X");
            while (res.Length < 8) res = "0" + res;
            return res;
        }

        private void DTile()
        {
            while (!downThreadKill)
            {
                while (toDown.Count > 0)
                {
                    string[] ft;
                    lock (toDown)
                    {
                        ft = toDown[0];
                        toDown.RemoveAt(0);
                        if (System.IO.File.Exists(ft[1]))
                        {
                            if (toDown.Count == 0)
                                map.Invoke(new InvokeEmpty(this.TilesGrabComplete));
                            continue;
                        };
                    };
                    
                    try
                    {
                        HttpWebRequest hwr = (HttpWebRequest)HttpWebRequest.Create(ft[0]);
                        hwr.UserAgent = "Mozilla/5.0 (Windows NT 5.1; rv:52.0) Gecko/20100101 Firefox/52.0";
                        hwr.Timeout = TimeOut * 1000;

                        HttpWebResponse wr = (HttpWebResponse)hwr.GetResponse();
                        System.IO.Stream rs = wr.GetResponseStream();
                        Image im = Image.FromStream(rs);
                        rs.Close();

                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ft[1]));
                        im.Save(ft[1]);
                    }
                    catch (Exception ex) 
                    { 
                    };

                    if (toDown.Count == 0)
                        map.Invoke(new InvokeEmpty(this.TilesGrabComplete));

                    System.Threading.Thread.Sleep(1);
                };
                System.Threading.Thread.Sleep(100);
            };            
        }

        private delegate void InvokeEmpty();
        private void TilesGrabComplete()
        {
            if(ntDW.Checked) map.ReloadMap();
        }
        private void TileGrabProcess()
        {

        }

        private void Form2_Load(object sender, EventArgs e)
        {

            downThread = new System.Threading.Thread(DTile);
            downThread.Start();      

            map.DrawMap = true;
            map.DrawVector = true;
            if (bTrack.Checked)
            {                
                MainTimer_Tick(MainTimer, e);
            };
            if (bTrack.Checked && (mtTCP.Checked) || (mtAPRS.Checked))
            {
                callTCPTHreadStart();
            };

            map.AfterMapDraw += new NaviMapNet.NaviMapNetViewer.AfterDraw(AfterMapDraw);
        }        

        private System.Threading.Thread tcpThread = null;
        private bool tcpAlive = false;
        private TcpClient tcpClient = null;
        private Stream tcpStream = null;
        private void callTCPTHreadStart()
        {
            tcpAlive = true;
            tcpThread = new System.Threading.Thread(tcpTHreadProc);
            tcpThread.Start();
        }
        private void callTCPTHreadStop()
        {
            tcpAlive = false;
            try
            {
                if(tcpClient.Connected)
                    this.Invoke(new PassAPRSCommand(PassAPRSComm), new object[] { "Disconnected" });
            }
            catch { };
            if (tcpStream != null)
                tcpStream.Close();
            if (tcpClient != null)
                tcpClient.Close();
            tcpClient = null;
        }
        private void tcpTHreadProc()
        {
            PassMapObjectsDel del = new PassMapObjectsDel(PassMapObjects);
            PassAPRSCommand comm = new PassAPRSCommand(PassAPRSComm);
            while (tcpAlive)
            {
                if ((tcpClient != null) && (tcpClient.Connected) && (tcpStream != null))
                {
                    try
                    {
                        int ava = tcpClient.Available;
                        if (ava > 0)
                        {
                            byte[] btr = new byte[ava];
                            tcpStream.Read(btr, 0, btr.Length);
                            string txt = System.Text.Encoding.GetEncoding(1251).GetString(btr);
                            List<PT0102> objs = new List<PT0102>();
                            while(!String.IsNullOrEmpty(txt))
                            {
                                string[] txtlns = txt.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string txtln in txtlns)
                                {
                                    this.Invoke(comm, new object[] { txtln });

                                    if (txtln.StartsWith("#")) continue;
                                    if (txtln.IndexOf(">") > 0)
                                    {
                                        Buddie b = APRSData.ParseAPRSPacket(txtln);
                                        if ((b != null) && b.PositionIsValid && (!String.IsNullOrEmpty(b.name)))
                                        {
                                            string body = "{ID:'" + b.name + "',DT:'" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + "',Lat:" + b.lat.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",Lon:" + b.lon.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",Alt:0.0,Hdg:" + b.course.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",Spd:" + b.speed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
                                            string fbody = "RTT@A02J1:" + body.Length.ToString("000") + "/" + body + "&&";
                                            byte[] arr = System.Text.Encoding.ASCII.GetBytes(fbody);
                                            RTTPacket trrp = RTTPacket.FromBytes(arr, 0);
                                            PT0102 d = PT0102.FromJSON(trrp.datatext);
                                            d.ORIGIN = txtln;
                                            d.Addit = b.IconSymbol;
                                            objs.Add(d);
                                        };
                                    };
                                };
                                txt = "";
                            };
                            if (objs.Count > 0)
                                this.Invoke(del, new object[] { objs.ToArray() });
                                //PassMapObjects(objs.ToArray());
                        };
                    }
                    catch (Exception ex) 
                    { 

                    };
                };
                System.Threading.Thread.Sleep(100);
            };
        }
        
        private void mtHTTP_Click(object sender, EventArgs e)
        {
            SelProto(0);
        }

        private void SelProto(byte http0tcp1aprs2kiss3)
        {
            mtHTTP.Checked = http0tcp1aprs2kiss3 == 0;
            mtTCP.Checked = http0tcp1aprs2kiss3 == 1;
            mtAPRS.Checked = (http0tcp1aprs2kiss3 == 2) || (http0tcp1aprs2kiss3 == 3);

            if (mtHTTP.Checked)
                callTCPTHreadStop();

            if(mtTCP.Checked && bTrack.Checked)
                callTCPTHreadStart();

            if (mtAPRS.Checked && bTrack.Checked)
                callTCPTHreadStart();
        }

        private void mtTCP_Click(object sender, EventArgs e)
        {
            SelProto(1);
        }

        private void mtDisk_Click(object sender, EventArgs e)
        {
            SelSrc(0);
        }

        private void SelSrc(byte tp)
        {
            mtDisk.Checked = tp == 0;
            ntDW.Checked = tp == 1;
            mtW.Checked = tp == 2;
            webToolStripMenuItem.Checked = tp == 3;
        }

        private void ntDW_Click(object sender, EventArgs e)
        {
            SelSrc(1);
        }

        private void mtW_Click(object sender, EventArgs e)
        {
            SelSrc(2);
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            map.DrawTilesBorder = !map.DrawTilesBorder;
            toolStripMenuItem4.Checked = map.DrawTilesBorder;
            RefreshMapObjects();
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            map.DrawTilesXYZ = !map.DrawTilesXYZ;
            toolStripMenuItem5.Checked = map.DrawTilesXYZ;
            map.ReloadMap();
        }

        private void RefreshMapObjects()
        {
            map.DrawOnMapData();                        
        }

        private void AfterMapDraw(Graphics g, Rectangle r)
        {
            if ((PrintOnMap.Checked))
            {
                if (GVIEW.Items.Count > 0)
                {
                    Font f = new Font("Arial", 10, FontStyle.Bold);
                    for(int i=0;i<GVIEW.Items.Count;i++)
                    {
                        ListViewItemObj lvio = (ListViewItemObj)GVIEW.Items[i];
                        string txt = String.Format("{0} - {1}: {3} км/ч, {4} км ({2})", new string[] { lvio.data.ID, GVIEW.Items[i].SubItems[1].Text, 
                            GVIEW.Items[i].SubItems[3].Text, lvio.data.Spd.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), 
                            lvio.data.Dist.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) });

                        g.DrawString(txt, f, new SolidBrush(Color.White), new PointF(r.Left + 123, r.Bottom - 14 * (GVIEW.Items.Count - i) - 5));
                        g.DrawString(txt, f, new SolidBrush(Color.White), new PointF(r.Left + 125, r.Bottom - 14 * (GVIEW.Items.Count - i) - 3));
                        g.DrawString(txt, f, new SolidBrush(Color.Navy), new PointF(r.Left + 124, r.Bottom - 14 * (GVIEW.Items.Count - i) - 4));
                    };
                };
            };
        }

        private void toolStripComboBox1_Click(object sender, EventArgs e)
        {

        }

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            map.TilesRenderingZoneSize = ((short)(toolStripComboBox1.SelectedIndex + 1));
            if (map.DrawTilesBorder) map.ReloadMap();
        }

        private void map_MouseMove(object sender, MouseEventArgs e)
        {
            locate = false;
            PointF m = map.MousePositionDegrees;
            toolStripStatusLabel4.Text = m.Y.ToString().Replace(",", ".");
            toolStripStatusLabel5.Text = m.X.ToString().Replace(",", ".");
        }

        private void navicomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(0);

            //map.ShowMapTypes = !navicomToolStripMenuItem.Checked;
        }

        private void Form2_Leave(object sender, EventArgs e)
        {
            
        }

        private void Form2_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (kiss != null) kiss.Stop();
            callTCPTHreadStop();
            downThreadKill = true;
            try
            {
                downThread.Abort();
            }
            catch { };
        }

        private void evview_Click(object sender, EventArgs e)
        {
            evview.Checked = !evview.Checked;
            panel1.Visible = evview.Checked;
            enProto.Visible = evview.Checked;
        }

        private void bMap_Click(object sender, EventArgs e)
        {
            bMap.Checked = !bMap.Checked;
            map.DrawMap = bMap.Checked;

            mapVect.Enabled = map.DrawMap;
            mapLays.Enabled = map.DrawMap && map.DrawVector;
        }

        private void bTrack_Click(object sender, EventArgs e)
        {          
            bTrack.Checked = !bTrack.Checked;

            if (!bTrack.Checked)
            {
                MainTimer.Enabled = false;
                if (APRSTYPE == "APRS")
                    callTCPTHreadStop();
                else
                {
                    if (kiss != null) kiss.Stop();
                    PassAPRSComm("Connection Closed " + APRSTYPE + " " + MainAPRSURL);
                    kiss = null;
                };
                toolStripStatusLabel6.Text = "OFF";
                toolStripStatusLabel6.BackColor = Color.Black;
                ToggleViewLog(true);
            };

            if (bTrack.Checked)
            {
                ToggleViewLog(false);                
                MainTimer_Tick(sender, e);
            };
            if (bTrack.Checked && mtTCP.Checked)
            {
                PassAPRSComm("Starting " + APRSTYPE + " " + MainAPRSURL);
                if (APRSTYPE == "APRS")
                    callTCPTHreadStart();
                else
                    KissStart();
            };
            if (bTrack.Checked && mtAPRS.Checked)
            {
                PassAPRSComm("Starting " + APRSTYPE + " " + MainAPRSURL);
                if (APRSTYPE == "APRS")
                    callTCPTHreadStart();
                else
                    KissStart();
            };
        }

        private void KissStart()
        {
            string[] hp = MainAPRSURL.Split(new char[]{':'});
            if (APRSTYPE == "AGW") kiss = new ax25kiss.KISSTNC(hp[0], int.Parse(hp[1]), ax25kiss.KISSTNC.ConnectionMode.AGW);
            if (APRSTYPE == "KISS") kiss = new ax25kiss.KISSTNC(MainAPRSURL);
            last_establshd = false;
            last_connected = false;
            kiss.AGWRadioPort = MainAGWRadio;
            kiss.onPacket = new Incom(this);
            toolStripStatusLabel6.Text = APRSTYPE;
            toolStripStatusLabel6.BackColor = Color.Silver;
            kiss.Start();
        }

        private void ToggleViewLog(bool show)
        {
            if (!show)
            {
                logTail.Clear();
                logSelect.Clear();
                LL.Clear();
                L2.Clear();
                GVIEW.Items.Clear();
                RefreshMapObjects();
                if (tabControl1.Contains(tabPage4))
                {
                    tabControl1.TabPages.Remove(tabPage4);
                    tabControl1.TabPages.Remove(tabPage5);
                    clearLogToolStripMenuItem.Visible = false;
                };
            };
        }

        private string laprsf = "-ev/ZZZZ";
        private bool last_connected = false;
        private bool last_establshd = false;
        private ulong cntr = 0;
        private void MainTimer_Tick(object sender, EventArgs e)
        {
            MainTimer.Enabled = false;
            {
                if((APRSTYPE != "APRS") && (kiss != null))
                {
                    bool now = kiss.IsRunning;
                    if (now != last_connected)
                    {
                        if (now)
                        {
                            PassAPRSComm("Connecting... " + APRSTYPE + " " + MainAPRSURL);
                            cntr = 0;
                        }
                        else
                            PassAPRSComm("Connection Closed " + APRSTYPE + " " + MainAPRSURL);
                    };
                    last_connected = now;
                    if ((cntr++ == 6) || (!last_establshd))
                    {
                        cntr = 0;
                        bool nc = kiss.Connected;
                        if (nc && (!last_establshd))
                        {
                            PassAPRSComm("Connected to " + APRSTYPE + " " + MainAPRSURL);
                            toolStripStatusLabel6.Text = APRSTYPE;
                            toolStripStatusLabel6.BackColor = Color.SkyBlue;
                        };
                        if (last_establshd && (!nc))
                        {
                            PassAPRSComm("Fail connect to " + APRSTYPE + " " + MainAPRSURL);
                            toolStripStatusLabel6.Text = APRSTYPE;
                            toolStripStatusLabel6.BackColor = Color.Red;
                        };
                        last_establshd = nc;
                    };
                };

                PT0102[] objs = new PT0102[0];
                if ((mtAPRS.Checked) && (APRSTYPE == "APRS")) // APRS
                {
                    toolStripStatusLabel6.Text = APRSTYPE;
                    toolStripStatusLabel6.BackColor = Color.Silver;
                    if (tcpClient == null)
                        tcpClient = new System.Net.Sockets.TcpClient();
                    if (!tcpClient.Connected)
                    {
                        toolStripStatusLabel6.Text = APRSTYPE;
                        toolStripStatusLabel6.BackColor = Color.Red;
                        string host = MainAPRSURL.Substring(0, MainAPRSURL.IndexOf(":"));
                        try
                        {
                            tcpClient.Connect(host, int.Parse(MainAPRSURL.Substring(MainAPRSURL.IndexOf(":") + 1)));
                            tcpStream = tcpClient.GetStream();
                            this.Invoke(new PassAPRSCommand(PassAPRSComm), new object[] { "Connected to " + host });
                            if (MainEventID == "*")
                                laprsf = "";
                            else if (MainEventID == "")
                                laprsf = "";
                            else
                                laprsf = MainEventID;
                            laprsf = MainAPRSfilter;
                            string p = "user " + (String.IsNullOrEmpty(MainAPRSuser) ? "viewonly" : MainAPRSuser) + " pass " + (String.IsNullOrEmpty(MainAPRSpass) ? "-1" : MainAPRSuser) + " vers APRSWin 0.1 " + (MainAPRSfilter == "" ? "" : "filter ") + MainAPRSfilter;
                            byte[] pts = System.Text.Encoding.GetEncoding(1251).GetBytes(p + "\r\n");
                            tcpStream.Write(pts, 0, pts.Length);
                            tcpStream.Flush();
                          
                            toolStripStatusLabel6.BackColor = Color.SkyBlue;
                        }
                        catch
                        {
                            toolStripStatusLabel6.Text = APRSTYPE;
                            toolStripStatusLabel6.BackColor = Color.Red;
                        };
                    }
                    else
                    {
                        try
                        {
                            string pt = "#ping";
                            if (laprsf != MainAPRSfilter)
                            {
                                laprsf = MainAPRSfilter;
                                pt = "#filter " + MainAPRSfilter;
                            };
                            byte[] pts = System.Text.Encoding.GetEncoding(1251).GetBytes(pt);
                            tcpStream.Write(pts, 0, pts.Length);
                            tcpStream.Flush();
                            toolStripStatusLabel6.Text = APRSTYPE;
                            toolStripStatusLabel6.BackColor = Color.SkyBlue;
                        }
                        catch
                        {
                            this.Invoke(new PassAPRSCommand(PassAPRSComm), new object[] { "Connection failed" });
                            tcpStream.Close();
                            tcpClient.Close();
                            tcpClient = null;
                            toolStripStatusLabel6.Text = APRSTYPE;
                            toolStripStatusLabel6.BackColor = Color.Red;
                        };
                    };
                };
                PassMapObjects(objs); 
            };
            MainTimer.Enabled = bTrack.Checked;
       }

        public delegate void PassMapObjectsDel(PT0102[] objs);
        public delegate void PassAPRSCommand(string command);
        public void PassAPRSComm(string command)
        {
            textBox1.Text = String.Format("{1}:\t {0}\r\n", command, DateTime.Now.ToString("ddd HH:mm:ss")) + textBox1.Text;
            if (textBox1.Text.Length > 10000) textBox1.Text = textBox1.Text.Substring(0, 10000);
        }
        public void PassMapObjects(PT0102[] objs)
        {
            if (objs == null) return;
            if (objs.Length == 0) return;

            foreach (PT0102 obj in objs)
            {                                
                System.Text.RegularExpressions.Regex rx = new System.Text.RegularExpressions.Regex(@"\d");
                System.Text.RegularExpressions.Match mx = rx.Match(obj.ID);
                int v = -1;
                if (mx.Success) v = 0;
                while (mx.Success) { v = byte.Parse(mx.Value); mx = mx.NextMatch(); };
                int symbol = v == -1 ? 10 : v;                

                bool add = true;                
                // Points
                if (ML.ObjectsCount > 0)
                    for (int i = 0; i < ML.ObjectsCount; i++)
                    {
                        NaviMapNet.MapPoint mp = (NaviMapNet.MapPoint)ML[i];
                        if (mp.Name == obj.ID)
                        {
                            add = false;
                            mp.Points[0].Y = obj.Lat;
                            mp.Points[0].X = obj.Lon;
                        };
                        if ((mapSelect.ObjectsCount != 0) && (mapSelect[0].Name == mp.Name))
                        {
                            mapSelect[0].Points[0] = mp.Points[0];
                        };
                    };
                // Text
                if (M2.ObjectsCount > 0)
                    for (int i = 0; i < M2.ObjectsCount; i++)
                    {
                        NaviMapNet.MapPoint mp = (NaviMapNet.MapPoint)M2[i];
                        if (mp.Name == obj.ID)
                        {
                            add = false;
                            mp.Points[0].Y = obj.Lat;
                            mp.Points[0].X = obj.Lon;
                        };
                        if ((mapSelect.ObjectsCount != 0) && (mapSelect[0].Name == mp.Name))
                            mapSelect[0].Points[0] = mp.Points[0];
                    };
                // Tail
                if(ndt.Checked && (mapTail.ObjectsCount > 0))
                    for (int i = 0; i < mapTail.ObjectsCount; i++)
                    {
                        NaviMapNet.MapPolyLine ml = (NaviMapNet.MapPolyLine)mapTail[i];
                        if (ml.Name == obj.ID)
                            if ((ml.Points[ml.Points.Length - 1].X != obj.Lon) && (ml.Points[ml.Points.Length - 1].Y != obj.Lat))
                            {
                                List<PointF> points = new List<PointF>();
                                points.AddRange(ml.Points);
                                points.Add(new PointF((float)obj.Lon, (float)obj.Lat));
                                float ttl = 0;
                                for (int p = points.Count - 1; p > 0; p--)
                                {                                    
                                    ttl += GetLengthMetersC(points[p - 1].Y, points[p - 1].X, points[p].Y, points[p].X, false);
                                    if (ttl > (int)PigTail.Value)
                                    {
                                        for (int pp = p - 1; pp >= 0; pp--)
                                            points.RemoveAt(pp);
                                        p = 0;
                                    };
                                };
                                ml.Points = points.ToArray();
                            };
                    };      
                // list
                if(LVIEW.Items.Count > 0)
                    for (int i = 0; i < LVIEW.Items.Count; i++)
                    {
                        if (LVIEW.Items[i].Name == obj.ID)
                        {
                            string Name = GetNamebyID(obj.ID);
                            string[] si = GetLVIEWText(obj, Name);                            
                            for (int six = 0; six < si.Length; six++)
                                LVIEW.Items[i].SubItems[six].Text = si[six];
                        };
                    };
                if (add)
                {
                    // Tail
                    {
                        NaviMapNet.MapPolyLine ml = new NaviMapNet.MapPolyLine();
                        ml.UserData = obj;
                        ml.Name = obj.ID;
                        ml.Color = Color.FromArgb(200, gColors[symbol]);
                        ml.Points = new PointF[] { new PointF((float)obj.Lon, (float)obj.Lat) };
                        ml.Width = 4;
                        mapTail.Add(ml);
                    };
                    string aprss = "";
                    bool aprsis = false;
                    if ((obj.Addit != null) && (obj.Addit is string) && (!String.IsNullOrEmpty((string)obj.Addit))) { aprss = (string)obj.Addit; aprsis = true; };
                    // M2
                    {
                        NaviMapNet.MapPoint mp = new NaviMapNet.MapPoint(obj.Lat, obj.Lon);
                        mp.UserData = obj;
                        mp.Name = obj.ID;
                        mp.SizePixels = new Size(0, 0);
                        {
                            mp.DrawText = true;
                            mp.Text = obj.ID;
                            mp.TextFont = new Font("Arial", 9, FontStyle.Bold);
                            SizeF sf = gDef.MeasureString(mp.Name, mp.TextFont);
                            mp.TextOffset = new Point((int)(-sf.Width / 2), 8);
                        };
                        if (ML.ObjectsCount == 0)
                        {
                            if (map.MapBoundsRectDegrees.Left > mp.Points[0].X) map.CenterDegrees = mp.Points[0];
                            if (map.MapBoundsRectDegrees.Right < mp.Points[0].X) map.CenterDegrees = mp.Points[0];
                            if (map.MapBoundsRectDegrees.Top > mp.Points[0].Y) map.CenterDegrees = mp.Points[0];
                            if (map.MapBoundsRectDegrees.Bottom < mp.Points[0].Y) map.CenterDegrees = mp.Points[0];
                        };    
                        M2.Add(mp);
                    };
                    // ML
                    {
                        NaviMapNet.MapPoint mp = new NaviMapNet.MapPoint(obj.Lat, obj.Lon);
                        mp.UserData = obj;
                        mp.Name = obj.ID;
                        if(aprsis)
                            mp.SizePixels = new Size(24, 24);
                        else
                            mp.SizePixels = new Size(16, 16);
                        mp.Text = symbol.ToString();
                        if (symbol == 10)
                        {
                            mp.Text = " ";
                            if (!String.IsNullOrEmpty(obj.ID)) mp.Text = obj.ID.Substring(0, 1);
                        };

                        string ov = mp.Text;
                        if (aprsis)
                            mp.Img = symbol2image(aprss, ref ov);
                        mp.Text = ov;

                        mp.Color = gColors[symbol];
                        if (aprsis) mp.Color = Color.Transparent;                        
                        {
                            mp.DrawText = true;
                            mp.TextFont = new Font("Arial", 9, FontStyle.Bold);
                            mp.TextBrush = new SolidBrush(symbol == 4 || symbol == 9 ? Color.Black : Color.White);
                            SizeF sf = gDef.MeasureString(mp.Text, mp.TextFont);
                            mp.TextOffset = new Point((int)(-sf.Width / 2) + 1, -7);                            
                        };
                        if (ML.ObjectsCount == 0)
                        {
                            if (map.MapBoundsRectDegrees.Left > mp.Points[0].X) map.CenterDegrees = mp.Points[0];
                            if (map.MapBoundsRectDegrees.Right < mp.Points[0].X) map.CenterDegrees = mp.Points[0];
                            if (map.MapBoundsRectDegrees.Top > mp.Points[0].Y) map.CenterDegrees = mp.Points[0];
                            if (map.MapBoundsRectDegrees.Bottom < mp.Points[0].Y) map.CenterDegrees = mp.Points[0];
                        };    
                        ML.Add(mp);
                    };
                    // list
                    {
                        string Name = GetNamebyID(obj.ID);
                        ListViewItemObj lvi = new ListViewItemObj(GetLVIEWText(obj, Name), obj);
                        lvi.Name = obj.ID;
                        LVIEW.Items.Add(lvi);
                    };
                };
            };
            toolStripStatusLabel8.Text = String.Format("Обновлено в {0}", DateTime.Now.ToString("ddd HH:mm:ss"));
            map.ReloadMap();
        }

        private Image symbol2image(string symb, ref string overtext)
        {
            overtext = "";
            Image bIm = global::APRSWin.Properties.Resources._1st;
			if(symb.Length == 2)
			{
                if (symb[0] == '\\')
                    bIm = global::APRSWin.Properties.Resources._2nd;
                else if ((symb[1] == '#') || (symb[1] == '&') || (symb[1] == '0') || (symb[1] == '>') || (symb[1] == 'A') || (symb[1] == 'W') || (symb[1] == '^') || (symb[1] == '_') || (symb[1] == 'a') || (symb[1] == 'c') || (symb[1] == 'i') || (symb[1] == 'n') || (symb[1] == 's') || (symb[1] == 'u') || (symb[1] == 'v') || (symb[1] == 'z'))
                    if (symb[0] != '/')
                    {
                        bIm = global::APRSWin.Properties.Resources._2nd;
                        overtext = symb[0].ToString();
                    };
				symb = symb.Substring(1);
			};
			string symbtable = "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
			int idd = symbtable.IndexOf(symb);
			if(idd < 0) idd = 14;
			int itop =  (int)(Math.Floor((double)idd / 16.0) * 24);
			int ileft = (int)((idd % 16) * 24);
            Bitmap bmp = new Bitmap(24,24);
            Graphics g = Graphics.FromImage(bmp);
            g.DrawImage(bIm,new Point(-ileft,-itop));
            g.Dispose();
            return bmp;
        }

        private string[] GetLVIEWText(PT0102 obj, string Name)
        {
            PT0102 js = obj;
            string lat2 = Math.Floor(js.Lat).ToString("00") + "° " + ((js.Lat - Math.Floor(js.Lat)) * 60).ToString("00.0000", System.Globalization.CultureInfo.InvariantCulture) + "' " + (js.Lat > 0 ? "N" : "S");
            string lon2 = Math.Floor(js.Lon).ToString("000") + "° " + ((js.Lon - Math.Floor(js.Lon)) * 60).ToString("00.0000", System.Globalization.CultureInfo.InvariantCulture) + "' " + (js.Lon > 0 ? "E" : "W");
            string lat3 = Math.Floor(js.Lat).ToString("00") + "° " + (Math.Floor((js.Lat - Math.Floor(js.Lat)) * 60)).ToString("00") + "' " + (((((js.Lat - Math.Floor(js.Lat)) * 60) - Math.Floor((js.Lat - Math.Floor(js.Lat)) * 60)) * 60)).ToString("00.00", System.Globalization.CultureInfo.InvariantCulture) + "\" " + (js.Lat > 0 ? "N" : "S");
            string lon3 = Math.Floor(js.Lon).ToString("000") + "° " + (Math.Floor((js.Lon - Math.Floor(js.Lon)) * 60)).ToString("000") + "' " + (((((js.Lon - Math.Floor(js.Lon)) * 60) - Math.Floor((js.Lon - Math.Floor(js.Lon)) * 60)) * 60)).ToString("00.00", System.Globalization.CultureInfo.InvariantCulture) + "\" " + (js.Lon > 0 ? "E" : "W");

            return new string[] { 
                obj.ID, 
                Name, 
                obj.Event, 
                obj.DT.ToLocalTime().ToString("ddd HH:mm:ss dd.MM.yyyy"), 
                lat3,//js.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + (js.Lat > 0 ? "N" : "S"), 
                lon3,//js.Lon.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + (js.Lon > 0 ? "E" : "W"), 
                obj.Spd.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " км/ч", 
                obj.Hdg.ToString("0")+"° "+ HeadingToText(js.Hdg),
                DateTime.Now.ToString("HH:mm:ss dd.MM.yy"),
                obj.Dist.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " км"};
        }

        private bool locate = false;
        private void map_MouseClick(object sender, MouseEventArgs e)
        {
            if (!locate) return;
            if ((ML.ObjectsCount == 0) && (LL.ObjectsCount == 0)) return;
            Point clicked = map.MousePositionPixels;
            PointF sCenter = map.PixelsToDegrees(clicked);
            PointF sFrom = map.PixelsToDegrees(new Point(clicked.X - 5, clicked.Y + 5));
            PointF sTo = map.PixelsToDegrees(new Point(clicked.X + 5, clicked.Y - 5));
            
            NaviMapNet.MapObject[] objs = ML.Select(new RectangleF(sFrom, new SizeF(sTo.X - sFrom.X, sTo.Y - sFrom.Y)));
            if ((objs != null) && (objs.Length > 0))
            {
                uint len = uint.MaxValue;
                int ind = 0;
                for (int i = 0; i < objs.Length; i++)
                {
                    uint tl = GetLengthMetersC(sCenter.Y, sCenter.X, objs[i].Center.Y, objs[i].Center.X, false);
                    if (tl < len) { len = tl; ind = i; };
                };

                SelectOnMap(objs[ind].Index);
            }
            else
            {
                mapSelect.Clear();
                RefreshMapObjects();
            };

            objs = LL.Select(new RectangleF(sFrom, new SizeF(sTo.X - sFrom.X, sTo.Y - sFrom.Y)));
            if ((objs != null) && (objs.Length > 0))
            {
                uint len = uint.MaxValue;
                int ind = 0;
                for (int i = 0; i < objs.Length; i++)
                {
                    uint tl = GetLengthMetersC(sCenter.Y, sCenter.X, objs[i].Center.Y, objs[i].Center.X, false);
                    if (tl < len) { len = tl; ind = i; };
                };

                SelectOnLogMap(objs[ind].Index);
            }
            else
            {
                logSelect.Clear();
                RefreshMapObjects();
            };            
        }

        private void SelectOnMap(int id)
        {
            if (id < 0) return;

            bool add = true;
            if (mapSelect.ObjectsCount > 0)
                if (mapSelect[0].Name == ML[id].Name)
                    add = false;

            if (add)
                mapSelect.Clear();

            toolStripStatusLabel7.Text = "";
            if (ML[id].ObjectType == NaviMapNet.MapObjectType.mPoint)
            {
                if (add)
                {
                    NaviMapNet.MapObject mo = (NaviMapNet.MapObject)ML[id];
                    NaviMapNet.MapPoint mp = new NaviMapNet.MapPoint(ML[id].Center);
                    mp.Name = ML[id].Name;
                    if(mo.SizePixels.Height == 24)
                        mp.SizePixels = new Size(34, 34);
                    else
                        mp.SizePixels = new Size(24, 24);
                    mp.Squared = false;
                    mp.Color = Color.Fuchsia;
                    mapSelect.Add(mp);
                    RefreshMapObjects();
                };

                if (LVIEW.Items.Count > 0)
                    for (int i = 0; i < LVIEW.Items.Count; i++)
                        if (LVIEW.Items[i].SubItems[0].Text == ML[id].Name)
                            if (!LVIEW.Items[i].Selected)
                            {
                                LVIEW.Items[i].Selected = true;
                                LVIEW.Select();
                            };

                PT0102 js = (PT0102)ML[id].UserData;
                toolStripStatusLabel7.Text = js.ID;
            };
        }

        private void SelectOnLogMap(int id)
        {
            if (id < 0) return;

            bool add = true;
            if (logSelect.ObjectsCount > 0)
                if (logSelect[0].Name == LL[id].Name)
                    add = false;

            if(add)
                logSelect.Clear();
            //textBox2.Text = "";
            if (LL[id].ObjectType == NaviMapNet.MapObjectType.mPoint)
            {
                if (add)
                {
                    NaviMapNet.MapPoint mp = new NaviMapNet.MapPoint(LL[id].Center);
                    mp.Name = LL[id].Name;
                    mp.SizePixels = new Size(24, 24);
                    mp.Squared = false;
                    mp.Color = Color.Teal;
                    logSelect.Add(mp);
                    RefreshMapObjects();
                };

                if (GVIEW.Items.Count > 0)
                    for (int i = 0; i < GVIEW.Items.Count; i++)
                        if (GVIEW.Items[i].SubItems[0].Text == LL[id].Name)
                            if (!GVIEW.Items[i].Selected) 
                            {
                                GVIEW.Items[i].Selected = true;
                                GVIEW.Select();
                            };
            };
        }

        public string GetNamebyID(string ID)
        {
            string Name = "";
            if ((ec != null) && (ec.users != null) && (ec.users.Count > 0))
                foreach (eventuser ev in ec.users)
                    if (ev.id == ID)
                        Name = ev.name;
            return Name;
        }

        public static string HeadingToText(float hdg)
		{
			int d = (int)Math.Round(hdg / 22.5);
			switch (d)
            {
                case 0: return "С";
                case 1: return "ССВ";
                case 2: return "СВ";
                case 3: return "СВВ";
                case 4: return "В";
                case 5: return "ЮВВ";
                case 6: return "ЮВ";
                case 7: return "ЮЮВ";
                case 8: return "Ю";
                case 9: return "ЮЮЗ";
                case 10: return "ЮЗ";
                case 11: return "ЮЗЗ";
                case 12: return "З";
                case 13: return "СЗЗ";
                case 14: return "СЗ";
                case 15: return "ССЗ";
                case 16: return "С";
                default: return "";
            };
		}

        private static uint GetLengthMetersC(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
        {
            double D2R = Math.PI / 180;
            if (radians) D2R = 1;
            double dDistance = Double.MinValue;
            double dLat1InRad = StartLat * D2R;
            double dLong1InRad = StartLong * D2R;
            double dLat2InRad = EndLat * D2R;
            double dLong2InRad = EndLong * D2R;

            double dLongitude = dLong2InRad - dLong1InRad;
            double dLatitude = dLat2InRad - dLat1InRad;

            // Intermediate result a.
            double a = Math.Pow(Math.Sin(dLatitude / 2.0), 2.0) +
                       Math.Cos(dLat1InRad) * Math.Cos(dLat2InRad) *
                       Math.Pow(Math.Sin(dLongitude / 2.0), 2.0);

            // Intermediate result c (great circle distance in Radians).
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            const double kEarthRadiusKms = 6378137.0000;
            dDistance = kEarthRadiusKms * c;

            return (uint)Math.Round(dDistance);
        }

        private void map_MouseDown(object sender, MouseEventArgs e)
        {
            locate = true;
        }

        private void загрузитьСHTTPРазовоToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PT0102[] objs = new PT0102[0];
            {
                toolStripStatusLabel6.Text = "Web";
                toolStripStatusLabel6.BackColor = Color.Silver;
                string url = MainServerURL + "vlist?" + Convert.ToBase64String(System.Text.Encoding.GetEncoding(1251).GetBytes("{Event:'" + MainEventID + "'}"));
                HttpWebRequest hwr = (HttpWebRequest)HttpWebRequest.Create(url);
                hwr.Timeout = TimeOut * 1000;
                try
                {
                    HttpWebResponse rp = (HttpWebResponse)hwr.GetResponse();
                    Stream rs = rp.GetResponseStream();
                    StreamReader sr = new StreamReader(rs);
                    string txt = sr.ReadToEnd();
                    sr.Close();
                    rp.Close();


                    if (txt.Length > 2)
                        objs = Newtonsoft.Json.JsonConvert.DeserializeObject<PT0102[]>(txt);
                    toolStripStatusLabel6.Text = "Web";
                    toolStripStatusLabel6.BackColor = Color.LightGreen;
                }
                catch
                {
                    toolStripStatusLabel6.Text = "Web";
                    toolStripStatusLabel6.BackColor = Color.Red;
                };
            };
            PassMapObjects(objs);
            toolStripStatusLabel6.BackColor = Color.DarkGreen;
        }

        private void LVIEW_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            if (LVIEW.SelectedItems.Count == 0) return;
            if(ML.ObjectsCount == 0) return;

            string ID = LVIEW.SelectedItems[0].Name;
            for(int i=0;i<ML.ObjectsCount;i++)
                if(ML[i].Name == ID)
                    {
                        SelectOnMap(i);
                        return;
                    };
        }

        private void LVIEW_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (LVIEW.SelectedItems.Count == 0) return;
            if (ML.ObjectsCount == 0) return;
            string ID = LVIEW.SelectedItems[0].Name;
            for (int i = 0; i < ML.ObjectsCount; i++)
                if (ML[i].Name == ID)
                {
                    map.CenterDegrees = ML[i].Points[0];
                    return;
                };
        }

        private void oSMMapnikToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(1);
        }

        public int SelectedMapType = 1;
        public string[] MapTypes = new string[] { 
            "Navicom", "Mapnik", "Openvkarte", 
            "Wiki", "None", "MapSerfer", 
            "Cycle", "MapQuest", "German", 
            "Gray", "Hot", "Aqua",
            "HereND", "HereNN", "HereTD",
            "2GIS", "Sputnik" };
        private void ChangeMapType(byte typ)
        {
            lock (toDown) 
                toDown.Clear();

            //map.UserDefinedGetTileUrl = null;
            //map.ShowMapTypes = true;
            SelectedMapType = typ;

            //mapsrc.Enabled = typ == 0;

            navicomToolStripMenuItem.Checked = typ == 0;
            oSMMapnikToolStripMenuItem.Checked = typ == 1;
            oSMOpenvkarteToolStripMenuItem.Checked = typ == 2;
            oSMWikimapiaToolStripMenuItem.Checked = typ == 3;
            //oSMCyclemapToolStripMenuItem.Checked = typ == 4;
            oSMToolStripMenuItem.Checked = typ == 5;
            oSMCycleMapToolStripMenuItem1.Checked = typ == 6;
            oSMMaToolStripMenuItem.Checked = typ == 7;
            oSMGermanToolStripMenuItem.Checked = typ == 8;
            oSMGrayToolStripMenuItem.Checked = typ == 9;
            oSMHotToolStripMenuItem.Checked = typ == 10;
            oSMAquaToolStripMenuItem.Checked = typ == 11;
            hereNormalDayToolStripMenuItem.Checked = typ == 12;
            hereNormalNightToolStripMenuItem.Checked = typ == 13;
            hereTerrainToolStripMenuItem.Checked = typ == 14;
            gISToolStripMenuItem.Checked = typ == 15;
            sputnikruToolStripMenuItem.Checked = typ == 16;

            if (typ > 0)
            {           
                map.ImageSourceType = NaviMapNet.NaviMapNetViewer.ImageSourceTypes.tiles;
                //map.UserDefinedGetTileUrl = null;
            };
            if (typ > 11)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.Custom_UserDefined;
            if (typ == 11)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_Aqua;
            if (typ == 10)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_HOT;
            if (typ == 9)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_Gray;
            if (typ == 8)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_German;
            if (typ == 7)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_MapQuest;
            if (typ == 6)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_Cyclemap;
            if (typ == 5)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_MapSerfer;
            if (typ == 4)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.Rosreestr;
            if(typ == 3)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_Wikimapia;
            if(typ == 2)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_Openvkarte;
            if (typ == 1)
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_Mapnik;
            if(typ == 0)
            {
                map.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.Custom_UserDefined;
                map.ImageSourceUrl = XMLSaved<int>.GetCurrentDir() + @"\Maps\Navicom\{z}\{y}\{x}.png";
                map.UserDefinedGetTileUrl = GetTilePathCall;
            };
        }

        private void oSMOpenvkarteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(2);
        }

        private void oSMWikimapiaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(3);
        }

        private void oSMCyclemapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(4);
        }

        private void oSMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(5);
        }

        private void oSMCycleMapToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ChangeMapType(6);
        }

        private void oSMMaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(7);
        }

        private void oSMGermanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(8);
        }

        private void oSMGrayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(9);
        }

        private void oSMHotToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(10);
        }

        private void oSMAquaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(11);
        }

        private void hereNormalDayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(12);
        }

        private void hereNormalNightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(13);
        }

        private void hereTerrainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(14);
        }

        private void gISToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(15);
        }

        private void sputnikruToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeMapType(16);
        }

        private void webToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelSrc(3);
        }

        private void reloadTilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            map.ReloadMap();
        }

        private void mapDownVisToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mapDown.Visible = true;
        }

        private FormWindowState prevState = FormWindowState.Normal;
        private Rectangle prevBounds = new Rectangle(0,0,400,400);
        private void GoFullscreen(bool fullscreen)
        {
            if (fullscreen)
            {
                this.prevState = this.WindowState;
                this.prevBounds = this.Bounds;
                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                this.Bounds = Screen.PrimaryScreen.Bounds;
            }
            else
            {
                this.WindowState = this.prevState;
                this.Bounds = this.prevBounds;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            }
        }

        private void FullScr_Click_1(object sender, EventArgs e)
        {
            FullScr.Checked = !FullScr.Checked;
            GoFullscreen(FullScr.Checked);
            ShMn.Checked = !FullScr.Checked;
            MainMenuStrip.Visible = !FullScr.Checked;
            ShSt.Checked = !FullScr.Checked;
            statusStrip1.Visible = !FullScr.Checked;
        }

        private void ShMn_Click(object sender, EventArgs e)
        {
            ShMn.Checked = !ShMn.Checked;
            MainMenuStrip.Visible = ShMn.Checked;
        }

        private void ShSt_Click(object sender, EventArgs e)
        {
            ShSt.Checked = !ShSt.Checked;
            statusStrip1.Visible = ShSt.Checked;
        }

        private void fullScr2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FullScr.Checked = !FullScr.Checked;
            GoFullscreen(FullScr.Checked);
        }

        private void ShZm_Click(object sender, EventArgs e)
        {
            ShZm.Checked = map.ShowZooms = !ShZm.Checked;
        }

        private void ShSc_Click(object sender, EventArgs e)
        {
            ShSc.Checked = map.ShowScale = !ShSc.Checked;
        }

        private void ShCC_Click(object sender, EventArgs e)
        {
            ShCC.Checked = map.ShowCross = !ShCC.Checked;
        }

        private void fullScr3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FullScr.Checked = !FullScr.Checked;
            GoFullscreen(FullScr.Checked);
            ShMn.Checked = !FullScr.Checked;
            MainMenuStrip.Visible = !FullScr.Checked;
            ShSt.Checked = !FullScr.Checked;
            statusStrip1.Visible = !FullScr.Checked;
            ShZm.Checked = map.ShowZooms = !FullScr.Checked;
            ShSc.Checked = map.ShowScale = !FullScr.Checked;
        }

        private void fullScr4ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FullScr.Checked = !FullScr.Checked;
            GoFullscreen(FullScr.Checked);
            ShMn.Checked = !FullScr.Checked;
            MainMenuStrip.Visible = !FullScr.Checked;
            ShSt.Checked = true;
            statusStrip1.Visible = true;
            ShZm.Checked = map.ShowZooms = !FullScr.Checked;
            ShSc.Checked = map.ShowScale = !FullScr.Checked;
        }

        private void chEvToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string ev = MainEventID;
            Utils.InputBox("Изменить событие", "Введите новое имя события (A-Z 0-9):", new Regex(@"[A-Z\*]{1}[A-Z\d\-]{0,7}"), ref ev);
            if (MainEventID != ev)
            {
                MainEventID = ev;
                ec = null;
                string EvFile = XMLSaved<int>.GetCurrentDir() + @"\events\" + (MainEventID == null ? "" : (MainEventID == "*" ? "" : MainEventID)) + ".xml";
                if (File.Exists(EvFile)) ec = eventconfig.Load(EvFile);
                if (tcpClient != null)
                {
                    if(tcpStream != null) tcpStream.Close();
                    tcpStream = null;
                    tcpClient = null;
                };
            };
            if (MainEventID == "")
                this.Text = "APRSWin";
            else
                this.Text = "APRSWin - " + MainEventID;

        }       
        
        private void ndt_Click(object sender, EventArgs e)
        {
            ndt.Checked = !ndt.Checked;
            if (!ndt.Checked)
                tapTail.Checked = false;
            tapTail.Enabled = ndt.Checked;
            PigTail.Enabled = ndt.Checked;
        }

        private void clearCacheToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string dir = XMLSaved<int>.GetCurrentDir() + @"\Maps";
            System.IO.Directory.Delete(dir, true);
            MessageBox.Show("Завершено", "Очистка кэша", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void showDDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolStripDropDownButton1.Visible = true;
        }

        private void swapToolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (map.MapTool == NaviMapNet.NaviMapNetViewer.MapTools.mtShift)
                map.MapTool = NaviMapNet.NaviMapNetViewer.MapTools.mtZoomIn;
            else if (map.MapTool == NaviMapNet.NaviMapNetViewer.MapTools.mtZoomIn)
                map.MapTool = NaviMapNet.NaviMapNetViewer.MapTools.mtZoomOut;
            else map.MapTool = NaviMapNet.NaviMapNetViewer.MapTools.mtShift;
        }
        
        private void Form2_Resize(object sender, EventArgs e)
        {
            panel2.Left = ((int)((tabPage4.Width - panel2.Width)/2));
        }

        private void DrawLogged(PT0102 obj)
        {
            RectangleF maprect = map.MapBoundsRectDegrees;

            System.Text.RegularExpressions.Regex rx = new System.Text.RegularExpressions.Regex(@"\d");
            System.Text.RegularExpressions.Match mx = rx.Match(obj.ID);
            int v = -1;
            if (mx.Success) v = 0;
            while (mx.Success) { v = byte.Parse(mx.Value); mx = mx.NextMatch(); };
            int symbol = v == -1 ? 10 : v;            

            // No Tail
            // L2
            {
                NaviMapNet.MapPoint mp = new NaviMapNet.MapPoint(obj.Lat, obj.Lon);
                mp.UserData = obj;
                mp.Name = obj.ID;
                mp.SizePixels = new Size(0, 0);
                {
                    mp.DrawText = true;
                    mp.Text = obj.ID;
                    mp.TextFont = new Font("Arial", 9, FontStyle.Bold);
                    mp.TextBrush = new SolidBrush(Color.Navy);
                    SizeF sf = gDef.MeasureString(mp.Name, mp.TextFont);
                    mp.TextOffset = new Point((int)(-sf.Width / 2), 8);
                };                                
                L2.Add(mp);
            };

            // LL
            {
                NaviMapNet.MapPoint mp = new NaviMapNet.MapPoint(obj.Lat, obj.Lon);
                mp.UserData = obj;
                mp.Name = obj.ID;
                mp.SizePixels = new Size(16, 16);
                mp.Text = symbol.ToString();
                if (symbol == 10)
                {
                    mp.Text = " ";
                    if (!String.IsNullOrEmpty(obj.ID)) mp.Text = obj.ID.Substring(0, 1);
                };

                mp.Color = gColors[symbol];
                {
                    mp.DrawText = true;
                    mp.TextFont = new Font("Arial", 9, FontStyle.Bold);
                    mp.TextBrush = new SolidBrush(symbol == 4 || symbol == 9 ? Color.Black : Color.White);
                    SizeF sf = gDef.MeasureString(mp.Text, mp.TextFont);
                    mp.TextOffset = new Point((int)(-sf.Width / 2) + 1, -7);
                };
                if(MoveMapClick.Checked)
                    if (LL.ObjectsCount == 0)
                    {
                        if (maprect.Left > mp.Points[0].X) map.CenterDegrees = mp.Points[0];
                        if (maprect.Right < mp.Points[0].X) map.CenterDegrees = mp.Points[0];
                        if (maprect.Top > mp.Points[0].Y) map.CenterDegrees = mp.Points[0];
                        if (maprect.Bottom < mp.Points[0].Y) map.CenterDegrees = mp.Points[0];
                    };    
                LL.Add(mp);
            };            
        }

        private void DrawTimed(PT0102 obj)
        {
            RectangleF maprect = map.MapBoundsRectDegrees;

            System.Text.RegularExpressions.Regex rx = new System.Text.RegularExpressions.Regex(@"\d");
            System.Text.RegularExpressions.Match mx = rx.Match(obj.ID);
            int v = -1;
            if (mx.Success) v = 0;
            while (mx.Success) { v = byte.Parse(mx.Value); mx = mx.NextMatch(); };
            int symbol = v == -1 ? 10 : v;

            bool addNode = true;
            bool addText = true;
            bool addTail = true;
            bool addList = true;

            // Points
            if (LL.ObjectsCount > 0)
                for (int i = 0; i < LL.ObjectsCount; i++)
                {
                    NaviMapNet.MapPoint mp = (NaviMapNet.MapPoint)LL[i];
                    if (mp.Name == obj.ID)
                    {
                        addNode = false;
                        mp.Points[0].Y = obj.Lat;
                        mp.Points[0].X = obj.Lon;
                    };
                    if ((logSelect.ObjectsCount != 0) && (logSelect[0].Name == mp.Name))
                        logSelect[0].Points[0] = mp.Points[0];
                };

            // Text
            if (L2.ObjectsCount > 0)
                for (int i = 0; i < L2.ObjectsCount; i++)
                {
                    NaviMapNet.MapPoint mp = (NaviMapNet.MapPoint)L2[i];
                    if (mp.Name == obj.ID)
                    {
                        addText = false;
                        mp.Points[0].Y = obj.Lat;
                        mp.Points[0].X = obj.Lon;
                    };
                    if ((logSelect.ObjectsCount != 0) && (logSelect[0].Name == mp.Name))
                        logSelect[0].Points[0] = mp.Points[0];
                };

            // Tail
            if (ndt.Checked && (logTail.ObjectsCount > 0))
                for (int i = 0; i < logTail.ObjectsCount; i++)
                {
                    NaviMapNet.MapPolyLine ml = (NaviMapNet.MapPolyLine)logTail[i];
                    if (ml.Name == obj.ID)
                    {
                        addTail = false;
                        if ((ml.Points[ml.Points.Length - 1].X != obj.Lon) && (ml.Points[ml.Points.Length - 1].Y != obj.Lat))
                        {
                            List<PointF> points = new List<PointF>();
                            points.AddRange(ml.Points);
                            points.Add(new PointF((float)obj.Lon, (float)obj.Lat));
                            float ttl = 0;
                            for (int p = points.Count - 1; p > 0; p--)
                            {
                                ttl += GetLengthMetersC(points[p - 1].Y, points[p - 1].X, points[p].Y, points[p].X, false);
                                if (ttl > (int)PigTail.Value)
                                {
                                    for (int pp = p - 1; pp >= 0; pp--)
                                        points.RemoveAt(pp);
                                    p = 0;
                                };
                            };
                            ml.Points = points.ToArray();
                        };
                    };
                };

            // list
            if (GVIEW.Items.Count > 0)
                for (int i = 0; i < GVIEW.Items.Count; i++)
                {
                    if (GVIEW.Items[i].Name == obj.ID)
                    {
                        addList = false;
                        string Name = GetNamebyID(obj.ID);
                        string[] si = GetLVIEWText(obj, Name);
                        for (int six = 0; six < si.Length; six++)
                            GVIEW.Items[i].SubItems[six].Text = si[six];
                        (GVIEW.Items[i] as ListViewItemObj).data = obj;
                    };
                };            

            if (addNode)
            {                                
                NaviMapNet.MapPoint mp = new NaviMapNet.MapPoint(obj.Lat, obj.Lon);
                mp.UserData = obj;
                mp.Name = obj.ID;
                mp.SizePixels = new Size(0, 0);
                {
                    mp.DrawText = true;
                    mp.Text = obj.ID;
                    mp.TextFont = new Font("Arial", 9, FontStyle.Bold);
                    mp.TextBrush = new SolidBrush(Color.Navy);
                    SizeF sf = gDef.MeasureString(mp.Name, mp.TextFont);
                    mp.TextOffset = new Point((int)(-sf.Width / 2), 8);
                };
                L2.Add(mp);
            };

            if (addText)
            {
                NaviMapNet.MapPoint mp = new NaviMapNet.MapPoint(obj.Lat, obj.Lon);
                mp.UserData = obj;
                mp.Name = obj.ID;
                mp.SizePixels = new Size(16, 16);
                mp.Text = symbol.ToString();
                if (symbol == 10) mp.Text = " ";

                mp.Color = gColors[symbol];
                {
                    mp.DrawText = true;
                    mp.TextFont = new Font("Arial", 9, FontStyle.Bold);
                    mp.TextBrush = new SolidBrush(symbol == 4 || symbol == 9 ? Color.Black : Color.White);
                    SizeF sf = gDef.MeasureString(symbol.ToString(), mp.TextFont);
                    mp.TextOffset = new Point((int)(-sf.Width / 2) + 1, -7);
                };
                if (MoveMapClick.Checked)
                    if (LL.ObjectsCount == 0)
                    {
                        if (map.MapBoundsRectDegrees.Left > mp.Points[0].X) map.CenterDegrees = mp.Points[0];
                        if (map.MapBoundsRectDegrees.Right < mp.Points[0].X) map.CenterDegrees = mp.Points[0];
                        if (map.MapBoundsRectDegrees.Top > mp.Points[0].Y) map.CenterDegrees = mp.Points[0];
                        if (map.MapBoundsRectDegrees.Bottom < mp.Points[0].Y) map.CenterDegrees = mp.Points[0];
                    };
                LL.Add(mp);
            };

            if (addTail)
            {
                NaviMapNet.MapPolyLine ml = new NaviMapNet.MapPolyLine();
                ml.UserData = obj;
                ml.Name = obj.ID;
                ml.Color = Color.FromArgb(200, gColors[symbol]);
                ml.Points = new PointF[] { new PointF((float)obj.Lon, (float)obj.Lat) };
                ml.Width = 4;
                logTail.Add(ml);
            };

            if(addList)
            {
                string Name = GetNamebyID(obj.ID);
                ListViewItemObj lvi = new ListViewItemObj(GetLVIEWText(obj, Name), obj);
                lvi.Name = obj.ID;
                GVIEW.Items.Add(lvi);
            };                        
        }              

        private void GVIEW_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (GVIEW.SelectedItems.Count == 0) return;
            if (LL.ObjectsCount == 0) return;

            string ID = GVIEW.SelectedItems[0].Name;
            for (int i = 0; i < LL.ObjectsCount; i++)
                if (LL[i].Name == ID)
                {
                    SelectOnLogMap(i);
                    return;
                };
        }

        private void GVIEW_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (GVIEW.SelectedItems.Count == 0) return;
            if (LL.ObjectsCount == 0) return;
            string ID = GVIEW.SelectedItems[0].Name;
            for (int i = 0; i < LL.ObjectsCount; i++)
                if (LL[i].Name == ID)
                {
                    map.CenterDegrees = LL[i].Points[0];
                    return;
                };
        }

        private void clearOnlineToolStripMenuItem_Click(object sender, EventArgs e)
        {           
            mapTail.Clear();
            mapSelect.Clear();
            ML.Clear();
            M2.Clear();

            toolStripStatusLabel7.Text = "";
            LVIEW.Items.Clear();
            RefreshMapObjects();
        }

        private void clearLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            logTail.Clear();
            logSelect.Clear();
            LL.Clear();
            L2.Clear();

            GVIEW.Items.Clear();
            RefreshMapObjects();
        }        

        private void pTTToolStripMenuItem_Click(object sender, EventArgs e)
        {
            long pg = (long)PigTail.Value;
            Utils.InputNumericBox("Длина хвоста", "Новая длина хвоста в метрах:", "от 150 до 99999 м", 150, 99999, ref pg);
            PigTail.Value = (int)pg;
        }

        private void очиститьКартуToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            logTail.Clear();
            logSelect.Clear();
            LL.Clear();
            L2.Clear();

            mapTail.Clear();
            mapSelect.Clear();
            ML.Clear();
            M2.Clear();

            toolStripStatusLabel7.Text = "";
            LVIEW.Items.Clear();
            GVIEW.Items.Clear();

            RefreshMapObjects();
        }

        private void перерисоватьОбъектыToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RefreshMapObjects();
        }

        private void nnmm(object sender, EventArgs e)
        {
            ToolStripMenuItem mi = (sender as ToolStripMenuItem);
            mi.Checked = !mi.Checked;

            logSelect.Visible = nS.Checked;
            logTail.Visible = nT.Checked;
            L2.Visible = n1.Checked;
            LL.Visible = n0.Checked;

            mapSelect.Visible = mS.Checked;
            mapTail.Visible = mT.Checked;
            M2.Visible = m1.Checked;
            ML.Visible = m0.Checked;

            mapLays.Checked = logSelect.Visible || logTail.Visible || L2.Visible || LL.Visible || mapSelect.Visible || mapTail.Visible || ML.Visible || M2.Visible;

            RefreshMapObjects();
        }

        private void mapVect_Click(object sender, EventArgs e)
        {
            mapVect.Checked = !mapVect.Checked;
            map.DrawVector = mapVect.Checked;
            mapLays.Enabled = map.DrawMap && map.DrawVector;

            RefreshMapObjects();
        }

        private void toolStripStatusLabel4_DoubleClick(object sender, EventArgs e)
        {

        }

        private void toolStripStatusLabel2_Click(object sender, EventArgs e)
        {
            double lat = map.CenterDegreesLat;
            double lon = map.CenterDegreesLon;
            if (Utils.InputLatLon("Навигация по карте", "Введите координаты центра карты:", ref lat, ref lon) == DialogResult.OK)
                map.CenterDegrees = new PointF((float)lon, (float)lat);
        }

        private void inCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            inCToolStripMenuItem.Checked = !inCToolStripMenuItem.Checked;
            map.InvertBackground = inCToolStripMenuItem.Checked;
        }

        private void toolStripDropDownButton1_DropDownOpening(object sender, EventArgs e)
        {
            toolStripMenuItem4.Checked = map.DrawTilesBorder;
            toolStripMenuItem5.Checked = map.DrawTilesXYZ;
        }

        private void видToolStripMenuItem1_DropDownOpening(object sender, EventArgs e)
        {
            ShZm.Checked = map.ShowZooms;
            ShSc.Checked = map.ShowScale;
            ShCC.Checked = map.ShowCross;
        }

        private void aPRSCodeGeneratorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Regex rx = new Regex(@"[A-Z\d\-]{0,8}");

            string tmps = "TEMP-01";
            string txt = "Логин:";
            if ((!String.IsNullOrEmpty(MainEventID)) && (MainEventID != "*"))
                txt = "Логин (фильтр: +ev/" + MainEventID + "):";

            if(Utils.InputBox("Генератор кодов APRS клиента", txt, rx, ref tmps) != DialogResult.OK) return;
            Utils.CopyBox("Генератор кодов APRS клиента", "Пароль для `" + tmps + "`:", 300, CSChecksum(tmps).ToString());
        }

        private static int CSChecksum(string callsign)
        {
            if (callsign == null) return 99999;
            if (callsign.Length == 0) return 99999;
            if (callsign.Length > 10) return 99999;

            //int stophere = callsign.IndexOf("-");
            //if (stophere > 0) callsign = callsign.Substring(0, stophere);
            string realcall = callsign.ToUpper();
            while (realcall.Length < 10) realcall += " ";

            // initialize hash 
            int hash = 0x2443; // RTTS with no 5: (5)2 (5)4 (5)4 (5)3 // 73e2
            int i = 0;
            int len = realcall.Length;

            // hash callsign two bytes at a time 
            while (i < len)
            {
                hash ^= (int)(realcall.Substring(i, 1))[0] << 8;
                hash ^= (int)(realcall.Substring(i + 1, 1))[0];
                i += 2;
            }
            // mask off the high bit so number is always positive 
            return hash & 0x7fff;
        }

        private void mtAPRS_Click(object sender, EventArgs e)
        {
            SelProto(2);
        }

        private void getGPXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string since = DateTime.Now.AddHours(-7).ToString("yyyy-MM-ddTHH:mm:ss");
            string till = DateTime.Now.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss");
            string Event = MainEventID;
            string ID = "USER-01";
            Utils.InputBox("Запрос GPX спортсмена с сервера", "Введите начальное время:", 180, null, ref since);
            Utils.InputBox("Запрос GPX спортсмена с сервера", "Введите конечное время:", 180, null, ref till);
            Utils.InputBox("Запрос GPX спортсмена с сервера", "Введите EventID:", ref Event);
            Utils.InputBox("Запрос GPX спортсмена с сервера", "Введите ID спортсмена:", ref ID);
            string url = MainServerURL + "vgpx?Since=" + since + "&Till=" + till + "&Event=" + Event + "&ID=" + ID;

            SaveFileDialog ofd = new SaveFileDialog();
            ofd.OverwritePrompt = false;
            ofd.Title = "Выберите куда сохранить";
            ofd.DefaultExt = ".gpx";
            ofd.FileName = "MyRec.gpx";
            ofd.Filter = "GPX Files (*.gpx)|*.gpx|All types (*.*)|*.*";
            if (ofd.ShowDialog() != DialogResult.OK)
            {
                ofd.Dispose();
                return;
            };
            string filename = ofd.FileName;
            ofd.Dispose();

            try
            {
                HttpWebRequest hwr = (HttpWebRequest)HttpWebRequest.Create(url);
                hwr.UserAgent = "Mozilla/5.0 (Windows NT 5.1; rv:52.0) Gecko/20100101 Firefox/52.0";
                hwr.Timeout = 300 * 1000;

                HttpWebResponse wr = (HttpWebResponse)hwr.GetResponse();
                System.IO.Stream rs = wr.GetResponseStream();
                StreamReader sr = new StreamReader(rs, System.Text.Encoding.UTF8);
                string data = sr.ReadToEnd();
                sr.Close();
                rs.Close();
                wr.Close();

                FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
                sw.Write(data);
                sw.Close();
                fs.Close();

                MessageBox.Show("Данные получены и сохранены в файл!", "Запрос данных", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
        }

        private void clrP_Click(object sender, EventArgs e)
        {
            textBox1.Clear();
        }        
    }

    public class Incom: ax25kiss.AX25Handler
    {
        private Form2 parent;

        public Incom(Form2 parent)
        {
            this.parent = parent;
        }

        public void handlePacket(ax25kiss.Packet packet)
        {
            string pck = packet.ToString();
            parent.Invoke(new Form2.PassAPRSCommand(parent.PassAPRSComm), new object[] { pck });

            Buddie b = APRSData.ParseAPRSPacket(packet.ToString());
            if ((b != null) && b.PositionIsValid && (!String.IsNullOrEmpty(b.name)))
            {
                string body = "{ID:'" + b.name + "',DT:'" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + "',Lat:" + b.lat.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",Lon:" + b.lon.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",Alt:0.0,Hdg:" + b.course.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",Spd:" + b.speed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
                string fbody = "RTT@A02J1:" + body.Length.ToString("000") + "/" + body + "&&";
                byte[] arr = System.Text.Encoding.ASCII.GetBytes(fbody);
                RTTPacket trrp = RTTPacket.FromBytes(arr, 0);
                PT0102 d = PT0102.FromJSON(trrp.datatext);
                d.ORIGIN = pck;
                d.Addit = b.IconSymbol;
                parent.Invoke(new Form2.PassMapObjectsDel(parent.PassMapObjects), new object[] { new PT0102[] { d } });
            };
        }
    }

    [XmlRoot("RemoteConfig")]
    public class tsRemoteConfig
    {
        public string http;
        public string udp;
        public string tcp;
        public string ais;
        public string aprs;
        public string frs;
        public string url;
        public string Event;
        public string EvName;
        public string ID;
        public string IDName;

        [XmlIgnore]
        public string APRSuser;
        [XmlIgnore]
        public string APRSpass;
        [XmlIgnore]
        public string APRSfilter;
        [XmlIgnore]
        public string AGWRadio;

        [XmlIgnore]
        public bool APRSOnly = false;
        [XmlIgnore]
        public bool AGWOnly = false;
        [XmlIgnore]
        public bool KISSOnly = false;
    }

    [Serializable]
    public struct PT0102
    {

        public string ORIGIN;

        public string IMEI;
        public string ID;
        public string Event;
        public DateTime DT;
        public float Lat;
        public float Lon;
        public float Alt;
        public float Hdg;
        public float Spd;
        public float Dist;

        //public string Filter;

        public object Addit;

        public static PT0102 FromJSON(string text)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<PT0102>(text);
        }

        public bool FixOk
        {
            get
            {
                return (Lat != 0) && (Lon != 0);
            }
        }

        public class PTComparer : IComparer<PT0102>
        {
            public int Compare(PT0102 a, PT0102 b)
            {
                return a.DT.ToUniversalTime().CompareTo(b.DT.ToUniversalTime());
            }
        }

        public static void Sort(PT0102[] array)
        {
            Array.Sort(array, new PTComparer());
        }

        public override string ToString()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }
    }

    public class ListViewItemObj : ListViewItem
    {
        public PT0102 data;
        public ListViewItemObj(string[] items, PT0102 data)
            : base(items)
        {
            this.data = data;
        }
    }

    [Serializable]
    public class eventconfig : XMLSaved<eventconfig>
    {
        [XmlElement("event")]
        public string id;
        public string name;
        public string comment;
        public double lat = 55.5;
        public double lon = 37.5;
        [XmlArrayItem("user")]
        public List<eventuser> users = new List<eventuser>();
    }

    [Serializable]
    public class eventuser
    {
        public string id;
        public string name;
        public string imei;
        public string comment;
    }
}