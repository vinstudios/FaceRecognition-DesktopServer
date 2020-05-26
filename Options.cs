using System;
using System.Windows.Forms;

namespace facerecognition
{
    public partial class Options : Form
    {
        public Options()
        {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Dispose();
        }

        private void Options_Load(object sender, EventArgs e)
        {
            numericUpDown1.Value = (decimal) Server.FaceDetectionThreshold;
            numericUpDown2.Value = (decimal)Server.FARValue;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Server.FaceDetectionThreshold = (float)numericUpDown1.Value;
            Server.FARValue = (float)numericUpDown2.Value;
            this.Dispose();
        }
    }
}
