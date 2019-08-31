using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BodyScanner
{
    /// <summary>
    /// A helper class to pass some values between different windows.
    /// </summary>
    public class WindowControl
    {
        /// <summary>
        /// Checks extras window is already created or not
        /// </summary>
        public static bool extrasWindowExist = false;

        /// <summary>
        /// Checks extras window is visible or not
        /// </summary>
        public static bool extrasWindowVisible = false;

        /// <summary>
        /// Variable for extras window skeleton tracking to check wait mesh is checked or unchecked
        /// </summary>
        public static bool waitMesh = false;

        /// <summary>
        /// Variable for extras window skeleton tracking to check mesh is started or not
        /// </summary>
        public static bool meshStarted = false;

        /// <summary>
        /// Starts saving skeleton
        /// </summary>
        public static bool startSavingSkeleton = false;

        /// <summary>
        /// Variable for extras window skeleton tracking to check it will pass the name of skeleton coordinates .csv is created or will not
        /// </summary>
        public static bool namePass = false;

        /// <summary>
        /// Holder of the name of the skeleton csv file
        /// </summary>
        public static string nameHolder;

        /// <summary>
        /// Holder of window height for all windows.
        /// </summary>
        public static double windowHeight = 860;

        /// <summary>
        /// Holder of window width for static window and moving window.
        /// </summary>
        public static double windowWidth = 1000;

        public static string degree;
        public static string spineBase_X;
        public static string spineBase_Y;
        public static string spineBase_Z;
        public static string spineMid_X;
        public static string spineMid_Y;
        public static string spineMid_Z;
        public static string neck_X;
        public static string neck_Y;
        public static string neck_Z;
        public static string head_X;
        public static string head_Y;
        public static string head_Z;
        public static string shoulderLeft_X;
        public static string shoulderLeft_Y;
        public static string shoulderLeft_Z;
        public static string elbowLeft_X;
        public static string elbowLeft_Y;
        public static string elbowLeft_Z;
        public static string wristLeft_X;
        public static string wristLeft_Y;
        public static string wristLeft_Z;
        public static string handLeft_X;
        public static string handLeft_Y;
        public static string handLeft_Z;
        public static string shoulderRight_X;
        public static string shoulderRight_Y;
        public static string shoulderRight_Z;
        public static string elbowRight_X;
        public static string elbowRight_Y;
        public static string elbowRight_Z;
        public static string wristRight_X;
        public static string wristRight_Y;
        public static string wristRight_Z;
        public static string handRight_X;
        public static string handRight_Y;
        public static string handRight_Z;
        public static string hipLeft_X;
        public static string hipLeft_Y;
        public static string hipLeft_Z;
        public static string kneeLeft_X;
        public static string kneeLeft_Y;
        public static string kneeLeft_Z;
        public static string ankleLeft_X;
        public static string ankleLeft_Y;
        public static string ankleLeft_Z;
        public static string footLeft_X;
        public static string footLeft_Y;
        public static string footLeft_Z;
        public static string hipRight_X;
        public static string hipRight_Y;
        public static string hipRight_Z;
        public static string kneeRight_X;
        public static string kneeRight_Y;
        public static string kneeRight_Z;
        public static string ankleRight_X;
        public static string ankleRight_Y;
        public static string ankleRight_Z;
        public static string footRight_X;
        public static string footRight_Y;
        public static string footRight_Z;
        public static string spineShoulder_X;
        public static string spineShoulder_Y;
        public static string spineShoulder_Z;
        public static string handTipLeft_X;
        public static string handTipLeft_Y;
        public static string handTipLeft_Z;
        public static string thumbLeft_X;
        public static string thumbLeft_Y;
        public static string thumbLeft_Z;
        public static string handTipRight_X;
        public static string handTipRight_Y;
        public static string handTipRight_Z;
        public static string thumbRight_X;
        public static string thumbRight_Y;
        public static string thumbRight_Z;


    }   
}
