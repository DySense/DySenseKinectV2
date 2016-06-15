Requires Windows 8 or newer - 64 bit.

Building from source:
 - Download visual studio express for desktop
 - Install official Microsoft SDK for Kinect 2.0
 - Download and run Kinect Configuration Verifier Tool and make sure you can get consistent frame rates.  If the tool gives a warning about the USB controller that can usually be ignored.
 - In the Configuration Manager in VS select Release and x64
 - Try to build solution
 - If it complains about NuGet packages missing then right click on solution and click "Manage Nuget Packages" and then click Restore
 - Make sure 
 - If you make any changes you should just need to copy the DySenseKinectV2.exe from /DySenseKinectV2/bin/x64/Release and overwrite the one already stored in DySense.