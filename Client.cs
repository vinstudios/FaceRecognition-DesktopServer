using System.Windows.Forms;

namespace facerecognition
{
    public partial class Client : Form
    {
        public Client(string title)
        {
            InitializeComponent();
            Text = title;
        }

        private void Client_Load(object sender, System.EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
        }
    }
}
