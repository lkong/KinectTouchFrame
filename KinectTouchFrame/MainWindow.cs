using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using xn;
using System.Threading;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Surface.Presentation;
using Microsoft.Surface.Presentation.Controls;
using Microsoft.Surface.Presentation.Input;
using System.Windows.Input;

namespace UserTracker.net
{
	public partial class KinectWindow : Form
	{
		public KinectWindow()
		{
			InitializeComponent();

            this.context = new Context(System.AppDomain.CurrentDomain.BaseDirectory.ToString() + SAMPLE_XML_FILE);
			this.depth = context.FindExistingNode(NodeType.Depth) as DepthGenerator;
			if (this.depth == null)
			{
				throw new Exception("Viewer must have a depth node!");
			}

            this.userGenerator = new UserGenerator(this.context);
            this.skeletonCapbility = new SkeletonCapability(this.userGenerator);
            this.poseDetectionCapability = new PoseDetectionCapability(this.userGenerator);
            this.calibPose = this.skeletonCapbility.GetCalibrationPose();

            this.userGenerator.NewUser += new UserGenerator.NewUserHandler(userGenerator_NewUser);
            this.userGenerator.LostUser += new UserGenerator.LostUserHandler(userGenerator_LostUser);
            this.poseDetectionCapability.PoseDetected += new PoseDetectionCapability.PoseDetectedHandler(poseDetectionCapability_PoseDetected);
            this.skeletonCapbility.CalibrationEnd += new SkeletonCapability.CalibrationEndHandler(skeletonCapbility_CalibrationEnd);

            this.skeletonCapbility.SetSkeletonProfile(SkeletonProfile.All);
            this.joints = new Dictionary<uint,Dictionary<SkeletonJoint,SkeletonJointPosition>>();
            this.userGenerator.StartGenerating();


			this.histogram = new int[this.depth.GetDeviceMaxDepth()];

			MapOutputMode mapMode = this.depth.GetMapOutputMode();

			this.bitmap = new Bitmap((int)mapMode.nXRes, (int)mapMode.nYRes, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            //this.SelectedBitmap = this.bitmap.Clone() as Bitmap;
			this.shouldRun = true;
			this.readerThread = new Thread(ReaderThread);
			this.readerThread.Start();
		}

        void skeletonCapbility_CalibrationEnd(ProductionNode node, uint id, bool success)
        {
            if (success)
            {
                this.skeletonCapbility.StartTracking(id);
                this.joints.Add(id, new Dictionary<SkeletonJoint, SkeletonJointPosition>());
            }
            else
            {
                this.poseDetectionCapability.StartPoseDetection(calibPose, id);
            }
        }

        void poseDetectionCapability_PoseDetected(ProductionNode node, string pose, uint id)
        {
            this.poseDetectionCapability.StopPoseDetection(id);
            this.skeletonCapbility.RequestCalibration(id, true);
        }

        void userGenerator_NewUser(ProductionNode node, uint id)
        {
            this.poseDetectionCapability.StartPoseDetection(this.calibPose, id);
        }

        void userGenerator_LostUser(ProductionNode node, uint id)
        {
            this.joints.Remove(id);
        }

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			lock (this)
			{
                if (!ChangeDisplay)
				e.Graphics.DrawImage(this.bitmap,
					this.panelView.Location.X,
					this.panelView.Location.Y,
					this.panelView.Size.Width,
					this.panelView.Size.Height);
                else
                    e.Graphics.DrawImage(this.Selected,
                    this.panelView.Location.X,
                    this.panelView.Location.Y,
                    this.panelView.Size.Width,
                    this.panelView.Size.Height);

            }
		}

		protected override void OnPaintBackground(PaintEventArgs pevent)
		{
			//Don't allow the background to paint
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			this.shouldRun = false;
			this.readerThread.Join();
			base.OnClosing(e);
		}

		protected override void OnKeyPress(KeyPressEventArgs e)
		{
			if (e.KeyChar == 27)
			{
				Close();
			}
            switch (e.KeyChar)
            {
            case (char)27:
            	break;
            case 'b':
                this.shouldDrawBackground = !this.shouldDrawBackground;
                break;
            case 'x':
                this.shouldDrawPixels = !this.shouldDrawPixels;
                break;
            case 's':
                this.shouldDrawSkeleton = !this.shouldDrawSkeleton;
                break;
            case 'i':
                this.shouldPrintID = !this.shouldPrintID;
                break;
            case 'l':
                this.shouldPrintState = !this.shouldPrintState;
                break;
            case 'c':
                this.shouldPickRegion = this.shouldPickRegion ? false : true;
                break;
            case 'n':
                this.ChangeDisplay = this.ChangeDisplay ? false : true;
                this.finishPick = this.finishPick?false:true;
                
                mouseEndX = 0;mouseEndY=0;mouseStartX=0;mouseStartY=0;
                break;
            case 'm':
                this.SaveBitmap =true;
                //avgSelectedDepth = avgDepth;
                //Console.Out.WriteLine("Depth of selected = " + avgSelectedDepth);
                break;
            }
			base.OnKeyPress(e);
		}

        protected override void OnMouseDown(System.Windows.Forms.MouseEventArgs e)
        {
            if (this.shouldPickRegion && e.Button==MouseButtons.Left)
            {
                mouseStartX = e.Location.X;
                mouseStartY = e.Location.Y;
                mouseEndX = e.Location.X;
                mouseEndY = e.Location.Y;
                finishPick = false;
            }
            base.OnMouseDown(e);

        }

        protected override void OnMouseMove(System.Windows.Forms.MouseEventArgs e)
        {
                if (this.shouldPickRegion && e.Button == MouseButtons.Left)
                {
                    mouseEndX = e.Location.X;
                    mouseEndY = e.Location.Y;
                    //Console.Out.WriteLine(mouseStartX + "  " + mouseStartY + "   " + mouseEndX + "   " + mouseEndY);
                }
            
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(System.Windows.Forms.MouseEventArgs e)
        { 
                if (this.shouldPickRegion)
                {
                    mouseEndX = e.Location.X;
                    mouseEndY = e.Location.Y;
                    finishPick = true;
                }
            base.OnMouseUp(e);
        }

		private unsafe void CalcHist(DepthMetaData depthMD)
		{
			// reset
			for (int i = 0; i < this.histogram.Length; ++i)
				this.histogram[i] = 0;

			ushort* pDepth = (ushort*)depthMD.DepthMapPtr.ToPointer();

			int points = 0;
			for (int y = 0; y < depthMD.YRes; ++y)
			{
				for (int x = 0; x < depthMD.XRes; ++x, ++pDepth)
				{
					ushort depthVal = *pDepth;
					if (depthVal != 0)
					{
						this.histogram[depthVal]++;
						points++;
					}
				}
			}

			for (int i = 1; i < this.histogram.Length; i++)
			{
				this.histogram[i] += this.histogram[i-1];
			}

			if (points > 0)
			{
				for (int i = 1; i < this.histogram.Length; i++)
				{
					this.histogram[i] = (int)(256 * (1.0f - (this.histogram[i] / (float)points)));
				}
			}
		}

        private Color[] colors = { Color.Red, Color.Blue, Color.ForestGreen, Color.Yellow, Color.Orange, Color.Purple, Color.White };
        private Color[] anticolors = { Color.Green, Color.Orange, Color.Red, Color.Purple, Color.Blue, Color.Yellow, Color.Black };
        private int ncolors = 6;

        private void GetJoint(uint user, SkeletonJoint joint)
        {
            SkeletonJointPosition pos = new SkeletonJointPosition();
            this.skeletonCapbility.GetSkeletonJointPosition(user, joint, ref pos);
			if (pos.position.Z == 0)
			{
				pos.fConfidence = 0;
			}
			else
			{
				pos.position = this.depth.ConvertRealWorldToProjective(pos.position);
			}
			this.joints[user][joint] = pos;
        }

        private void DrawRegion(Graphics g)
        {
            if (shouldPickRegion)
            { 
                SolidBrush bgBrush=new SolidBrush(Color.FromArgb(70,Color.Gold));
                g.FillRectangle(bgBrush, MinX, MinY, MaxX-MinX, MaxY-MinY);
            }
        }



        private void GetJoints(uint user)
        {
            GetJoint(user, SkeletonJoint.Head);
            GetJoint(user, SkeletonJoint.Neck);

            GetJoint(user, SkeletonJoint.LeftShoulder);
            GetJoint(user, SkeletonJoint.LeftElbow);
            GetJoint(user, SkeletonJoint.LeftHand);

            GetJoint(user, SkeletonJoint.RightShoulder);
            GetJoint(user, SkeletonJoint.RightElbow);
            GetJoint(user, SkeletonJoint.RightHand);

            GetJoint(user, SkeletonJoint.Torso);

            GetJoint(user, SkeletonJoint.LeftHip);
            GetJoint(user, SkeletonJoint.LeftKnee);
            GetJoint(user, SkeletonJoint.LeftFoot);

            GetJoint(user, SkeletonJoint.RightHip);
            GetJoint(user, SkeletonJoint.RightKnee);
            GetJoint(user, SkeletonJoint.RightFoot);
        }

        private void DrawLine(Graphics g, Color color, Dictionary<SkeletonJoint, SkeletonJointPosition> dict, SkeletonJoint j1, SkeletonJoint j2)
        {
			Point3D pos1 = dict[j1].position;
			Point3D pos2 = dict[j2].position;

			if (dict[j1].fConfidence == 0 || dict[j2].fConfidence == 0)
				return;

            g.DrawLine(new Pen(color),
						new Point((int)pos1.X, (int)pos1.Y),
                        new Point((int)pos2.X, (int)pos2.Y));

        }
        private void DrawSkeleton(Graphics g, Color color, uint user)
        {
            GetJoints(user);
            Dictionary<SkeletonJoint, SkeletonJointPosition> dict = this.joints[user];

            DrawLine(g, color, dict, SkeletonJoint.Head, SkeletonJoint.Neck);

            DrawLine(g, color, dict, SkeletonJoint.LeftShoulder, SkeletonJoint.Torso);
            DrawLine(g, color, dict, SkeletonJoint.RightShoulder, SkeletonJoint.Torso);

            DrawLine(g, color, dict, SkeletonJoint.Neck, SkeletonJoint.LeftShoulder);
            DrawLine(g, color, dict, SkeletonJoint.LeftShoulder, SkeletonJoint.LeftElbow);
            DrawLine(g, color, dict, SkeletonJoint.LeftElbow, SkeletonJoint.LeftHand);

            DrawLine(g, color, dict, SkeletonJoint.Neck, SkeletonJoint.RightShoulder);
            DrawLine(g, color, dict, SkeletonJoint.RightShoulder, SkeletonJoint.RightElbow);
            DrawLine(g, color, dict, SkeletonJoint.RightElbow, SkeletonJoint.RightHand);

            DrawLine(g, color, dict, SkeletonJoint.LeftHip, SkeletonJoint.Torso);
            DrawLine(g, color, dict, SkeletonJoint.RightHip, SkeletonJoint.Torso);
            DrawLine(g, color, dict, SkeletonJoint.LeftHip, SkeletonJoint.RightHip);

            DrawLine(g, color, dict, SkeletonJoint.LeftHip, SkeletonJoint.LeftKnee);
            DrawLine(g, color, dict, SkeletonJoint.LeftKnee, SkeletonJoint.LeftFoot);

            DrawLine(g, color, dict, SkeletonJoint.RightHip, SkeletonJoint.RightKnee);
            DrawLine(g, color, dict, SkeletonJoint.RightKnee, SkeletonJoint.RightFoot);
        }

        public uint GetUser(int X,int Y,int width,int height)
        {
            uint nearestUser = 0;
            uint[] users = this.userGenerator.GetUsers();
            foreach (uint user in users)
            {
                if (this.skeletonCapbility.IsTracking(user))
                {
                    GetJoints(user);
                    Dictionary<SkeletonJoint, SkeletonJointPosition> dict = this.joints[user];
                    Point3D LeftHand = dict[SkeletonJoint.LeftHand].position;
                    Point3D RightHand = dict[SkeletonJoint.RightHand].position;
                    Point touchPoint = new Point(X, Y);
                    if (Math.Abs(X - TransformToScreenPos(LeftHand, width, height).X) < BelongingThresold && Math.Abs(Y - TransformToScreenPos(LeftHand, width, height).Y) < BelongingThresold)
                        nearestUser = user;
                    if (Math.Abs(X - TransformToScreenPos(RightHand, width, height).X) < BelongingThresold && Math.Abs(Y - TransformToScreenPos(RightHand, width, height).Y) < BelongingThresold)
                        nearestUser = user;
                    //e.GetTouchPoint().
                }


            }
            return nearestUser;
        }

        public Dictionary<String, Point> getHands(int width,int height)
        {
            Dictionary<String, Point> handspos = new Dictionary<string, Point>();
            uint[] users = this.userGenerator.GetUsers();
            foreach (uint user in users)
            {
                if (this.skeletonCapbility.IsTracking(user))
                {
                    GetJoints(user);
                    Dictionary<SkeletonJoint, SkeletonJointPosition> dict = this.joints[user];
                    Point3D LeftHand = dict[SkeletonJoint.LeftHand].position;
                    Point3D RightHand = dict[SkeletonJoint.RightHand].position;
                    Point LeftHandScreenPos = TransformToScreenPos(LeftHand, width, height);
                    Point RightHandScreenPos = TransformToScreenPos(RightHand, width, height);
                    String userLeftHand=Convert.ToString(user);
                    String userRightHand=Convert.ToString(user);
                    userLeftHand+=" Lefthand";
                    userRightHand+=" Righthand";
                    handspos.Add(userLeftHand, LeftHandScreenPos);
                    handspos.Add(userRightHand, RightHandScreenPos);
                }


            }
            return handspos;
        }


        private Point TransformToScreenPos(Point3D p,int width,int height)
        {
            Point re=new Point(0,0);
            float tmpX = p.X;
            float tmpY = p.Y;
            if (tmpX < MinX || tmpY < MinY) return re;
            tmpX = tmpX - MinX; tmpY = tmpY - MinY;
            tmpX = tmpX / (MaxX - MinX) * width;
            tmpY = tmpY / (MaxY - MinY) * height;
            re.X = (int)tmpX;
            re.Y = (int)tmpY;
            return re;
        }


        private unsafe void ReaderThread()
        {
            DepthMetaData depthMD = new DepthMetaData();

            while (this.shouldRun)
            {
                try
                {
                    this.context.WaitOneUpdateAll(this.depth);
                }
                catch (Exception)
                {
                }

                this.depth.GetMetaData(depthMD);

                CalcHist(depthMD);
                MinX = mouseStartX > mouseEndX ? mouseEndX : mouseStartX;
                MaxX = mouseStartX < mouseEndX ? mouseEndX : mouseStartX;
                MinY = mouseStartY > mouseEndY ? mouseEndY : mouseStartY;
                MaxY = mouseStartY < mouseEndY ? mouseEndY : mouseStartY;

                    lock (this)
                    {
                        Rectangle rect = new Rectangle(0, 0, this.bitmap.Width, this.bitmap.Height);
                        BitmapData data = this.bitmap.LockBits(rect, ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);


                        if (this.shouldDrawPixels)
                        {
                            ushort* pDepth = (ushort*)this.depth.GetDepthMapPtr().ToPointer();
                            ushort* pLabels = (ushort*)this.userGenerator.GetUserPixels(0).SceneMapPtr.ToPointer();
                            if (this.finishPick) this.avgDepth = 0;
                            // set pixels
                            for (int y = 0; y < depthMD.YRes; ++y)
                                {
                                    byte* pDest = (byte*)data.Scan0.ToPointer() + y * data.Stride;
                                    for (int x = 0; x < depthMD.XRes; ++x, ++pDepth, ++pLabels, pDest += 3)
                                    {
                                        pDest[0] = pDest[1] = pDest[2] = 0;

                                        ushort label = *pLabels;
                                        if (this.shouldDrawBackground || *pLabels != 0)
                                        {
                                            Color labelColor = Color.White;
                                            if (label != 0)
                                            {
                                                labelColor = colors[label % ncolors];
                                            }

                                            byte pixel = (byte)this.histogram[*pDepth];
                                            pDest[0] = (byte)(pixel * (labelColor.B / 256.0));
                                            pDest[1] = (byte)(pixel * (labelColor.G / 256.0));
                                            pDest[2] = (byte)(pixel * (labelColor.R / 256.0));
                                        }

                                        //WTF
                                        if (this.finishPick)
                                            if (x < MinX || x > MaxX || y < MinY || y > MaxY)
                                                if (MaxX!=MinX)
                                            {
                                                pDest[0] = pDest[1] = pDest[2] = 0;
                                            }
                                        if (this.finishPick)
                                            if (x > MinX && x < MaxX && y > MinY && y < MaxY)
                                            { if (*pDepth > maxSelectedDepth) maxSelectedDepth = *pDepth; this.avgDepth += *pDepth; }
                                    }
                                }


                        }

                        this.bitmap.UnlockBits(data);
                        if (this.finishPick)
                            if (MaxX != MinX)
                        {
                            this.avgDepth = this.avgDepth / ((MaxX - MinX) * (MaxY - MinY));
                            Point3D CenterOfSelected = new Point3D((mouseStartX + mouseEndX) / 2, (mouseStartY + mouseEndY) / 2, maxSelectedDepth);
                            this.depth.ConvertRealWorldToProjective(CenterOfSelected);
                            Console.Out.WriteLine("avg depth of selected region = " + avgDepth+" Max = " + CenterOfSelected.Z);
                        }
                        if (this.SaveBitmap) {
                            String path = Directory.GetCurrentDirectory() + "\\Captured.jpg";
                            try { this.bitmap.Save(path); }
                            catch (Exception e) { Console.Out.WriteLine(e.ToString()); }
                            avgSelectedDepth = avgDepth;
                            Console.Out.WriteLine("Depth of selected = " + avgSelectedDepth);
                            this.SaveBitmap = false;
                        }

                    Graphics g;
                    if (!this.ChangeDisplay)
                    {
                        g = Graphics.FromImage(this.bitmap);
                        //Console.Out.WriteLine("Dynamic Background");
                    }
                    else
                    {
                        String filepath = Directory.GetCurrentDirectory() + "\\Captured.jpg";
                        try 
                        {
                            this.Selected =new Bitmap(filepath);
                        }
                        
                        catch (Exception e) { Console.Out.WriteLine(e.ToString()); }
                        g = Graphics.FromImage(this.Selected);
                        //Console.Out.WriteLine("Static Background");
                    }
                    //g.DrawString("Center of Selected", new Font("Arial", 10), new SolidBrush(Color.Gold), CenterOfSelected.X, CenterOfSelected.Y);
                    //Draw Region
                    if (this.shouldPickRegion) DrawRegion(g);

                    uint[] users = this.userGenerator.GetUsers();
                    foreach (uint user in users)
                    {
                        Point3D com = this.userGenerator.GetCoM(user);

                        com = this.depth.ConvertRealWorldToProjective(com);
                        if (this.shouldPrintID)
                        {

                            if (com.Z > userMaxDepth)
                            {
                                userMaxDepth = (int)com.Z;
                            }                            
                            string label = "";
                            if (!this.shouldPrintState)
                                label += user;
                            else if (this.skeletonCapbility.IsTracking(user))
                                label += user + " - Tracking";
                            else if (this.skeletonCapbility.IsCalibrating(user))
                                label += user + " - Calibrating...";
                            else
                                label += user + " - Looking for pose";
                            //Console.Out.WriteLine("Catch u at " + com.Z + " within " + (avgDepth - DistanceThresold) + " and " + (avgDepth + DistanceThresold));
                            //Console.Out.WriteLine(avgSelectedDepth);
                            label += " Depth = " + com.Z;
                            if (com.Z < avgSelectedDepth - DistanceThresold || com.Z > avgSelectedDepth + DistanceThresold)
                                label += " TOO FAR";
                            else
                                Console.Out.WriteLine("Catch u at " + com.Z + " within " + (avgSelectedDepth - DistanceThresold) + " and " + (avgSelectedDepth + DistanceThresold));
                            g.DrawString(label, new Font("Arial", 10), new SolidBrush(anticolors[user % ncolors]), com.X, com.Y);

                        }

                        if (this.shouldDrawSkeleton && this.skeletonCapbility.IsTracking(user))
                            //                        if (this.skeletonCapbility.IsTracking(user))
                            if ((com.Z > avgSelectedDepth - DistanceThresold && com.Z < avgSelectedDepth + DistanceThresold) && avgSelectedDepth != 0)
                            DrawSkeleton(g, anticolors[user % ncolors], user);

                    }
                    g.Dispose();
                }

                this.Invalidate();
            }
        }




		private readonly string SAMPLE_XML_FILE = @"Resources\SamplesConfig.xml";

        private Bitmap Selected;
		private Context context;
		private DepthGenerator depth;
        private UserGenerator userGenerator;
        private SkeletonCapability skeletonCapbility;
        private PoseDetectionCapability poseDetectionCapability;
        private string calibPose;
		private Thread readerThread;

        private int avgDepth = 0;
        private int maxSelectedDepth = 0;
        private int avgSelectedDepth = 0;
        private int userMaxDepth = 0;
		private bool shouldRun;
		private Bitmap bitmap;
		private int[] histogram;

        private int DistanceThresold = 300;
        public int BelongingThresold = 20;

        private Dictionary<uint, Dictionary<SkeletonJoint, SkeletonJointPosition>> joints;




        private int mouseStartX = 0;
        private int mouseStartY = 0;
        private int mouseEndX = 0;
        private int mouseEndY = 0;
        int MinX;
        int MaxX;
        int MinY;
        int MaxY;
        private bool shouldDrawPixels = true;
        private bool shouldDrawBackground = true;
        private bool shouldPrintID = true;
        private bool shouldPrintState = true;
        private bool shouldDrawSkeleton = true;
        private bool shouldPickRegion = false;
        private bool finishPick = false;
        private bool ChangeDisplay = false;
        private bool SaveBitmap = false;

        public static Image selected { get; set; }
    }
}
