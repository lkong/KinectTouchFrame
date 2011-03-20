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
        }

        private void SurfaceWindow_TouchDown(object sender, TouchEventArgs e)
        {
            int X = (int)e.GetTouchPoint(this).Position.X;
            int Y = (int)e.GetTouchPoint(this).Position.Y;
            watchingWindow.getHands((int)this.Width, (int)this.Height);
            Console.Out.WriteLine(watchingWindow.GetUser(X, Y, (int)this.Width, (int)this.Height));
        }
        private KinectWindow watchingWindow;

        private void HandRender()
        {
            Dictionary<String, System.Drawing.Point> Hands = watchingWindow.getHands((int)this.Width, (int)this.Height);
            foreach (KeyValuePair<String, System.Drawing.Point> pair in Hands)
            {
                System.Drawing.Point HandPoint = pair.Value;
                String HandString = pair.Key;
                if (HandPoint.X!=0 && HandPoint.Y!=0)
                    if (HandPoint.X < (int)this.Width && HandPoint.Y < (int)this.Height)
                    {
                        Ellipse HandRange = new Ellipse();
                        HandRange.Fill = System.Windows.Media.Brushes.DarkBlue;
                        HandRange.Width = 100;
                        HandRange.Height = 100;
                        RotateTransform pos = new RotateTransform();
                        pos.CenterX = HandPoint.X;
                        pos.CenterY = HandPoint.Y;
                        HandRange.RenderTransform = pos;
                       // HandRange.
                        DrawingGrid.Children.Add(HandRange);
                    }
            }
        
        }
    }
}