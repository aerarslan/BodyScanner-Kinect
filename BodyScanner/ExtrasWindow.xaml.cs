using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace BodyScanner
{    
    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class ExtrasWindow : Window
    {

        // Thickness of drawn joint lines         
        private const double JointThickness = 4;

        // Thickness of clip edge rectangles 
        private const double ClipBoundsThickness = 10;

        // Constant for clamping Z values of camera space points from being negative  
        private const float InferredZPositionClamp = 0.1f;

        // Brush used for drawing joints that are currently tracked    
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        // Brush used for drawing joints that are currently inferred          
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        // Pen used for drawing bones that are currently inferred
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        // Drawing group for body rendering output
        private DrawingGroup drawingGroup;

        // Drawing image that we will display
        private DrawingImage imageSource;

        // Active Kinect sensor
        private KinectSensor kinectSensor = null;

        // Coordinate mapper to map one type of point to another
        private CoordinateMapper coordinateMapper = null;

        // Reader for body frames 
        private BodyFrameReader bodyFrameReader = null;

        // Array for the bodies 
        private Body[] bodies = null;

        // definition of bones
        private List<Tuple<JointType, JointType>> bones;

        // Width of display (depth space)
        private int displayWidth;

        // Height of display (depth space) 
        private int displayHeight;

        // List of colors for each body tracked
        private List<Pen> bodyColors;

        /// <summary>
        /// Reader for color frames
        /// </summary>
        private ColorFrameReader colorFrameReader = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap colorBitmap = null;

        /// <summary>
        /// Map depth range to byte range
        /// </summary>
        private const int MapDepthToByte = 8000 / 256;
        
        /// <summary>
        /// Reader for depth frames
        /// </summary>
        private DepthFrameReader depthFrameReader = null;

        /// <summary>
        /// Description of the data contained in the depth frame
        /// </summary>
        private FrameDescription depthFrameDescription = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap depthBitmap = null;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        private byte[] depthPixels = null;

        public string fileName;
        byte[] colorData;
        
        // Frame number to save data
        public static int frameNo = 0;

        private bool _triggerSkeleton;

        public bool TriggerSkeleton
        {
            get { return _triggerSkeleton; }
            set
            {
                _triggerSkeleton = value;
                if (_triggerSkeleton)
                {
                    // DO SOMETHING HERE
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public ExtrasWindow()
        {
            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // open the reader for the depth frames
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();

            // wire handler for frame arrival
            this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;

            // get FrameDescription from DepthFrameSource
            this.depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // allocate space to put the pixels being received and converted
            this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];

            // create the bitmap to display
            this.depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);

            // open the reader for the color frames
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();

            // wire handler for frame arrival
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;

            // create the colorFrameDescription from the ColorFrameSource using Bgra format
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
            uint frameSize = colorFrameDescription.BytesPerPixel * colorFrameDescription.LengthInPixels;
            colorData = new byte[frameSize];

            // create the bitmap to display
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
          
            // open the sensor
            this.kinectSensor.Open();

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            waitMeshCheck.IsChecked = true;

            this.Height = WindowControl.windowHeight;
        }
       
        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.depthBitmap;
            }
        }

        public ImageSource ImageSource2
        {
            get
            {
                return this.colorBitmap;
            }
        }

        public ImageSource ImageSource3
        {
            get
            {
                return this.imageSource;
            }
        }

        private void ExtrasWindow1_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ExtrasWindow1_Closing(object sender, CancelEventArgs e)
        {
            if (this.depthFrameReader != null)
            {
                // DepthFrameReader is IDisposable
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }

            if (this.colorFrameReader != null)
            {
                // ColorFrameReder is IDisposable
                this.colorFrameReader.Dispose();
                this.colorFrameReader = null;
            }

            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            WindowControl.extrasWindowExist = false;
            WindowControl.extrasWindowVisible = false;

            int findImg = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (File.Exists("img" + i + ".jpeg"))
                {
                    findImg++;
                }
                else
                    break;
            }
            for (int i = 0; i <= findImg; i++)
            {
                File.Delete("img" + i + ".jpeg");
            }

        }

        // Handles the body frame data arriving from the sensor
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                    int penIndex = 0;
                    foreach (Body body in this.bodies)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];

                        if (body.IsTracked)
                        {

                            this.DrawClippedEdges(body, dc);

                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
                            // convert the joint points to depth (display) space
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();


                            Joint baseSpine = joints[JointType.SpineBase];
                            Joint midSpine = joints[JointType.SpineMid];
                            Joint neck = joints[JointType.Neck];
                            Joint head = joints[JointType.Head];
                            Joint leftShoulder = joints[JointType.ShoulderLeft];
                            Joint leftElbow = joints[JointType.ElbowLeft];
                            Joint leftWrist = joints[JointType.WristLeft];
                            Joint leftHand = joints[JointType.HandLeft];
                            Joint rightShoulder = joints[JointType.ShoulderRight];
                            Joint rightElbow = joints[JointType.ElbowRight];
                            Joint rightWrist = joints[JointType.WristRight];
                            Joint rightHand = joints[JointType.HandRight];
                            Joint leftHip = joints[JointType.HipLeft];
                            Joint leftKnee = joints[JointType.KneeLeft];
                            Joint leftAnkle = joints[JointType.AnkleLeft];
                            Joint leftFoot = joints[JointType.FootLeft];
                            Joint rightHip = joints[JointType.HipRight];
                            Joint rightKnee = joints[JointType.KneeRight];
                            Joint rightAnkle = joints[JointType.AnkleRight];
                            Joint rightFoot = joints[JointType.FootRight];
                            Joint shoulderSpine = joints[JointType.SpineShoulder];
                            Joint leftHandTip = joints[JointType.HandTipLeft];
                            Joint leftThumb = joints[JointType.ThumbLeft];
                            Joint rightHandTip = joints[JointType.HandTipRight];
                            Joint rightThumb = joints[JointType.ThumbRight];

                            if (WindowControl.waitMesh)
                            {
                                if (WindowControl.meshStarted)
                                {                                  
                                    if (WindowControl.startSavingSkeleton && oneFrameStop)
                                    {

                                        float middleAnkleX = ((rightAnkle.Position.X + leftAnkle.Position.X) / -2);
                                        float middleAnkleY = ((rightAnkle.Position.Y + leftAnkle.Position.Y) / 2);
                                        float middleAnkleZ = ((rightAnkle.Position.Z + leftAnkle.Position.Z) / -2);

                                        float thirdCornerX = ((head.Position.X + middleAnkleX) / -2);
                                        float thirdCornerZ = ((rightAnkle.Position.Z + leftAnkle.Position.Z) / -2);
                                        float thirdCornerY = head.Position.Y;

                                        double hypotenuseMeter = Math.Sqrt(

                                            ((Convert.ToDouble(head.Position.X*-1) - Convert.ToDouble(middleAnkleX)) * (Convert.ToDouble(head.Position.X * -1) - Convert.ToDouble(middleAnkleX))) +
                                            ((Convert.ToDouble(head.Position.Y) - Convert.ToDouble(middleAnkleY)) * (Convert.ToDouble(head.Position.Y) - Convert.ToDouble(middleAnkleY))) +
                                            ((Convert.ToDouble(head.Position.Z * -1) - Convert.ToDouble(middleAnkleZ)) * (Convert.ToDouble(head.Position.Z * -1) - Convert.ToDouble(middleAnkleZ)))
                                            );
                                       
                                        double shortEdgeMeter = Math.Sqrt(

                                           ((Convert.ToDouble(head.Position.X * -1) - Convert.ToDouble(thirdCornerX)) * (Convert.ToDouble(head.Position.X * -1) - Convert.ToDouble(thirdCornerX))) +
                                           ((Convert.ToDouble(head.Position.Y) - Convert.ToDouble(thirdCornerY)) * (Convert.ToDouble(head.Position.Y) - Convert.ToDouble(thirdCornerY))) +
                                           ((Convert.ToDouble(head.Position.Z * -1) - Convert.ToDouble(thirdCornerZ)) * (Convert.ToDouble(head.Position.Z * -1) - Convert.ToDouble(thirdCornerZ)))
                                           );
                                        
                                        double a = shortEdgeMeter / hypotenuseMeter;

                                        float degree = (float)Math.Asin(a);
                                        if(head.Position.Z*-1 > middleAnkleZ)
                                        {
                                            degree = (float)((degree / 2) * 100);
                                            degree = 360 - degree;
                                        }
                                        else
                                        {
                                            degree = (float)((degree / 2) * 100);
                                        }
    
                                        WindowControl.degree = st(degree).Replace(',', '.');
                                        WindowControl.spineBase_X = st(baseSpine.Position.X * -1).Replace(',', '.');
                                        WindowControl.spineBase_Y = st(baseSpine.Position.Y).Replace(',', '.');
                                        WindowControl.spineBase_Z = st(baseSpine.Position.Z * -1).Replace(',', '.');
                                        WindowControl.spineMid_X = st(midSpine.Position.X * -1).Replace(',', '.');
                                        WindowControl.spineMid_Y = st(midSpine.Position.Y).Replace(',', '.');
                                        WindowControl.spineMid_Z = st(midSpine.Position.Z * -1).Replace(',', '.');
                                        WindowControl.neck_X = st(neck.Position.X * -1).Replace(',', '.');
                                        WindowControl.neck_Y = st(neck.Position.Y).Replace(',', '.');
                                        WindowControl.neck_Z = st(neck.Position.Z * -1).Replace(',', '.');
                                        WindowControl.head_X = st(head.Position.X * -1).Replace(',', '.');
                                        WindowControl.head_Y = st(head.Position.Y).Replace(',', '.');
                                        WindowControl.head_Z = st(head.Position.Z * -1).Replace(',', '.');
                                        WindowControl.shoulderLeft_X = st(leftShoulder.Position.X * -1).Replace(',', '.');
                                        WindowControl.shoulderLeft_Y = st(leftShoulder.Position.Y).Replace(',', '.');
                                        WindowControl.shoulderLeft_Z = st(leftShoulder.Position.Z * -1).Replace(',', '.');
                                        WindowControl.elbowLeft_X = st(leftElbow.Position.X * -1).Replace(',', '.');
                                        WindowControl.elbowLeft_Y = st(leftElbow.Position.Y).Replace(',', '.');
                                        WindowControl.elbowLeft_Z = st(leftElbow.Position.Z * -1).Replace(',', '.');
                                        WindowControl.wristLeft_X = st(leftWrist.Position.X * -1).Replace(',', '.');
                                        WindowControl.wristLeft_Y = st(leftWrist.Position.Y).Replace(',', '.');
                                        WindowControl.wristLeft_Z = st(leftWrist.Position.Z * -1).Replace(',', '.');
                                        WindowControl.handLeft_X = st(leftHand.Position.X * -1).Replace(',', '.');
                                        WindowControl.handLeft_Y = st(leftHand.Position.Y).Replace(',', '.');
                                        WindowControl.handLeft_Z = st(leftHand.Position.Z * -1).Replace(',', '.');
                                        WindowControl.shoulderRight_X = st(rightShoulder.Position.X * -1).Replace(',', '.');
                                        WindowControl.shoulderRight_Y = st(rightShoulder.Position.Y).Replace(',', '.');
                                        WindowControl.shoulderRight_Z = st(rightShoulder.Position.Z * -1).Replace(',', '.');
                                        WindowControl.elbowRight_X = st(rightElbow.Position.X * -1).Replace(',', '.');
                                        WindowControl.elbowRight_Y = st(rightElbow.Position.Y).Replace(',', '.');
                                        WindowControl.elbowRight_Z = st(rightElbow.Position.Z * -1).Replace(',', '.');
                                        WindowControl.wristRight_X = st(rightWrist.Position.X * -1).Replace(',', '.');
                                        WindowControl.wristRight_Y = st(rightWrist.Position.Y).Replace(',', '.');
                                        WindowControl.wristRight_Z = st(rightWrist.Position.Z * -1).Replace(',', '.');
                                        WindowControl.handRight_X = st(rightHand.Position.X * -1).Replace(',', '.');
                                        WindowControl.handRight_Y = st(rightHand.Position.Y).Replace(',', '.');
                                        WindowControl.handRight_Z = st(rightHand.Position.Z * -1).Replace(',', '.');
                                        WindowControl.hipLeft_X = st(leftHip.Position.X * -1).Replace(',', '.');
                                        WindowControl.hipLeft_Y = st(leftHip.Position.Y).Replace(',', '.');
                                        WindowControl.hipLeft_Z = st(leftHip.Position.Z * -1).Replace(',', '.');
                                        WindowControl.kneeLeft_X = st(leftKnee.Position.X * -1).Replace(',', '.');
                                        WindowControl.kneeLeft_Y = st(leftKnee.Position.Y).Replace(',', '.');
                                        WindowControl.kneeLeft_Z = st(leftKnee.Position.Z * -1).Replace(',', '.');
                                        WindowControl.ankleLeft_X = st(leftAnkle.Position.X * -1).Replace(',', '.');
                                        WindowControl.ankleLeft_Y = st(leftAnkle.Position.Y).Replace(',', '.');
                                        WindowControl.ankleLeft_Z = st(leftAnkle.Position.Z * -1).Replace(',', '.');
                                        WindowControl.footLeft_X = st(leftFoot.Position.X * -1).Replace(',', '.');
                                        WindowControl.footLeft_Y = st(leftFoot.Position.Y).Replace(',', '.');
                                        WindowControl.footLeft_Z = st(leftFoot.Position.Z * -1).Replace(',', '.');
                                        WindowControl.hipRight_X = st(rightHip.Position.X * -1).Replace(',', '.');
                                        WindowControl.hipRight_Y = st(rightHip.Position.Y).Replace(',', '.');
                                        WindowControl.hipRight_Z = st(rightHip.Position.Z * -1).Replace(',', '.');
                                        WindowControl.kneeRight_X = st(rightKnee.Position.X * -1).Replace(',', '.');
                                        WindowControl.kneeRight_Y = st(rightKnee.Position.Y).Replace(',', '.');
                                        WindowControl.kneeRight_Z = st(rightKnee.Position.Z * -1).Replace(',', '.');
                                        WindowControl.ankleRight_X = st(rightAnkle.Position.X * -1).Replace(',', '.');
                                        WindowControl.ankleRight_Y = st(rightAnkle.Position.Y).Replace(',', '.');
                                        WindowControl.ankleRight_Z = st(rightAnkle.Position.Z * -1).Replace(',', '.');
                                        WindowControl.footRight_X = st(rightFoot.Position.X * -1).Replace(',', '.');
                                        WindowControl.footRight_Y = st(rightFoot.Position.Y).Replace(',', '.');
                                        WindowControl.footRight_Z = st(rightFoot.Position.Z * -1).Replace(',', '.');
                                        WindowControl.spineShoulder_X = st(shoulderSpine.Position.X * -1).Replace(',', '.');
                                        WindowControl.spineShoulder_Y = st(shoulderSpine.Position.Y).Replace(',', '.');
                                        WindowControl.spineShoulder_Z = st(shoulderSpine.Position.Z * -1).Replace(',', '.');
                                        WindowControl.handTipLeft_X = st(leftHandTip.Position.X * -1).Replace(',', '.');
                                        WindowControl.handTipLeft_Y = st(leftHandTip.Position.Y).Replace(',', '.');
                                        WindowControl.handTipLeft_Z = st(leftHandTip.Position.Z * -1).Replace(',', '.');
                                        WindowControl.thumbLeft_X = st(leftThumb.Position.X * -1).Replace(',', '.');
                                        WindowControl.thumbLeft_Y = st(leftThumb.Position.Y).Replace(',', '.');
                                        WindowControl.thumbLeft_Z = st(leftThumb.Position.Z * -1).Replace(',', '.');
                                        WindowControl.handTipRight_X = st(rightHandTip.Position.X * -1).Replace(',', '.');
                                        WindowControl.handTipRight_Y = st(rightHandTip.Position.Y).Replace(',', '.');
                                        WindowControl.handTipRight_Z = st(rightHandTip.Position.Z * -1).Replace(',', '.');
                                        WindowControl.thumbRight_X = st(rightThumb.Position.X * -1).Replace(',', '.');
                                        WindowControl.thumbRight_Y = st(rightThumb.Position.Y).Replace(',', '.');
                                        WindowControl.thumbRight_Z = st(rightThumb.Position.Z * -1).Replace(',', '.');

                                        /*
                                        using (StreamWriter writer = new StreamWriter(fileName, true))
                                        {                        
                                            writer.Write("Degree" + "," + st(degree).Replace(',', '.') + "," + st(degree).Replace(',', '.') + "," + st(degree).Replace(',', '.') + "\nSpineBase" + "," + st(baseSpine.Position.X*-1).Replace(',','.') + "," + st(baseSpine.Position.Y).Replace(',','.') + "," + st(baseSpine.Position.Z*-1).Replace(',','.') + "\nSpineMid" + "," + st(midSpine.Position.X*-1).Replace(',','.') + "," + st(midSpine.Position.Y).Replace(',','.') + "," + st(midSpine.Position.Z*-1).Replace(',','.') +"\nNeck" + "," + st(neck.Position.X*-1).Replace(',','.') + "," + st(neck.Position.Y).Replace(',','.') + "," + st(neck.Position.Z*-1).Replace(',', '.')
                                                         + "\nHead" + "," + st(head.Position.X*-1).Replace(',','.') + "," + st(head.Position.Y).Replace(',','.') + "," + st(head.Position.Z*-1).Replace(',','.') + "\nShoulderLeft" + "," + st(leftShoulder.Position.X*-1).Replace(',','.') + "," + st(leftShoulder.Position.Y).Replace(',','.') + "," + st(leftShoulder.Position.Z*-1).Replace(',','.') + "\nElbowLeft" + "," + st(leftElbow.Position.X*-1).Replace(',','.') + "," + st(leftElbow.Position.Y).Replace(',','.') + "," + st(leftElbow.Position.Z*-1).Replace(',', '.')
                                                         + "\nWristLeft" + "," + st(leftWrist.Position.X*-1).Replace(',','.') + "," + st(leftWrist.Position.Y).Replace(',','.') + "," + st(leftWrist.Position.Z*-1).Replace(',','.') + "\nHandLeft" + "," + st(leftHand.Position.X*-1).Replace(',','.') + "," + st(leftHand.Position.Y).Replace(',','.') + "," + st(leftHand.Position.Z*-1).Replace(',','.') + "\nShoulderRight" + "," + st(rightShoulder.Position.X*-1).Replace(',','.') + "," + st(rightShoulder.Position.Y).Replace(',','.') + "," + st(rightShoulder.Position.Z*-1).Replace(',', '.')
                                                         + "\nElbowRight" + "," + st(rightElbow.Position.X*-1).Replace(',','.') + "," + st(rightElbow.Position.Y).Replace(',','.') + "," + st(rightElbow.Position.Z*-1).Replace(',','.') + "\nWristRight" + "," + st(rightWrist.Position.X*-1).Replace(',','.') + "," + st(rightWrist.Position.Y).Replace(',','.') + "," + st(rightWrist.Position.Z*-1).Replace(',','.') + "\nHandRight" + "," + st(rightHand.Position.X*-1).Replace(',','.') + "," + st(rightHand.Position.Y).Replace(',','.') + "," + st(rightHand.Position.Z*-1).Replace(',', '.')
                                                         + "\nHipLeft" + "," + st(leftHip.Position.X*-1).Replace(',','.') + "," + st(leftHip.Position.Y).Replace(',','.') + "," + st(leftHip.Position.Z*-1).Replace(',','.') + "\nKneeLeft" + "," + st(leftKnee.Position.X*-1).Replace(',','.') + "," + st(leftKnee.Position.Y).Replace(',','.') + "," + st(leftKnee.Position.Z*-1).Replace(',','.') + "\nAnkleLeft" + "," + st(leftAnkle.Position.X*-1).Replace(',','.') + "," + st(leftAnkle.Position.Y).Replace(',','.') + "," + st(leftAnkle.Position.Z*-1).Replace(',', '.')
                                                         + "\nFootLeft" + "," + st(leftFoot.Position.X*-1).Replace(',','.') + "," + st(leftFoot.Position.Y).Replace(',','.') + "," + st(leftFoot.Position.Z*-1).Replace(',','.') + "\nHipRight" + "," + st(rightHip.Position.X*-1).Replace(',','.') + "," + st(rightHip.Position.Y).Replace(',','.') + "," + st(rightHip.Position.Z*-1).Replace(',','.') + "\nKneeRight" + "," + st(rightKnee.Position.X*-1).Replace(',','.') + "," + st(rightKnee.Position.Y).Replace(',','.') + "," + st(rightKnee.Position.Z*-1).Replace(',', '.')
                                                         + "\nAnkleRight" + "," + st(rightAnkle.Position.X*-1).Replace(',','.') + "," + st(rightAnkle.Position.Y).Replace(',','.') + "," + st(rightAnkle.Position.Z*-1).Replace(',','.') + "\nFootRight" + "," + st(rightFoot.Position.X*-1).Replace(',','.') + "," + st(rightFoot.Position.Y).Replace(',','.') + "," + st(rightFoot.Position.Z*-1).Replace(',','.') + "\nSpineShoulder" + "," + st(shoulderSpine.Position.X*-1).Replace(',','.') + "," + st(shoulderSpine.Position.Y).Replace(',','.') + "," + st(shoulderSpine.Position.Z*-1).Replace(',', '.')
                                                         + "\nHandTipLeft" + "," + st(leftHandTip.Position.X*-1).Replace(',','.') + "," + st(leftHandTip.Position.Y).Replace(',','.') + "," + st(leftHandTip.Position.Z*-1).Replace(',','.') + "\nThumbLeft" + "," + st(leftThumb.Position.X*-1).Replace(',','.') + "," + st(leftThumb.Position.Y).Replace(',','.') + "," + st(leftThumb.Position.Z*-1).Replace(',','.') + "\nHandTipRight" + "," + st(rightHandTip.Position.X*-1).Replace(',','.') + "," + st(rightHandTip.Position.Y).Replace(',','.') + "," + st(rightHandTip.Position.Z*-1).Replace(',', '.')
                                                         + "\nThumbRight" + "," + st(rightThumb.Position.X*-1).Replace(',','.') + "," + st(rightThumb.Position.Y).Replace(',','.') + "," + st(rightThumb.Position.Z*-1).Replace(',', '.'));
                                            writer.WriteLine();
                                        }
                                        */
                                        WindowControl.startSavingSkeleton = false;
                                        oneFrameStopCheck.IsChecked = false;
                                        skeletonStatus.Text = "";
                                    }
                                }
                                else if (!WindowControl.meshStarted && WindowControl.startSavingSkeleton)
                                {
                                    skeletonStatus.Text = "Start mesh to continue or stop saving";
                                }
                            }

                            else
                            {
                                if (WindowControl.startSavingSkeleton && !oneFrameStop)
                                {
                                    using (StreamWriter writer = new StreamWriter(fileName, true))
                                    {

                                        frameNo++;
                                        writer.Write(st(frameNo) + ";" + st(baseSpine.Position.X) + ";" + st(baseSpine.Position.Y) + ";" + st(baseSpine.Position.Z) + ";" + st(midSpine.Position.X) + ";" + st(midSpine.Position.Y) + ";" + st(midSpine.Position.Z) + ";" + st(neck.Position.X) + ";" + st(neck.Position.Y) + ";" + st(neck.Position.Z)
                                                     + ";" + st(head.Position.X) + ";" + st(head.Position.Y) + ";" + st(head.Position.Z) + ";" + st(leftShoulder.Position.X) + ";" + st(leftShoulder.Position.Y) + ";" + st(leftShoulder.Position.Z) + ";" + st(leftElbow.Position.X) + ";" + st(leftElbow.Position.Y) + ";" + st(leftElbow.Position.Z)
                                                     + ";" + st(leftWrist.Position.X) + ";" + st(leftWrist.Position.Y) + ";" + st(leftWrist.Position.Z) + ";" + st(leftHand.Position.X) + ";" + st(leftHand.Position.Y) + ";" + st(leftHand.Position.Z) + ";" + st(rightShoulder.Position.X) + ";" + st(rightShoulder.Position.Y) + ";" + st(rightShoulder.Position.Z)
                                                     + ";" + st(rightElbow.Position.X) + ";" + st(rightElbow.Position.Y) + ";" + st(rightElbow.Position.Z) + ";" + st(rightWrist.Position.X) + ";" + st(rightWrist.Position.Y) + ";" + st(rightWrist.Position.Z) + ";" + st(rightHand.Position.X) + ";" + st(rightHand.Position.Y) + ";" + st(rightHand.Position.Z)
                                                     + ";" + st(leftHip.Position.X) + ";" + st(leftHip.Position.Y) + ";" + st(leftHip.Position.Z) + ";" + st(leftKnee.Position.X) + ";" + st(leftKnee.Position.Y) + ";" + st(leftKnee.Position.Z) + ";" + st(leftAnkle.Position.X) + ";" + st(leftAnkle.Position.Y) + ";" + st(leftAnkle.Position.Z)
                                                     + ";" + st(leftFoot.Position.X) + ";" + st(leftFoot.Position.Y) + ";" + st(leftFoot.Position.Z) + ";" + st(rightHip.Position.X) + ";" + st(rightHip.Position.Y) + ";" + st(rightHip.Position.Z) + ";" + st(rightKnee.Position.X) + ";" + st(rightKnee.Position.Y) + ";" + st(rightKnee.Position.Z)
                                                     + ";" + st(rightAnkle.Position.X) + ";" + st(rightAnkle.Position.Y) + ";" + st(rightAnkle.Position.Z) + ";" + st(rightFoot.Position.X) + ";" + st(rightFoot.Position.Y) + ";" + st(rightFoot.Position.Z) + ";" + st(shoulderSpine.Position.X) + ";" + st(shoulderSpine.Position.Y) + ";" + st(shoulderSpine.Position.Z)
                                                     + ";" + st(leftHandTip.Position.X) + ";" + st(leftHandTip.Position.Y) + ";" + st(leftHandTip.Position.Z) + ";" + st(leftThumb.Position.X) + ";" + st(leftThumb.Position.Y) + ";" + st(leftThumb.Position.Z) + ";" + st(rightHandTip.Position.X) + ";" + st(rightHandTip.Position.Y) + ";" + st(rightHandTip.Position.Z)
                                                     + ";" + st(rightThumb.Position.X) + ";" + st(rightThumb.Position.Y) + ";" + st(rightThumb.Position.Z));
                                        writer.WriteLine();
                                    }
                                }

                                else if (WindowControl.startSavingSkeleton && oneFrameStop)
                                {
                                    using (StreamWriter writer = new StreamWriter(fileName, true))
                                    {

                                        frameNo++;
                                        writer.Write(st(frameNo) + ";" + st(baseSpine.Position.X) + ";" + st(baseSpine.Position.Y) + ";" + st(baseSpine.Position.Z) + ";" + st(midSpine.Position.X) + ";" + st(midSpine.Position.Y) + ";" + st(midSpine.Position.Z) + ";" + st(neck.Position.X) + ";" + st(neck.Position.Y) + ";" + st(neck.Position.Z)
                                                     + ";" + st(head.Position.X) + ";" + st(head.Position.Y) + ";" + st(head.Position.Z) + ";" + st(leftShoulder.Position.X) + ";" + st(leftShoulder.Position.Y) + ";" + st(leftShoulder.Position.Z) + ";" + st(leftElbow.Position.X) + ";" + st(leftElbow.Position.Y) + ";" + st(leftElbow.Position.Z)
                                                     + ";" + st(leftWrist.Position.X) + ";" + st(leftWrist.Position.Y) + ";" + st(leftWrist.Position.Z) + ";" + st(leftHand.Position.X) + ";" + st(leftHand.Position.Y) + ";" + st(leftHand.Position.Z) + ";" + st(rightShoulder.Position.X) + ";" + st(rightShoulder.Position.Y) + ";" + st(rightShoulder.Position.Z)
                                                     + ";" + st(rightElbow.Position.X) + ";" + st(rightElbow.Position.Y) + ";" + st(rightElbow.Position.Z) + ";" + st(rightWrist.Position.X) + ";" + st(rightWrist.Position.Y) + ";" + st(rightWrist.Position.Z) + ";" + st(rightHand.Position.X) + ";" + st(rightHand.Position.Y) + ";" + st(rightHand.Position.Z)
                                                     + ";" + st(leftHip.Position.X) + ";" + st(leftHip.Position.Y) + ";" + st(leftHip.Position.Z) + ";" + st(leftKnee.Position.X) + ";" + st(leftKnee.Position.Y) + ";" + st(leftKnee.Position.Z) + ";" + st(leftAnkle.Position.X) + ";" + st(leftAnkle.Position.Y) + ";" + st(leftAnkle.Position.Z)
                                                     + ";" + st(leftFoot.Position.X) + ";" + st(leftFoot.Position.Y) + ";" + st(leftFoot.Position.Z) + ";" + st(rightHip.Position.X) + ";" + st(rightHip.Position.Y) + ";" + st(rightHip.Position.Z) + ";" + st(rightKnee.Position.X) + ";" + st(rightKnee.Position.Y) + ";" + st(rightKnee.Position.Z)
                                                     + ";" + st(rightAnkle.Position.X) + ";" + st(rightAnkle.Position.Y) + ";" + st(rightAnkle.Position.Z) + ";" + st(rightFoot.Position.X) + ";" + st(rightFoot.Position.Y) + ";" + st(rightFoot.Position.Z) + ";" + st(shoulderSpine.Position.X) + ";" + st(shoulderSpine.Position.Y) + ";" + st(shoulderSpine.Position.Z)
                                                     + ";" + st(leftHandTip.Position.X) + ";" + st(leftHandTip.Position.Y) + ";" + st(leftHandTip.Position.Z) + ";" + st(leftThumb.Position.X) + ";" + st(leftThumb.Position.Y) + ";" + st(leftThumb.Position.Z) + ";" + st(rightHandTip.Position.X) + ";" + st(rightHandTip.Position.Y) + ";" + st(rightHandTip.Position.Z)
                                                     + ";" + st(rightThumb.Position.X) + ";" + st(rightThumb.Position.Y) + ";" + st(rightThumb.Position.Z));
                                        writer.WriteLine();
                                    }
                                    WindowControl.startSavingSkeleton = false;
                                    oneFrameStopCheck.IsChecked = false;
                                }
                            }

                            foreach (JointType jointType in joints.Keys)
                            {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0)
                                {
                                    position.Z = InferredZPositionClamp;
                                }

                                DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }

                            this.DrawBody(joints, jointPoints, dc, drawPen);

                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                }
            }
        }

        // Draws a body

        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }


        // Draws one bone of a body (joint to joint)
        // <param name="jointType0">first joint of bone to draw</param>
        // <param name="jointType1">second joint of bone to draw</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        // Draws indicators to show which edges are clipping body data    
        // <param name="body">body to draw clipping information for</param>
        // <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }


        /// <summary>
        /// Handles the color frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {                                                     
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        this.colorBitmap.Lock();

                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(
                                this.colorBitmap.BackBuffer,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                            this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));
                        }

                        this.colorBitmap.Unlock();
                    }              
                }
            }
        }

        /// <summary>
        /// Handles the depth frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            ushort maxDepth = ushort.MaxValue;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            //// maxDepth = depthFrame.DepthMaxReliableDistance

                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }
                }
            }

            if (depthFrameProcessed)
            {
                this.RenderDepthPixels();
            }
        }

        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="depthFrameData">Pointer to the DepthFrame image data</param>
        /// <param name="depthFrameDataSize">Size of the DepthFrame image data</param>
        /// <param name="minDepth">The minimum reliable depth value for the frame</param>
        /// <param name="maxDepth">The maximum reliable depth value for the frame</param>
        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            // convert depth to a visual representation
            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i)
            {
                // Get the depth for this pixel
                ushort depth = frameData[i];

                // To convert to a byte, we're mapping the depth value to the byte range.
                // Values outside the reliable depth range are mapped to 0 (black).
                this.depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
            }
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderDepthPixels()
        {
            this.depthBitmap.WritePixels(
                new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                this.depthPixels,
                this.depthBitmap.PixelWidth,
                0);
        }

        private void stopSavingSkeleton_Click(object sender, RoutedEventArgs e)
        {
            WindowControl.startSavingSkeleton = false;
            skeletonStatus.Text = "";
        }

        private void startSavingSkeleton_Click(object sender, RoutedEventArgs e)
        {
            /*
                SPINEBASE = 0;
                SPINEMID = 1;
                NECK = 2;
                HEAD = 3;
                SHOULDERLEFT = 4;
                ELBOWLEFT = 5;
                WRISTLEFT = 6;
                HANDLEFT = 7;
                SHOULDERRIGHT = 8;
                ELBOWRIGHT = 9;
                WRISTRIGHT = 10;
                HANDRIGHT = 11;
                HIPLEFT = 12;
                KNEELEFT = 13;
                ANKLELEFT = 14;
                FOOTLEFT = 15;
                HIPRIGHT = 16;
                KNEERIGHT = 17;
                ANKLERIGHT = 18;
                FOOTRIGHT = 19;
                SPINESHOULDER = 20;
                HANDTIPLEFT  = 21;
                THUMBLEFT = 22;
                HANDTIPRIGHT = 23;
                THUMBRIGHT = 24;
             */
            var dialog = new SaveFileDialog
            {
                Title = "Save Model",
                Filter = "CSV|*.csv",
                FilterIndex = 0,
                DefaultExt = ".csv",
                InitialDirectory = Directory.GetCurrentDirectory() + "\\Recorded_Data"
        };

            string dateTime = DateTime.Now.ToString("yyyyMMdd_hhmmss");
            dialog.FileName = dateTime;
           
            if (WindowControl.waitMesh)
            {
                if (dialog.ShowDialog() == true)
                {
                    fileName = dialog.FileName;
                    using (var writer = File.CreateText(dialog.FileName))
                    {
                        writer.Write("Joints," + "X," + "Y," + "Z");
                        writer.WriteLine();
                    }
                }
            }
            else
            {
                if (dialog.ShowDialog() == true)
                {
                    fileName = dialog.FileName;
                    using (var writer = File.CreateText(dialog.FileName))
                    {
                        writer.Write("Frame;" + "SpineBase_X;" + "SpineBase_Y;" + "SpineBase_Z;" + "SpineMid_X;" + "SpineMid_Y;" + "SpineMid_Z;" + "Neck_X;" + "Neck_Y;" + "Neck_Z;" +
                                  "Head_X;" + "Head_Y;" + "Head_Z;" + "ShoulderLeft_X;" + "ShoulderLeft_Y;" + "ShoulderLeft_Z;" + "ElbowLeft_X;" + "ElbowLeft_Y;" + "ElbowLeft_Z;" +
                                  "WristLeft_X;" + "WristLeft_Y;" + "WristLeft_Z;" + "HandLeft_X;" + "HandLeft_Y;" + "HandLeft_Z;" + "ShoulderRight_X;" + "ShoulderRight_Y;" + "ShoulderRight_Z;" +
                                  "ElbowRight_X;" + "ElbowRight_Y;" + "ElbowRight_Z;" + "WristRight_X;" + "WristRight_Y;" + "WristRight_Z;" + "HandRight_X;" + "HandRight_Y;" + "HandRight_Z;" +
                                  "HipLeft_X;" + "HipLeft_Y;" + "HipLeft_Z;" + "KneeLeft_X;" + "KneeLeft_Y;" + "KneeLeft_Z;" + "AnkleLeft_X;" + "AnkleLeft_Y;" + "AnkleLeft_Z;" +
                                  "FootLeft_X;" + "FootLeft_Y;" + "FootLeft_Z;" + "HipRight_X;" + "HipRight_Y;" + "HipRight_Z;" + "KneeRight_X;" + "KneeRight_Y;" + "KneeRight_Z;" +
                                  "AnkleRight_X;" + "AnkleRight_Y;" + "AnkleRight_Z;" + "FootRight_X;" + "FootRight_Y;" + "FootRight_Z;" + "SpineShoulder_X;" + "SpineShoulder_Y;" + "SpineShoulder_Z;" +
                                  "HandTipLeft_X;" + "HandTipLeft_Y;" + "HandTipLeft_Z;" + "ThumbLeft_X;" + "ThumbLeft_Y;" + "ThumbLeft_Z;" + "HandTipRight_X;" + "HandTipRight_Y;" + "HandTipRight_Z;" +
                                  "ThumbRight_X;" + "ThumbRight_Y;" + "ThumbRight_Z;");
                        writer.WriteLine();
                    }
                }
            }

            if (WindowControl.waitMesh)
            {
                WindowControl.namePass = true;
                WindowControl.nameHolder = Path.GetFileNameWithoutExtension(dialog.FileName);
            }

            if (waitMeshCheck.IsChecked == true)
            {
                WindowControl.startSavingSkeleton = true;
                skeletonStatus.Text = "Start mesh to continue or stop saving";
            }
            else
            {
                Task.Delay(startDelay * 1000).ContinueWith(_ =>
                {
                    WindowControl.startSavingSkeleton = true;
                }
            );
            }
            
            if(!manuelStop && !oneFrameStop)
            {
                Task.Delay((startDelay + savingTime) * 1000).ContinueWith(_ =>
                {
                    WindowControl.startSavingSkeleton = false;
                }
                );
            }           
        }

        public string st(float coordinate)
        {
            return Convert.ToString(coordinate);
        }

        public int startDelay = 1;
        private void startDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                startDelaySliderTxt.Text = Convert.ToString(startDelaySlider.Value);
                startDelay = Convert.ToInt32(startDelaySlider.Value);
            }
            catch
            {

            }
            
        }

        public int savingTime = 3;
        private void savingTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                manuelStopCheck.IsChecked = false;                    
                savingTimeSliderTxt.Text = Convert.ToString(savingTimeSlider.Value);
                savingTime = Convert.ToInt32(savingTimeSlider.Value);
            }
            catch
            {

            }
        }

        public bool oneFrameStop = false;
        private void oneFrameStopCheck_Checked(object sender, RoutedEventArgs e)
        {
            manuelStopCheck.IsChecked = false;
            savingTimeSlider.IsEnabled = false;
            oneFrameStop = true;
        }

        public bool manuelStop = false;
        private void manuelStopCheck_Checked(object sender, RoutedEventArgs e)
        {
            savingTimeSlider.IsEnabled = false;
            oneFrameStopCheck.IsChecked = false;
            manuelStop = true;
        }

        private void oneFrameStopCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            oneFrameStop = false;
            savingTimeSlider.IsEnabled = true;
        }

        private void manuelStopCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            savingTimeSlider.IsEnabled = true;
            manuelStop = false;
        }

        private void waitMeshCheck_Checked(object sender, RoutedEventArgs e)
        {
            oneFrameStop = true;
            savingTimeSlider.IsEnabled = false;
            oneFrameStopCheck.IsEnabled = false;
            manuelStopCheck.IsEnabled = false;
            startDelaySlider.IsEnabled = false;
            WindowControl.waitMesh = true;
            oneFrameStopCheck.IsChecked = false;
            manuelStopCheck.IsChecked = false;
            skeletonMenuBorder.Visibility = Visibility.Hidden;
            startDelaySliderTxt.Visibility = Visibility.Hidden;
            savingTimeSliderTxt.Visibility = Visibility.Hidden;
            startSavingSkeleton.Visibility = Visibility.Hidden;
            stopSavingSkeleton.Visibility = Visibility.Hidden;
        }

        private void waitMeshCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            oneFrameStop = false;
            savingTimeSlider.IsEnabled = true;
            oneFrameStopCheck.IsEnabled = true;         
            manuelStopCheck.IsEnabled = true;
            startDelaySlider.IsEnabled = true;
            WindowControl.waitMesh = false;
            WindowControl.namePass = false;
            skeletonMenuBorder.Visibility = Visibility.Visible;
            startDelaySliderTxt.Visibility = Visibility.Visible;
            savingTimeSliderTxt.Visibility = Visibility.Visible;
            startSavingSkeleton.Visibility = Visibility.Visible;
            stopSavingSkeleton.Visibility = Visibility.Visible;
        }
    }
}
