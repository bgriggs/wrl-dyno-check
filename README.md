![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/bgriggs/wrl-dyno-check/build.yml)

# WRL Dyno Check
This application processes runs from Dynojet dynamometers and checks for compliance with the World Racing League (WRL) flat curve ruleset.

![alt text](https://github.com/bgriggs/wrl-dyno-ckeck/blob/9834a12132982ebbc2bfeba9f3075a243a788cc2/BigMission.WrlDynoCheck/BigMission.WrlDynoCheck/Assets/screenshot1.png?raw=true)

## Installation
Download the latest release install from https://bgriggs.github.io/wrl-dyno-check/BigMission.WrlDynoCheck.Desktop.application

Accept the firewall settings to allow the application to communicate with the Dynojet WinPep 8 software.

## Usage
The application allow for two modes of operation. 
1. Live connection to Dynojet to process runs as they are completed during tuning.
2. Load an exported CSV file from Dynojet WinPep 8 software to process runs that have already been completed.

Once a run is loaded, the Power series will be plotted. The application will overlay the 999.9 RPM range that does not receive a penalty along with a dashed intercept line.

Additional series will be added that overlay on the Power series. These run for the length of the flatness and will change color based on each progressive penalty.

Total penalties will be calculated and displayed in the upper center part of the application. 

Additional information is provided to help in assisting with making tuning adjustments. The "lower" values refer to the left side of the chart before peak power. The "upper" values refer to the right side of the chart after peak power.

### Dynojet Live Connection
To connect to a Dynojet dynamometer, the application must be running on a Windows machine with the Dynojet WinPep 8 software installed. The WinPep 8 software must be running and connected to the dynamometer. The application will automatically connect to the WinPep 8 software and process runs as they are completed.

Ensure the WinPep 8 software is configured to allow JETDRIVE. This can be done by selecting the "Options" menu, then "Settings", then "JETDRIVE". Select the interface that is connected to the internet, typically ethernet or WiFi.

![alt text](https://github.com/bgriggs/wrl-dyno-ckeck/blob/9834a12132982ebbc2bfeba9f3075a243a788cc2/BigMission.WrlDynoCheck/BigMission.WrlDynoCheck/Assets/screenshot-jetdrive-settings.png?raw=true)

### Load CSV File
To load a CSV file, select the "Open CSV File" folder button in the upper left of the application. Select the CSV file that was exported from the Dynojet WinPep 8 software. The application will process the runs in the CSV file.

When exporting from WinPep 8, right-click the run and select Export. Select the Time Export tab and ensure 50ms is selected. This will export the run data in a format that can be processed by the application.
![alt text](https://github.com/bgriggs/wrl-dyno-ckeck/blob/main/BigMission.WrlDynoCheck/BigMission.WrlDynoCheck/Assets/screenshot-dynojet-export-options.png?raw=true)
