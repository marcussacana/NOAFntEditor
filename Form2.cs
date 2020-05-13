using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NOAFntEditor
{
    public partial class Form2 : Form
    {
        public uint Value => (uint)numericUpDown1.Value;
        public Form2(string Title, int Default) : this() {
            Text = Title;
            if (Title.Length > 50) {
                timer1.Enabled = true;
                Text += " ";
            }

            numericUpDown1.Value = Default;
        }
        public Form2()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Text = Text.Substring(1) + Text.Substring(0, 1);
        }
    }
}
