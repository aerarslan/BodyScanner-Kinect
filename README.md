# BodyScanner-Kinect
An app to create 3D model of a body with using Kinect. The output can be used to measure body parts.

## [Source Code of SubPrograms](https://github.com/aerarslan/PCL-Filter-Merge-Smooth)
## [Calculate Circumference of Body Parts with the Output of BodyScanner-Kinect](https://github.com/aerarslan/Body-Part-Circumference-Finder)

## HOW TO USE BODY SCANNER
Convert, view, filter, smooth, merge features is used after a 3d scan is done and saved.
Saving human body and coordinates of skeleton joints:
*	Open the app with BodyScanner.exe
*	Click Toggle Extras button to activate skeleton recording.
*	Be sure extras window is opened during mesh recording.
*	Be sure Wait Mesh to Save Skeleton check box in extras window is checked. (Default is checked)
*	Choose the mod you want to record mesh.
* If you choose Static Kinect Mod:
  * Check Short Scan to get 1 sec mesh.
  * Be sure Scan Duration is more than 10 sec.
  * Be sure you choose the right Distance.
  * Stay 2.5m away from kinect (distance depands on what you choose) and click Start Mesh button.
  * You can stop recording mesh manually with Stop Integration button or It will stop automatically when the duration time passes.
  * After recording is done, be sure PCD checkbox is check if you want point cloud of mesh. If It is not, you will save the mesh in one of the 3d object formats you choose. Also you can manually convert 3d objects into PCD format with Convert Mesh to PCD button later.
  * Click Save Model to save mesh, enter the name of skeleton.csv and the name of 3d object. 
* If you choose Moving Kinect Mod:
  * Be sure kinect sees your skeleton properly.
  * Click Start Mesh button to see how mesh looks.
  * You can always click Start Mesh button to start the mesh from the beginning. It takes new skeleton coordinates whenever you click Start Mesh button again.
  * Change the parameters with sliders to find best option for you.
  * Be sure Integration Weight is around 300. More than 300 can cause a big data and PCD options can not handle with that big data.
  * When you prepare a good mesh, click Stop Integration and the mesh you record will be stay without scattered.
  * After recording is done, be sure PCD checkbox is check if you want point cloud of mesh. If It is not, you will save the mesh in one of the 3d object formats you choose. Also you can manually convert 3d objects into PCD format with Convert Mesh to PCD button later.
  * Click Save Model to save mesh, enter the name of skeleton.csv and the name of 3d object. 
  
# Using Convert, View, Filter, Smooth, Merge Buttons:

* Convert Mesh to PCD: Converts 3d object into PCD. After you click the button, you have to choose the 3d object that you want to convert into PCD file. PCD format of 3d object will be saved into the same directory of 3d object.
* View PCD: Visualizes a given PCD file. After you click the button, you have to choose a PCD file to view.
* Filter PCD: Filters a given PCD file. After you click the button, you have to choose a PCD file that you want to be filtered. Then you have to enter the name of the new filtered PCD file.
* Smooth PCD: Smooths a given PCD file. After you click the button, you have to choose a PCD file that you want to be smoothed. Then you have to enter the name of the new smoothed PCD file.
* Merge Skeleton/Body: Merges the saved skeleton.csv file with the saved PCD file of 3d scan. After you click the button, you have to choose a PCD file and a CSV file that is the skeleton coordinates of the PCD file. Then you have to enter the name of the new merged CSV file.
