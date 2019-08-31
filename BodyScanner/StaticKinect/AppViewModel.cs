// 
// Based on code from https://github.com/baSSiLL/BodyScanner
//
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Media3D;


namespace BodyScanner
{
    /// <summary>
    /// The main class of whole scanning process. Also responsible from saving mesh.
    /// </summary>
    internal class AppViewModel : ViewModelBase
    {
        /// <summary>
        /// Main class to control scanning. 
        /// </summary>
        private readonly ScanningEngine engine;

        /// <summary>
        /// Kinect frame renderer object
        /// </summary>
        private readonly KinectFrameRenderer renderer;

        /// <summary>
        /// Shows errors, messages to the user.
        /// </summary>
        private readonly UserInteractionService uis;

        /// <summary>
        /// Checks all requirements for scan to start, checks the conditions to finish the scan. Updates the scan.
        /// </summary>
        /// <param name="engine">Engine that holds the scan data.</param>
        /// <param name="renderer">The main frame renderer.</param>
        /// <param name="uis">User Interaction service</param>
        public AppViewModel(ScanningEngine engine, KinectFrameRenderer renderer, UserInteractionService uis)
        {
            Contract.Requires(engine != null);
            Contract.Requires(renderer != null);

            this.engine = engine;
            this.renderer = renderer;
            this.uis = uis;

            // Delegate Command checks for a bool and execute an action. CanStartScanning is the bool and DoScanning is the action.
            startScanningCommand = new DelegateCommand(DoScanning, CanStartScanning);
            saveModelCommand = new DelegateCommand(SaveBodyModel, () => engine.ScannedMesh != null);

            engine.ScanStarted += Engine_ScanStarted;
            engine.ScanUpdated += Engine_ScanUpdated;
        }

        /// <summary>
        /// Prompt to get values from Resources.resx
        /// </summary>
        public string Prompt
        {
            get { return prompt; }
            private set { SetPropertyValue(value, ref prompt); }
        }
        private string prompt;

        /// <summary>
        /// Defines a start scanning command
        /// </summary>
        public ICommand StartScanningCommand => startScanningCommand;

        /// <summary>
        /// A delegate command object to check the start of the scannning.
        /// </summary>
        private DelegateCommand startScanningCommand;

        /// <summary>
        /// Checks for the situation of the scanning process. 
        /// </summary>
        public bool IsScanning
        {
            get { return isScanning; }
            private set { SetPropertyValue(value, ref isScanning); }
        }
        private bool isScanning;

        /// <summary>
        /// Checks the requirements of scanning. If everything is fine, starts scanning.
        /// </summary>
        private async void DoScanning()
        {
            if (!CanStartScanning())
                throw new InvalidOperationException("Cannot start scanning");

            IsScanning = true;
            Body3DModel = null;          

            try
            {
                // engine updates the scan and gets the mesh.
                await engine.Run();
                Prompt = Properties.Resources.PromptScanCompleted;
                FloorNormal = new Vector3D(engine.FloorNormal.X, engine.FloorNormal.Y, engine.FloorNormal.Z);
                Body3DModel = engine.ScannedMesh == null
                    ? null
                    : MeshConverter.Convert(engine.ScannedMesh);

              
            }
            catch (ApplicationException ex)
            {
                uis.ShowError(ex.Message);
                Prompt = Properties.Resources.PromptScanAborted;
            }

            ShowScanBitmap = false;
            
            IsScanning = false;

            await Task.Delay(TimeSpan.FromSeconds(5));
           
        }

        /// <summary>
        /// Returns the opposite of isScanning.
        /// </summary>
        private bool CanStartScanning()
        {
            return !isScanning;
        }

        /// <summary>
        /// Binded to scanImage in StaticKinectWindow. Becomes true when scan is started and false when It is ended.
        /// </summary>
        public bool ShowScanBitmap
        {
            get { return showScanBitmap; }
            private set { SetPropertyValue(value, ref showScanBitmap); }
        }
        private bool showScanBitmap;

        /// <summary>
        /// Gets the surface Bitmap.
        /// </summary>
        public ThreadSafeBitmap ScanBitmap => engine.ScanBitmap;

        /// <summary>
        /// Holds the 3d model. Gets it from engine.ScannedMesh
        /// </summary>
        public MeshGeometry3D Body3DModel
        {
            get { return body3DModel; }
            private set { SetPropertyValue(value, ref body3DModel); }
        }
        private MeshGeometry3D body3DModel;

        /// <summary>
        /// Vector3D object to hold engine.FloorNormal.X, Y and Z
        /// </summary>
        public Vector3D FloorNormal
        {
            get { return floorNormal; }
            private set { SetPropertyValue(value, ref floorNormal); }
        }
        private Vector3D floorNormal;

        /// <summary>
        /// Becomes true after scan is end and if the body model is not null.
        /// </summary>
        public bool ShowBodyModel
        {
            get { return Body3DModel != null; }
        }

        /// <summary>
        /// Defines a save model command.
        /// </summary>
        public ICommand SaveModelCommand => saveModelCommand;

        /// <summary>
        /// A deletgate command object to check the saving process. Activates SaveBodyModel function if engine. ScannedMesh is not null.
        /// </summary>
        private readonly DelegateCommand saveModelCommand;

        /// <summary>
        /// Returns true If user wants to convert 3d mesh into Point Cloud Data
        /// </summary>
        public static bool convertToPCD = false;

        /// <summary>
        /// Hold the save format.
        /// </summary>
        public static string saveFormat = ".stl";

        /// <summary>
        /// Skeleton csv file name.
        /// </summary>
        public string fileName;

        /// <summary>
        /// Saves the mesh with using ModelIO.cs. Saves the skeleton.csv, gets the values from WindowControl.cs.
        /// </summary>
        private void SaveBodyModel()
        {

            var dialogCSV = new SaveFileDialog
            {
                Title = "Save Model",
                Filter = "CSV|*.csv",
                FilterIndex = 0,
                DefaultExt = ".csv",
                InitialDirectory = Directory.GetCurrentDirectory() + "\\Recorded_Data"
            };

            string dateTime = DateTime.Now.ToString("yyyyMMdd_hhmmss");
            dialogCSV.FileName = dateTime;


            if (dialogCSV.ShowDialog() == true)
            {
                fileName = dialogCSV.FileName;
                using (var writer = File.CreateText(dialogCSV.FileName))
                {
                    writer.Write("Joints," + "X," + "Y," + "Z");
                    writer.WriteLine();
                }
            }

            WindowControl.namePass = true;
            WindowControl.nameHolder = Path.GetFileNameWithoutExtension(dialogCSV.FileName);


            using (StreamWriter writer = new StreamWriter(fileName, true))
            {
                writer.Write("Degree" + "," + WindowControl.degree + "," + WindowControl.degree + "," + WindowControl.degree + "\nSpineBase" + "," + WindowControl.spineBase_X + "," + WindowControl.spineBase_Y + "," + WindowControl.spineBase_Z + "\nSpineMid" + "," + WindowControl.spineMid_X + "," + WindowControl.spineMid_Y + "," + WindowControl.spineMid_Z + "\nNeck" + "," + WindowControl.neck_X + "," + WindowControl.neck_Y + "," + WindowControl.neck_Z
                            + "\nHead" + "," + WindowControl.head_X + "," + WindowControl.head_Y + "," + WindowControl.head_Z + "\nShoulderLeft" + "," + WindowControl.shoulderLeft_X + "," + WindowControl.shoulderLeft_Y + "," + WindowControl.shoulderLeft_Z + "\nElbowLeft" + "," + WindowControl.elbowLeft_X + "," + WindowControl.elbowLeft_Y + "," + WindowControl.elbowLeft_Z
                            + "\nWristLeft" + "," + WindowControl.wristLeft_X + "," + WindowControl.wristLeft_Y + "," + WindowControl.wristLeft_Z + "\nHandLeft" + "," + WindowControl.handLeft_X + "," + WindowControl.handLeft_Y + "," + WindowControl.handLeft_Z + "\nShoulderRight" + "," + WindowControl.shoulderRight_X + "," + WindowControl.shoulderRight_Y + "," + WindowControl.shoulderRight_Z
                            + "\nElbowRight" + "," + WindowControl.elbowRight_X + "," + WindowControl.elbowRight_Y + "," + WindowControl.elbowRight_Z + "\nWristRight" + "," + WindowControl.wristRight_X + "," + WindowControl.wristRight_Y + "," + WindowControl.wristRight_Z + "\nHandRight" + "," + WindowControl.handRight_X + "," + WindowControl.handRight_Y + "," + WindowControl.handRight_Z
                            + "\nHipLeft" + "," + WindowControl.hipLeft_X + "," + WindowControl.hipLeft_Y + "," + WindowControl.hipLeft_Z + "\nKneeLeft" + "," + WindowControl.kneeLeft_X + "," + WindowControl.kneeLeft_Y + "," + WindowControl.kneeLeft_Z + "\nAnkleLeft" + "," + WindowControl.ankleLeft_X + "," + WindowControl.ankleLeft_Y + "," + WindowControl.ankleLeft_Z
                            + "\nFootLeft" + "," + WindowControl.footLeft_X + "," + WindowControl.footLeft_Y + "," + WindowControl.footLeft_Z + "\nHipRight" + "," + WindowControl.hipRight_X + "," + WindowControl.hipRight_Y + "," + WindowControl.hipRight_Z + "\nKneeRight" + "," + WindowControl.kneeRight_X + "," + WindowControl.kneeRight_Y + "," + WindowControl.kneeRight_Z
                            + "\nAnkleRight" + "," + WindowControl.ankleRight_X + "," + WindowControl.ankleRight_Y + "," + WindowControl.ankleRight_Z + "\nFootRight" + "," + WindowControl.footRight_X + "," + WindowControl.footRight_Y + "," + WindowControl.footRight_Z + "\nSpineShoulder" + "," + WindowControl.spineShoulder_X + "," + WindowControl.spineShoulder_Y + "," + WindowControl.spineShoulder_Z
                            + "\nHandTipLeft" + "," + WindowControl.handTipLeft_X + "," + WindowControl.handTipLeft_Y + "," + WindowControl.handTipLeft_Z + "\nThumbLeft" + "," + WindowControl.thumbLeft_X + "," + WindowControl.thumbLeft_Y + "," + WindowControl.thumbLeft_Z + "\nHandTipRight" + "," + WindowControl.handTipRight_X + "," + WindowControl.handTipRight_Y + "," + WindowControl.handTipRight_Z
                            + "\nThumbRight" + "," + WindowControl.thumbRight_X + "," + WindowControl.thumbRight_Y + "," + WindowControl.thumbRight_Z);
                writer.WriteLine();
            }

            var dialog = new SaveFileDialog
            {
                Title = "Save Model",
                Filter = "Wavefront OBJ|*.obj|STL (binary)|*.stl|PLY (text)|*.ply",
                FilterIndex = 0,
                DefaultExt = ".obj"
            };

            // Checks If the 3d object will have the same name with skeleton.csv.
            if(WindowControl.namePass)
            {
                dialog.FileName = WindowControl.nameHolder;
            }
            else
            {
                dialog.FileName = DateTime.Now.ToString("yyyyMMdd_hhmmss");
            }

            if(saveFormat == ".obj")
            {
                dialog.Filter = "OBJ|*.obj";
                dialog.DefaultExt = ".obj";
            }
            else if(saveFormat == ".stl")
            {
                dialog.Filter = "STL|*.stl";
                dialog.DefaultExt = ".stl";
            }
            else if(saveFormat == ".ply")
            {
                dialog.Filter = "PLY|*.ply";
                dialog.DefaultExt = ".ply";
            }
            
            if (dialog.ShowDialog() == true)
            {
                var flipAxes = true;
                switch (Path.GetExtension(dialog.FileName).ToLowerInvariant())
                {
                    case ".obj":
                        using (var writer = File.CreateText(dialog.FileName))
                        {
                            ModelIO.SaveAsciiObjMesh(engine.ScannedMesh, writer, flipAxes);
                        }
                        break;
                    case ".stl":
                        using (var file = File.Create(dialog.FileName))
                        using (var writer = new BinaryWriter(file))
                        {
                            ModelIO.SaveBinaryStlMesh(engine.ScannedMesh, writer, flipAxes);
                        }
                        break;
                    case ".ply":
                        using (var writer = File.CreateText(dialog.FileName))
                        {
                            ModelIO.SaveAsciiPlyMesh(engine.ScannedMesh, writer, flipAxes);
                        }
                        break;
                    default:
                        uis.ShowError("Unsupported file format");
                        break;
                }
                
                // If convertToPCD is true, executes a sub program called pcl_converter_release from Point Cloud Library.
                if (convertToPCD)
                {
                    string filePath = dialog.FileName;
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string directoryName = Path.GetDirectoryName(dialog.FileName);
                    string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();

                    Process cmd = new Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.RedirectStandardInput = true;
                    cmd.StartInfo.RedirectStandardOutput = true;
                    cmd.StartInfo.CreateNoWindow = true;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.Start();

                    cmd.StandardInput.WriteLine(Directory.GetCurrentDirectory() + "\\pcl_converter_release -c " + filePath + " " + directoryName + "\\" + fileName + ".pcd");
                    cmd.StandardInput.Flush();
                    cmd.StandardInput.Close();
                    cmd.WaitForExit();
                    Console.WriteLine(cmd.StandardOutput.ReadToEnd());

                    // After converting, starts a new process to view the converted PCD file.
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
        /// Shows the scan bit map after scan is started. Updates the scan bitmap.
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void Engine_ScanStarted(object sender, EventArgs e)
        {
            ShowScanBitmap = true;
            Prompt = Properties.Resources.PromptScanning;

            OnScanUpdated();
        }

        /// <summary>
        /// Shows the scan bit map after scan is started. Updates the scan bitmap.        
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>

        private void Engine_ScanUpdated(object sender, EventArgs e)
        {          
            OnScanUpdated();
        }

        /// <summary>
        /// Updates the Scan Bitmap
        /// </summary>
        private void OnScanUpdated()
        {          
            OnPropertyChanged(nameof(ScanBitmap));
        }

        /// <summary>
        /// OnPropertyChanged method to update values.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            switch (propertyName)
            {
                case nameof(IsScanning):
                    startScanningCommand.InvalidateCanExecute();
                    break;
                case nameof(Body3DModel):
                    OnPropertyChanged(nameof(ShowBodyModel));
                    WindowControl.meshStarted = false;                  
                    saveModelCommand.InvalidateCanExecute();
                    break;
            }
        }
    }
}
