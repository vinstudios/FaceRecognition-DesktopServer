using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Luxand;

namespace facerecognition
{

    public struct TFaceRecord
    {
        public byte [] Template; //Face Template;
        public FSDK.TFacePosition FacePosition;
        public FSDK.TPoint[] FacialFeatures; //Facial Features;
        public string ImageFileName;
        public FSDK.CImage image;
        public FSDK.CImage faceImage;
    }

    public partial class Server : Form
    {
        public static float FaceDetectionThreshold = 3;
        public static float FARValue = 100;
        
        public static List<TFaceRecord> FaceList;
        public static List<string> FaceName;
                      
        static ImageList imageList1;

        private readonly Dictionary<int, TcpClient> tcpClients = new Dictionary<int, TcpClient>();
        private List<Thread> threadClients = new List<Thread>();
        private TcpListener listener;
        //private TcpClient client;
        private string ipAddress = string.Empty;
 
        public Server()
        {
            InitializeComponent();
        }

        private void Server_Load(object sender, EventArgs e)
        {
            Text = Application.ProductName;
            FaceList = new List<TFaceRecord>();
            FaceName = new List<string>();
            imageList1 = new ImageList();
            threadClients = new List<Thread>();

            Size size100x100 = new Size();
            size100x100.Height = 100;
            size100x100.Width = 100;
            imageList1.ImageSize = size100x100;
            imageList1.ColorDepth = ColorDepth.Depth24Bit;

            listView1.OwnerDraw = false;
            listView1.View = View.LargeIcon;
            listView1.LargeImageList = imageList1;

            add("Initializing FaceSDK...");

            if (FSDK.FSDKE_OK != FSDK.ActivateLibrary("fVrFCzYC5wOtEVspKM/zfLWVcSIZA4RNqx74s+QngdvRiCC7z7MHlSf2w3+OUyAZkTFeD4kSpfVPcRVIqAKWUZzJG975b/P4HNNzpl11edXGIyGrTO/DImoZksDSRs6wktvgr8lnNCB5IukIPV5j/jBKlgL5aqiwSfyCR8UdC9s="))
            {
                MessageBox.Show("Please run the License Key Wizard (Start - Luxand - FaceSDK - License Key Wizard)", "Error activating FaceSDK", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            if (FSDK.InitializeLibrary() != FSDK.FSDKE_OK)
                MessageBox.Show("Error initializing FaceSDK!", "Error");

            add("FaceSDK Initialized");

            string hostname = Dns.GetHostName();
            IPHostEntry iphost = Dns.Resolve(hostname);
            IPAddress[] addresses = iphost.AddressList;
            
            ipAddress = addresses[addresses.Length - 1].ToString();
            add("Starting Face Recognition server @ " + ipAddress);
            
            try
            {
                listener = new TcpListener(IPAddress.Parse(ipAddress), 5005);
                listener.Start();

                Text += " @ " + ipAddress; 
                add("Waiting for client to connect...");
                add("Don't forget to enroll a faces to the database.");
            }
            catch (Exception ex)
            {
                add(ex.Message);
            }

            Task.Run(() =>
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Thread thread = new Thread(handleClients);
                    tcpClients.Add(thread.ManagedThreadId, client);
                    threadClients.Add(thread);
                    thread.Start(thread.ManagedThreadId);
                }
                catch (Exception ex)
                {
                    add(ex.Message);
                }
            });
        }

        private void handleClients(object threadId)
        {
            int clientId = (int)threadId;
            TcpClient tcpClient = tcpClients[clientId];
            
            string remoteEndPoint = tcpClient.Client.RemoteEndPoint.ToString();
            string clientIp = remoteEndPoint.Substring(0, remoteEndPoint.IndexOf(":"));
            string clientPort = remoteEndPoint.Substring(remoteEndPoint.IndexOf(":") + 1);
            add("Client " + remoteEndPoint + " connected");

            Client client = new Client("Client " + clientIp + " @ " + clientPort);
            client = new Client("Client " + clientIp + " @ " + clientPort);
            this.BeginInvoke((Action)(() => client.Show()));
            
            NetworkStream stream = tcpClient.GetStream();
            string clientData = string.Empty;
            Byte[] bytes = new Byte[256];
            int i;
            bool init = false;

            try
            {
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    if (!init) clientData += Encoding.ASCII.GetString(bytes, 0, i);
                    
                    if (clientData.Contains("<EOF>") && !init)
                    {
                        init = true;
                        add("Hello");
                        string result = clientData.Substring(0, clientData.IndexOf("<EOF>"));
                        clientData = string.Empty;
                        byte[] imageBytes = Convert.FromBase64String(result);
                        MemoryStream ms = new MemoryStream(imageBytes, 0, imageBytes.Length);
                        ms.Position = 0;

                        Image img = Image.FromStream(ms);
                        if (client == null || client.IsDisposed)
                        {
                            client = new Client("Client " + clientIp + " @ " + clientPort);
                            this.BeginInvoke((Action)(() => client.Show()));
                        }
                        this.BeginInvoke((Action)(() => client.label1.Visible = false));
                        this.BeginInvoke((Action)(() => client.pictureBox1.Image = img));

                        if (FaceList.Count == 0)
                        {
                            add("Please enroll faces first");
                            init = false;
                        }
                        else
                        {
                            TFaceRecord fr = new TFaceRecord();
                            fr.FacePosition = new FSDK.TFacePosition();
                            fr.FacialFeatures = new FSDK.TPoint[FSDK.FSDK_FACIAL_FEATURE_COUNT];
                            fr.Template = new byte[FSDK.TemplateSize];
                            fr.image = new FSDK.CImage(img);
                            img.Dispose();

                            fr.FacePosition = fr.image.DetectFace();

                            if (0 == fr.FacePosition.w)
                            {
                                add("No faces found. Try to lower the Minimal Face Quality parameter in the Options dialog box.");
                                init = false;
                            }
                            else
                            {
                                fr.faceImage = fr.image.CopyRect((int)(fr.FacePosition.xc - Math.Round(fr.FacePosition.w * 0.5)), (int)(fr.FacePosition.yc - Math.Round(fr.FacePosition.w * 0.5)), (int)(fr.FacePosition.xc + Math.Round(fr.FacePosition.w * 0.5)), (int)(fr.FacePosition.yc + Math.Round(fr.FacePosition.w * 0.5)));
                                bool eyesDetected = false;

                                try
                                {
                                    fr.FacialFeatures = fr.image.DetectEyesInRegion(ref fr.FacePosition);
                                    eyesDetected = true;
                                }
                                catch
                                {
                                    add("Detecting eyes failed.");
                                }

                                if (eyesDetected)
                                {
                                    fr.Template = fr.image.GetFaceTemplateInRegion(ref fr.FacePosition); // get template with higher precision
                                    float Threshold = 0.0f;
                                    FSDK.GetMatchingThresholdAtFAR(FARValue / 100, ref Threshold);

                                    int MatchedCount = 0;
                                    int FaceCount = FaceList.Count;

                                    float[] Similarities = new float[FaceCount];

                                    for (int x = 0; x < FaceList.Count; x++)
                                    {
                                        float Similarity = 0.0f;
                                        TFaceRecord CurrentFace = FaceList[x];

                                        FSDK.MatchFaces(ref fr.Template, ref CurrentFace.Template, ref Similarity);

                                        if (Similarity >= Threshold)
                                        {
                                            Similarities[MatchedCount] = Similarity;
                                            ++MatchedCount;
                                        }
                                    }

                                    if (MatchedCount != 0)
                                    {
                                        float finalResult = Similarities.Max();

                                        if (finalResult * 100 < 95.0)
                                        {
                                            SendBackToClient(tcpClient, "R=NONE;");
                                            add("No morethan 95.0% matches found in database faces");
                                            init = false;
                                        }
                                        else
                                        {

                                            int index = 0;
                                            for (int x = 0; x < MatchedCount; x++)
                                            {
                                                if (Similarities[x] == finalResult)
                                                {
                                                    index = x;
                                                }
                                            }
                                            SendBackToClient(tcpClient, "R=" + FaceName[index]);
                                            init = false;
                                        }
                                    }
                                    else
                                    {
                                        SendBackToClient(tcpClient, "R=NONE;");
                                        add("No matches found. You can try to increase the FAR parameter in the Options dialog box.");
                                        init = false; 
                                    }
                                }
                                else
                                {
                                    add("No eyes detected in photos.");
                                    init = false;
                                }
                            }
                        }
                    }
                }

                tcpClients.Remove(clientId);
                tcpClient.Client.Shutdown(SocketShutdown.Both);
                tcpClient.Client.Close();
                tcpClient.Client.Dispose();

                int idx = 0;
                for (int x = 0; x < threadClients.Count(); x++)
                    if (threadClients[x].ManagedThreadId == clientId) idx = x;
                threadClients.RemoveAt(idx);
                
                add("Client " + remoteEndPoint + " disconnected");
                
                if (client.Visible)
                {
                    BeginInvoke((Action)(() => client.Close()));
                }
            }
            catch
            {
                add("Client " + remoteEndPoint + " error");
            }
        }
 
        private void SendBackToClient(TcpClient client, string text)
        {
            try
            {
                byte[] buffer = Encoding.ASCII.GetBytes(text);
                NetworkStream stream = client.GetStream();
                stream.Write(buffer, 0, buffer.Length);
                //stream.Flush();
            }
            catch (Exception ex)
            {
                add("Client " + client.Client.RemoteEndPoint.ToString() + " SEND ERROR: " + ex.Message);
            }

        }

        private void add(string text)
        {
            if (InvokeRequired)
            {
                this.BeginInvoke((Action)(() => richTextBox1.AppendText(text + "\n")));
            }
            else
            {
                richTextBox1.AppendText(text + "\n");
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.listView1.SelectedIndices.Count > 0){
                Image img = FaceList[listView1.SelectedIndices[0]].image.ToCLRImage();
                //pictureBox1.Height = img.Height;
                //pictureBox1.Width = img.Width;
                pictureBox1.Image = img;
                img.Dispose();
                //pictureBox1.Refresh();
                
                //Graphics gr = pictureBox1.CreateGraphics();
                //gr.DrawRectangle(Pens.LightGreen, FaceList[listView1.SelectedIndices[0]].FacePosition.xc - FaceList[listView1.SelectedIndices[0]].FacePosition.w / 2, FaceList[listView1.SelectedIndices[0]].FacePosition.yc - FaceList[listView1.SelectedIndices[0]].FacePosition.w/2, FaceList[listView1.SelectedIndices[0]].FacePosition.w, FaceList[listView1.SelectedIndices[0]].FacePosition.w);

                //for (int i = 0; i < 2; ++i){
                //    FSDK.TPoint tp = FaceList[listView1.SelectedIndices[0]].FacialFeatures[i];
                //    gr.DrawEllipse(Pens.Blue, tp.x, tp.y, 3, 3);
                //}                
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        
        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Options frmOptions = new Options();
            frmOptions.Show();
        }

        private void enrollFacesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "JPEG (*.jpg)|*.jpg|Windows bitmap (*.bmp)|*.bmp|All files|*.*";
            dlg.Multiselect = true;
            
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    //Assuming that faces are vertical (HandleArbitraryRotations = false) to speed up face detection
                    FSDK.SetFaceDetectionParameters(false, true, 384);
                    FSDK.SetFaceDetectionThreshold((int)FaceDetectionThreshold);
                    
                    foreach (string fn in dlg.FileNames)
                    {
                        string name = Path.GetFileNameWithoutExtension(fn);
                        
                        TFaceRecord fr = new TFaceRecord();
                        fr.ImageFileName = fn;
                        fr.FacePosition = new FSDK.TFacePosition();
                        fr.FacialFeatures = new FSDK.TPoint[2];
                        fr.Template = new byte[FSDK.TemplateSize];
 
                        fr.image = new FSDK.CImage(fn);
                                               
                        fr.FacePosition = fr.image.DetectFace();

                        if (0 == fr.FacePosition.w)
                            if (dlg.FileNames.Length <= 1)
                                MessageBox.Show("No faces found. Try to lower the Minimal Face Quality parameter in the Options dialog box.", "Enrollment error");
                            else
                                add(name + " face enroll failed.\nTry to lower the Minimal Face Quality parameter in the Options dialog box.");
                        else
                        {
                            fr.faceImage = fr.image.CopyRect((int)(fr.FacePosition.xc - Math.Round(fr.FacePosition.w * 0.5)), (int)(fr.FacePosition.yc - Math.Round(fr.FacePosition.w * 0.5)), (int)(fr.FacePosition.xc + Math.Round(fr.FacePosition.w * 0.5)), (int)(fr.FacePosition.yc + Math.Round(fr.FacePosition.w * 0.5)));

                            try
                            {
                                fr.FacialFeatures = fr.image.DetectEyesInRegion(ref fr.FacePosition);
                            }
                            catch (Exception ex2)
                            {
                                MessageBox.Show(ex2.Message, "Error detecting eyes.");
                            }

                            try
                            {
                                fr.Template = fr.image.GetFaceTemplateInRegion(ref fr.FacePosition); // get template with higher precision
                            }
                            catch (Exception ex2)
                            {
                                MessageBox.Show(ex2.Message, "Error retrieving face template.");
                            }

                            FaceList.Add(fr);
                            FaceName.Add(name);

                            imageList1.Images.Add(fr.faceImage.ToCLRImage());
                            listView1.Items.Add((imageList1.Images.Count - 1).ToString(), name,/**fn.Split('\\')[fn.Split('\\').Length - 1],*/ imageList1.Images.Count - 1);

                            add(name + " enrolled");
                            listView1.SelectedIndices.Clear();
                            listView1.SelectedIndices.Add(listView1.Items.Count - 1);
                        }
                    }
                 
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message.ToString(), "Exception");
                }
            }
        }

        private void clearDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FaceList.Clear();
            listView1.Items.Clear();
            imageList1.Images.Clear();
            pictureBox1.Width = 0;
            pictureBox1.Height = 0;
            GC.Collect();
        }

        private void matchFaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FaceList.Count == 0)
                MessageBox.Show("Please enroll faces first", "Error");
            else {
                OpenFileDialog dlg = new OpenFileDialog();
                dlg.Filter = "JPEG (*.jpg)|*.jpg|Windows bitmap (*.bmp)|*.bmp|All files|*.*";
                
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string fn = dlg.FileNames[0];
                        TFaceRecord fr = new TFaceRecord();
                        fr.ImageFileName = fn;
                        fr.FacePosition = new FSDK.TFacePosition();
                        fr.FacialFeatures = new FSDK.TPoint[FSDK.FSDK_FACIAL_FEATURE_COUNT];
                        fr.Template = new byte[FSDK.TemplateSize];

                        try
                        {
                            fr.image = new FSDK.CImage(fn);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Error loading file");
                        }

                        fr.FacePosition = fr.image.DetectFace();
                        if (0 == fr.FacePosition.w)
                            MessageBox.Show("No faces found. Try to lower the Minimal Face Quality parameter in the Options dialog box.", "Enrollment error");
                        else
                        {
                            fr.faceImage = fr.image.CopyRect((int)(fr.FacePosition.xc - Math.Round(fr.FacePosition.w * 0.5)), (int)(fr.FacePosition.yc - Math.Round(fr.FacePosition.w * 0.5)), (int)(fr.FacePosition.xc + Math.Round(fr.FacePosition.w * 0.5)), (int)(fr.FacePosition.yc + Math.Round(fr.FacePosition.w * 0.5)));

                            bool eyesDetected = false;
                            try
                            {
                                fr.FacialFeatures = fr.image.DetectEyesInRegion(ref fr.FacePosition);
                                eyesDetected = true;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message, "Error detecting eyes.");
                            }

                            if (eyesDetected)
                            {
                                fr.Template = fr.image.GetFaceTemplateInRegion(ref fr.FacePosition); // get template with higher precision
                            }
                        }
                        
                        Results frmResults = new Results();
                        frmResults.Go(fr);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Can't open image(s) with error: " + ex.Message.ToString(), "Error");
                    }

                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            Environment.Exit(0);
        }
    }
}
