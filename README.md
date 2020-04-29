# G2Q

G2Q is a Qlik Sense data connector for importing GDX files into a Qlik application, and has been developed by the European Commission's Joint Research Centre (JRC).

* Qlik Sense is a business intelligence platform, used for analyzing and disseminating dashboards.

* GDX files are generated by Gams, that is a high-level modeling system for mathematical programming and optimization.

## Prerequisites

### Download and install the latest Gams

Gams has to be installed in the machine where Qlik Sense is running.
* Download a GAMS distribution from https://www.gams.com/latest/ 
* For developing purposes it is not necessary to have a valid license, GAMS will operate as a free demo system.
* The installation is quite straight forward by following the wizard.

### Download and install the latest Qlik Sense Desktop or Qlik Sense Server

Access to a Qlik Sense licence and installation is needed
* To download the latest version, you can visit https://demo.qlik.com/download/ use the left column to navigate to the last version of Qlik sense desktop. The main reason to use the desktop (stand-alone) version is for debug.
* Also, in this download site, click in “View All/Search” and find the SDK. We need to download the latest SDK version because we need the “QvxLibray.dll” it provides. The file to download at the moment of writing this guide is “QvxSDK_2.1x64”.
* To use Qlik sense desktop we need a registered Qlik user account. Go to https://www.qlik.com/es-es/try-or-buy/download-qlik-sense if you need to create one.
* Like GAMS, the installation is quite simple by following the wizard.

## Installation

* Download the "bin" folder
* Locate Qlik connectors folder, typically C:\Users\\[User]\AppData\Local\Programs\Common Files\Qlik\Custom Data\
* Copy the folder "bin" with its content, giving it a specific name like "GamsConnector"
* Copy the file "GAMS.net4.dll" located at "[GAMS install directory]\[Latest version]" to the "GamsConnector" folder
* Copy the file "QvxLibrary.dll" located into Qlik SDK file downloaded earlier to the "GamsConnector" folder.
* Restart Qlik Sense server or Qlik Sense Desktop

## How to use

TODO

## Compilation

Download Visual Studio
1. Create a new C# console project.
2. Set “x64” as “Platform target” 
3. Add as reference “GAMS.net4.dll” located at “[GAMS install directory]\[Latest version]” e.g. “C:\Program Files\25.1”
4. Add as reference “QvxLibrary.dll” located into SDK file downloaded earlier.
5. Check that both references has been correctly added
6. Compile the project using Visual Studio
7. Follow the next step:
[Making your connector recognizable by Qlik Sense or QlikView](https://help.qlik.com/en-US/qlikview-developer/April2019/Subsystems/QVXSDKAPI/Content/QV_QVXSDKAPI/Making-connector-recognizable-by-QlikView.htm)

In order to install the compiled connector, the "web" folder has to be copied from the "src" alongside the generated exe files.

## Used APIs

### Qlik SDK

TODO

### Gams API

TODO

## License

Check the [LICENSE](LICENSE) file
