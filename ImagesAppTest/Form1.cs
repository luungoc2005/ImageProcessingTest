﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace ImagesAppTest
{
    public partial class Form1 : Form
    {
        List<string> listFiles;

        public Form1()
        {
            InitializeComponent();
            listFiles = new List<string>();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            dlg1.ShowDialog();
            if (dlg1.SelectedPath != "")
            {
                listFiles.Clear();
                listBox1.Items.Clear();
                foreach (var filename in Directory.GetFiles(dlg1.SelectedPath))
	            {
                    listFiles.Add(filename);
                    listBox1.Items.Add(Path.GetFileName(filename));
	            }
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if ((listBox1.SelectedIndex > -1) && File.Exists(listFiles[listBox1.SelectedIndex]))
                {
                    pictureBox1.Load(listFiles[listBox1.SelectedIndex]);
                }
            }
            catch
            {
            }
        }
    }
}