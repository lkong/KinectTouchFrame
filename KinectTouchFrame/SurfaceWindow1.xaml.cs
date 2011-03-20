using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Surface.Presentation;
using Microsoft.Surface.Presentation.Controls;
using Microsoft.Surface.Presentation.Input;
using UserTracker.net;
using System.Threading;
namespace KinectTouchFrame
{
    /// <summary>
    /// Interaction logic for SurfaceWindow1.xaml
    /// </summary>
    public partial class SurfaceWindow1 : SurfaceWindow
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public SurfaceWindow1()
        {
            InitializeComponent();
            watchingWindow = new KinectWindow();
            watchingWindow.Show();
            WindowsWidth = (int)this.Width;
            WindowsHeight = (int)this.Height;
          
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
        }


        private void SurfaceWindow_TouchDown(object sender, TouchEventArgs e)
        {
            //test
            Ellipse handRange = new Ellipse();
            handRange.Fill = System.Windows.Media.Brushes.DeepPink;
            handRange.Width = 100;
            handRange.Height = 100;
            TranslateTransform Pos = new TranslateTransform();
            Pos.X = (int)e.GetTouchPoint(this).Position.X;//-this.Width/2;
            Pos.Y = (int)e.GetTouchPoint(this).Position.Y;//-this.Height/2;
            handRange.RenderTransform = null;
            handRange.RenderTransform = Pos;
            // HandRange.
            DrawingCanvas.Children.Add(handRange);

            //testover


            int X = (int)e.GetTouchPoint(this).Position.X;
            int Y = (int)e.GetTouchPoint(this).Position.Y;
            watchingWindow.getHands((int)this.Width, (int)this.Height);
            Console.Out.WriteLine(watchingWindow.GetUser(X, Y, (int)this.Width, (int)this.Height));
            Dictionary<String, System.Drawing.Point> Hands = watchingWindow.getHands(WindowsWidth, WindowsHeight);
            foreach (KeyValuePair<String, System.Drawing.Point> pair in Hands)
            {
                System.Drawing.Point HandPoint = pair.Value;
                String HandString = pair.Key;
                //Console.Out.WriteLine("Hand in " + HandPoint.X + " , " + HandPoint.Y);
                if (HandPoint.X != 0 && HandPoint.Y != 0)
                    if (HandPoint.X < (int)this.Width && HandPoint.Y < (int)this.Height)
                    {
                        Ellipse HandRange = new Ellipse();
                        HandRange.Fill = System.Windows.Media.Brushes.DarkBlue;
                        HandRange.Width = 100;
                        HandRange.Height = 100;
                        TranslateTransform pos = new TranslateTransform();
                        pos.X = HandPoint.X;//-this.Width/2;
                        pos.Y = HandPoint.Y;// -this.Height / 2;
                        HandRange.RenderTransform = null; 
                        HandRange.RenderTransform = pos;
                        // HandRange.
                        
                        DrawingCanvas.Children.Add(HandRange);
                        Console.Out.WriteLine(HandString+" in " + HandPoint.X + " , " + HandPoint.Y);
                    }
            }
        }
        private static KinectWindow watchingWindow;
        private static int WindowsWidth;
        private static int WindowsHeight;
    }
}