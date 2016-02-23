using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.ComponentModel;

namespace DySenseKinectV2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Hide();

            string[] args = Environment.GetCommandLineArgs();

            // Have to create/run sensor driver on a separate thread so can process the event queue on the main thread.
            // TODO - try to get away from using a window at all. 
            BackgroundWorker programWorker = new BackgroundWorker();
            programWorker.DoWork += programWorker_DoWork;
            programWorker.RunWorkerAsync(args);
        }

        void programWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            string[] args = (string[])(e.Argument);
            Program.Main(args);
        }
    }

}
