using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using Microsoft.Kinect;
using System.Windows.Threading;
using System.Threading;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace BodyScanner
{
    /// <summary>
    /// Interaction logic for StaticKinectWindow.xaml
    /// </summary>
    public partial class StaticKinectWindow : Window
    {
        /// <summary>
        /// The situation of recording.
        /// Passes the value to the Reader_MultiSourceFrameArrived() function in ReconstructionController class.
        /// </summary>
        public static bool startRecording = true;

        /// <summary>
        /// The bitmap holder of scan bitmap.
        /// </summary>
        private readonly WriteableBitmapHolder scanBitmapHolder = new WriteableBitmapHolder();

        /// <summary>
        /// The constructor function of window. Appviewmodel is created here. Also viewmodel is updated in here.
        /// </summary>
        public StaticKinectWindow()
        {
            // Creates a new appviewmodel and equalize it with the DataContext on line 48
            AppViewModel viewModel;
            try
            {
                // creates the new appviewmodel. 
                viewModel = CreateViewModel();
            }
            catch (Exception ex)
            {
                // message box
                MessageBox.Show(string.Format(Properties.Resources.InitializationError, ex.Message),
                    Properties.Resources.ApplicationName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            DataContext = viewModel;

            InitializeComponent();

            // Loaded is a RoutedEventHandler. Occurs when the element is laid out, rendered, and ready for interaction. 
            // Here it calls ViewModel.PropertyChanged function.
            Loaded += Window_Loaded;

            // default duration of the scan
            scanDurationSlider.Value = 20;
         
        }

        /// <summary>
        /// Expression-bodied property. Returns appviewmodel.
        /// </summary>
        private AppViewModel ViewModel => (AppViewModel)DataContext;

        /// <summary>
        /// Viewmodel property changed function. Calls ViewModel_PropertyChanged() function that updates scan bitmap and viewport transforms.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>       
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        /// <summary>
        /// Creates a new view model.
        /// </summary>
        /// <returns>A new AppViewModel</returns>
        private static AppViewModel CreateViewModel()
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());

            // Checks for the sensor then opens it.
            var sensor = CreateAndOpenSensor();

            // Creates a new ScanningEngine with the sensor that is created above.
            var engine = new ScanningEngine(sensor, () => new ReconstructionController(sensor));

            // Creates a new KinectFrameRenderer with the sensor that is created above.
            var renderer = new KinectFrameRenderer(sensor, new DepthToColorConverter());

            // Creates a new UserInteractionService object.
            var uis = new UserInteractionService();

            // A new AppViewModel
            return new AppViewModel(engine, renderer, uis);
        }

        /// <summary>
        /// Gets the sensor then opens it.
        /// </summary>
        private static KinectSensor CreateAndOpenSensor()
        {
            var sensor = KinectSensor.GetDefault();
            if (sensor == null)
            {
                throw new ApplicationException(Properties.Resources.KinectNotAvailable);
            }

            sensor.Open();
            if (!sensor.IsOpen)
            {
                throw new ApplicationException(Properties.Resources.KinectNotAvailable);
            }

            return sensor;
        }

        /// <summary>
        /// Property changed function for viewmodel. Is used to update scan bitmap and viewport transforms.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>       
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(AppViewModel.ScanBitmap):
                    UpdateScanBitmap();
                    break;
                case nameof(AppViewModel.Body3DModel):
                    SetupViewportTransforms();
                    break;
            }
        }

        /// <summary>
        /// Bitmap updater for scan bitmap.
        /// </summary>  
        private void UpdateScanBitmap()
        {
            UpdateBitmap(ViewModel.ScanBitmap, scanBitmapHolder, scanImage);
        }

        /// <summary>
        /// Updates the source of given image.
        /// </summary>
        /// <param name="bitmap">Bitmap</param>
        /// <param name="holder">Bitmap holder</param>   
        /// <param name="image">Image from statickinect window.</param>  
        private void UpdateBitmap(ThreadSafeBitmap bitmap, WriteableBitmapHolder holder, Image image)
        {
            if (holder == null || bitmap == null)
                return;

            var changed = false;

            // Can not be interrupted by other threads. WritePixels returns true if the bitmap is changed.
            bitmap.Access(bitmapData =>
                changed = holder.WritePixels(bitmap.Width, bitmap.Height, bitmapData));

            // If bitmap is changed updates the source of the image.
            if (changed)
            {
                image.Source = holder.Bitmap;
            }

        }

        /// <summary>
        /// Gets the center of the viewport. Transforms the body 3d model around the center.
        /// Is activated after the scan is done to show the scanned 3d model on the screen.
        /// </summary>
        private void SetupViewportTransforms()
        {
            var geometry = ViewModel.Body3DModel;
            if (geometry == null)
                return;

            var center = new Vector3D(
                (geometry.Bounds.X + geometry.Bounds.SizeX) / 2,
                0,
                (geometry.Bounds.Z + geometry.Bounds.SizeZ) / 2);
            var translate = new TranslateTransform3D(-center);

            // TODO: Invert Y and align with floor normal in MeshConverter instead
            var invertY = new ScaleTransform3D(1, -1, 1);
            var alignWithFloor = new RotateTransform3D(GetFloorAlignment());

            var rotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
            var animation = (AnimationTimeline)FindResource("AngleAnimation");
            rotation.BeginAnimation(AxisAngleRotation3D.AngleProperty, animation);
            var rotate = new RotateTransform3D(rotation);
            model.Transform = new Transform3DGroup
            {
                Children = { translate, invertY, alignWithFloor, rotate }
            };
        }

        /// <summary>
        /// Is used to calculate the axis angle rotation to be used in SetupViewportTransforms() function.
        /// </summary>
        private Rotation3D GetFloorAlignment()
        {
            if (Math.Abs(ViewModel.FloorNormal.Y - 1) < 1e-4)
                return Rotation3D.Identity;

            var axis = Vector3D.CrossProduct(ViewModel.FloorNormal, new Vector3D(0, 1, 0));
            var angle = Math.Asin(axis.Length);
            return new AxisAngleRotation3D(axis, angle * 180 / Math.PI);
        }

        /// <summary>
        /// Passes the startRecording bool to the Reader_MultiSourceFrameArrived() function in ReconstructionController.cs
        /// It reads the frames if the startRecording variable is true.
        /// Stops the recording. Changes the ending time of the scan with Now + 0.1 seconds.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>       
        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            if (startRecording)
            {
                startRecording = false;
                ScanningEngine.scanEndTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(0.1));
            }
        }

        /// <summary>
        /// Passes the startRecording bool to the Reader_MultiSourceFrameArrived() function in ReconstructionController.cs
        /// It reads the frames if the startRecording variable is true.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>       
        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            if (startRecording == false)
            {
                startRecording = true;
            }
        }

        /// <summary>
        /// If shortScan checkbox is checked, SCAN_DURATION in ScanningEngine becomes 0.1 second.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>     
        private void shortScan_Checked(object sender, RoutedEventArgs e)
        {
            ScanningEngine.SCAN_DURATION = TimeSpan.FromSeconds(0.1);
        }

        /// <summary>
        /// Equalizes the scan duration with the slider.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>     
        private void shortScan_Unchecked(object sender, RoutedEventArgs e)
        {
            ScanningEngine.SCAN_DURATION = TimeSpan.FromSeconds(scanDurationSlider.Value);
        }

        /// <summary>
        /// scanDurationSlider value changed function. Removes the tick on shortScan cheeckbox when changed. Equalize the value to the SCAN_DURATION
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>    
        private void scanDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            shortScan.IsChecked = false;
            try
            {
                scanDurationSliderTxt.Text = Convert.ToString(scanDurationSlider.Value);
                ScanningEngine.SCAN_DURATION = TimeSpan.FromSeconds(scanDurationSlider.Value);
            }
            catch
            {

            }
        }

        /// <summary>
        /// Hides the current window and opens the selected one.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>    
        private void viewType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            viewType.SelectedIndex = 0;
            this.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Closes the all processes.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>    
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Environment.Exit(-1);
        }

        /// <summary>
        /// Creates and opens a new extras window if its not already created.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void extrasButton_Click(object sender, RoutedEventArgs e)
        {
            if (!WindowControl.extrasWindowExist)
            {
                ExtrasWindow extrasWindow = new ExtrasWindow();
                extrasWindow.Top = this.Top;
                extrasWindow.Left = this.Left + 998;
                extrasWindow.Visibility = Visibility.Visible;
                WindowControl.extrasWindowVisible = true;
                WindowControl.extrasWindowExist = true;
            }
        }

        /// <summary>
        /// Passes the bool to the AppViewModel.cs SaveBodyModel() function.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void pcdCheck_Checked(object sender, RoutedEventArgs e)
        {
            AppViewModel.convertToPCD = true;
        }

        /// <summary>
        /// Passes the bool to the AppViewModel.cs SaveBodyModel() function.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void pcdCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            AppViewModel.convertToPCD = false;
        }

        /// <summary>
        /// Passes the saving format to the AppViewModel.cs SaveBodyModel() function.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void stlFormat_Checked(object sender, RoutedEventArgs e)
        {
            AppViewModel.saveFormat = ".stl";
        }

        /// <summary>
        /// Passes the saving format to the AppViewModel.cs SaveBodyModel() function.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void objFormat_Checked(object sender, RoutedEventArgs e)
        {
            AppViewModel.saveFormat = ".obj";
        }

        /// <summary>
        /// Passes the saving format to the AppViewModel.cs SaveBodyModel() function.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void plyFormat_Checked(object sender, RoutedEventArgs e)
        {
            AppViewModel.saveFormat = ".ply";
        }

        /// <summary>
        /// Converts the given 3d object into pcd file then opens it with the viewer.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void converterButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dialog.DefaultExt = "*.*";
            dialog.Filter = "All files (*.*)|*.*";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dialog.ShowDialog();


            // Get the selected file name and display in a TextBox 
            if (result == true)
            {

                string filePath = dialog.FileName;
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string directoryName = Path.GetDirectoryName(dialog.FileName);
                string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();

                if (extension == ".stl" || extension == ".obj" || extension == ".ply")
                {
                    Process cmd = new Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.RedirectStandardInput = true;
                    cmd.StartInfo.RedirectStandardOutput = true;
                    cmd.StartInfo.CreateNoWindow = true;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.Start();

                    cmd.StandardInput.WriteLine(Directory.GetCurrentDirectory() + "\\pcl_converter_release.exe -c " + filePath + " " + directoryName + "\\" + fileName + ".pcd");
                    cmd.StandardInput.Flush();
                    cmd.StandardInput.Close();
                    cmd.WaitForExit();
                    Console.WriteLine(cmd.StandardOutput.ReadToEnd());

                    Process cmd2 = new Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.RedirectStandardInput = true;
                    cmd.StartInfo.RedirectStandardOutput = true;
                    cmd.StartInfo.CreateNoWindow = true;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.Start();

                    cmd.StandardInput.WriteLine(Directory.GetCurrentDirectory() + "\\pcl_viewer_release.exe " + directoryName + "\\" + fileName + ".pcd");
                    cmd.StandardInput.Flush();
                    cmd.StandardInput.Close();
                    cmd.WaitForExit();
                    Console.WriteLine(cmd.StandardOutput.ReadToEnd());
                }
            }
        }

        /// <summary>
        /// Is used to view the given pcd file.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void viewerButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dialog.DefaultExt = "*.*";
            dialog.Filter = "All files (*.*)|*.*";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dialog.ShowDialog();


            // Get the selected file name and display in a TextBox 
            if (result == true)
            {

                string filePath = dialog.FileName;                              
                string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();

                if (extension == ".pcd")
                {
                    Process cmd = new Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.RedirectStandardInput = true;
                    cmd.StartInfo.RedirectStandardOutput = true;
                    cmd.StartInfo.CreateNoWindow = true;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.Start();

                    cmd.StandardInput.WriteLine(Directory.GetCurrentDirectory() + "\\pcl_viewer_release.exe " + filePath);
                    cmd.StandardInput.Flush();
                    cmd.StandardInput.Close();
                    cmd.WaitForExit();
                    Console.WriteLine(cmd.StandardOutput.ReadToEnd());
                }
            }

            
        }

        /// <summary>
        /// Changes the distance for static kinect mod.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void staticKinectDistance1_Checked(object sender, RoutedEventArgs e)
        {
            ReconstructionController.MIN_DEPTH = 0.5f;
            ReconstructionController.MAX_DEPTH = 1.5f;
         
        }

        /// <summary>
        /// Changes the distance for static kinect mod.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void staticKinectDistance2_Checked(object sender, RoutedEventArgs e)
        {
            ReconstructionController.MIN_DEPTH = 1.5f;
            ReconstructionController.MAX_DEPTH = 3.5f;
        }

        /// <summary>
        /// Changes the distance for static kinect mod.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void staticKinectDistance3_Checked(object sender, RoutedEventArgs e)
        {
            ReconstructionController.MIN_DEPTH = 3.0f;
            ReconstructionController.MAX_DEPTH = 5.0f;
        }

        /// <summary>
        /// Merges the given pcd file and csv file into a csv file. Puts the given csv file to the above.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void MergeCoordinates_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
      
            dialog.DefaultExt = "*.pcd";
            dialog.Filter = "PCD File(*.pcd)|*.*";
            dialog.FileName = "PointCloud.pcd";
            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dialog.ShowDialog();

            Microsoft.Win32.OpenFileDialog dialog2 = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 

            dialog2.DefaultExt = "*.csv";
            dialog2.Filter = "CSV File(*.csv)|*.*";
            dialog2.FileName = "SkeletonCSV.csv";
            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result2 = dialog2.ShowDialog();

            var dialog3 = new SaveFileDialog
            {
                Title = "Save Merged Point Cloud",
                Filter = "CSV File|*.csv",
                FilterIndex = 0,
                DefaultExt = ".csv"
            };
            dialog3.FileName = "MergedCSV.csv";
            
            // Get the selected file name and display in a TextBox 
            if (result == true && result2 == true && dialog3.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();

                string filePath2 = dialog2.FileName;
                string extension2 = Path.GetExtension(dialog2.FileName).ToLowerInvariant();

                string filePath3 = dialog3.FileName;
                
                if (extension == ".pcd" && extension2 == ".csv")
                {
                    string strCmdText;
                    strCmdText = "/C " + Directory.GetCurrentDirectory() + "\\SubPrograms m " + filePath + " " + filePath2 + " " + filePath3;
                    System.Diagnostics.Process.Start("CMD.exe", strCmdText);
                }
            }                          
        }

        /// <summary>
        /// Is used to filter the given pcd file.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void FilterPointCloud_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 

            dialog.DefaultExt = "*.pcd";
            dialog.Filter = "PCD File(*.pcd)|*.*";
            dialog.FileName = "PointCloudToFilter.pcd";
            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dialog.ShowDialog();
            string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            string filePath = dialog.FileName;

            var dialog2 = new SaveFileDialog
            {
                Title = "Save Filtered PCD",
                Filter = "PCD File(*.pcd)|*.*",
                FilterIndex = 0,
                DefaultExt = ".pcd"
            };

            if (extension == ".pcd")
            {
                dialog2.FileName = Path.GetFileNameWithoutExtension(filePath) + "_filtered"; ;
            }

            // Get the selected file name and display in a TextBox 
            if (result == true && dialog2.ShowDialog() == true)
            {
                string filePath2 = dialog2.FileName;

                if (extension == ".pcd")
                {
                    string strCmdText;
                    strCmdText = "/C " + Directory.GetCurrentDirectory() + "\\SubPrograms f " + filePath + " " + filePath2;
                    System.Diagnostics.Process.Start("CMD.exe", strCmdText);
                }
            }
        }

        /// <summary>
        /// Is used to smooth the given pcd file.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>   
        private void SmootherButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 

            dialog.DefaultExt = "*.pcd";
            dialog.Filter = "PCD File(*.pcd)|*.*";
            dialog.FileName = "PointCloudToSmooth.pcd";
            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dialog.ShowDialog();
            string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            string filePath = dialog.FileName;

            var dialog2 = new SaveFileDialog
            {
                Title = "Save Smoothed PCD",
                Filter = "PCD File(*.pcd)|*.*",
                FilterIndex = 0,
                DefaultExt = ".pcd"
            };

            if (extension == ".pcd")
            {
                dialog2.FileName = Path.GetFileNameWithoutExtension(filePath) + "_smoothed"; ;
            }

            // Get the selected file name and display in a TextBox 
            if (result == true && dialog2.ShowDialog() == true)
            {
                string filePath2 = dialog2.FileName;

                if (extension == ".pcd")
                {
                    string strCmdText;
                    strCmdText = "/C " + Directory.GetCurrentDirectory() + "\\SubPrograms s " + filePath + " " + filePath2;
                    System.Diagnostics.Process.Start("CMD.exe", strCmdText);
                }
            }
        }

        /// <summary>
        /// Passes the current height and width to the WindowControl.cs
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param> 
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {           
            WindowControl.windowHeight = Application.Current.MainWindow.Height;
            WindowControl.windowWidth = Application.Current.MainWindow.Width;
        }

        /// <summary>
        /// Gets the last changed windows height and width.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param> 
        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.Visibility == Visibility.Visible)
                Application.Current.MainWindow = this;
            Application.Current.MainWindow.Height = WindowControl.windowHeight;
            Application.Current.MainWindow.Width = WindowControl.windowWidth;
        }
    }
}
