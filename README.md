# G2Q

G2Q is a Qlik Sense data connector for importing GDX files into a Qlik application, and has been developed by the European Commission's Joint Research Centre (JRC).

* **[Qlik Sense](https://www.qlik.com/es-es/products/qlik-sense)** is a business intelligence platform, used for analyzing and disseminating dashboards.

* **GDX (Gams Data eXchange)** files are generated by [Gams](https://www.gams.com/), that is a high-level modeling system for mathematical programming and optimization.

## Prerequisites

### Download and install the latest Gams

Gams has to be installed in the machine where Qlik Sense is running.
* Download a GAMS distribution from https://www.gams.com/latest/ 
* For developing purposes it is not necessary to have a valid license, GAMS will operate as a free demo system.
* The installation is quite straight forward by following the wizard.

The development was performed and tested with **Gams DLL v25.1.1**

### Download and install the latest Qlik Sense Desktop or Qlik Sense Server

Access to a Qlik Sense licence and installation is needed
* To download the latest version, you can visit https://demo.qlik.com/download/ use the left column to navigate to the last version of Qlik sense desktop. The main reason to use the desktop (stand-alone) version is for debug.
* Also, in this download site, click in “View All/Search” and find the SDK. We need to download the latest SDK version because we need the “QvxLibray.dll” it provides. The file to download at the moment of writing this guide is “QvxSDK_2.1x64”.
* To use Qlik sense desktop we need a registered Qlik user account. Go to https://www.qlik.com/es-es/try-or-buy/download-qlik-sense if you need to create one.
* Like GAMS, the installation is quite simple by following the wizard.

The development was performed and tested with **QvxLibrary DLL v2.1.4**

## Installation

* Download the "bin" folder
* Locate Qlik connectors folder, typically C:\Users\\[User]\AppData\Local\Programs\Common Files\Qlik\Custom Data\
* Copy the folder "bin" with its content, giving it a specific name like "GamsConnector"
* Copy the file "GAMS.net4.dll" located at "[GAMS install directory]\[Latest version]" to the "GamsConnector" folder
* Copy the file "QvxLibrary.dll" located into Qlik SDK file downloaded earlier to the "GamsConnector" folder.
* Restart Qlik Sense server or Qlik Sense Desktop

## How to use

### Creating a connection
It should be possible to create a new GAMS connection in the Data Load Editor

![Creating a connection](/doc/Creating_a_connection.PNG)

The connection has to be be assigned to a path, which will give access to the GAMS files
1. Select the path
2. Choose a name for the connection
3. Click on "Test Connection"
4. Click on "Create"

### Using a connection
The simplest way to generate this script is using the Qlik wizard, which is opened by clicking the button "Select data" that appears in the GAMS connection.

![Select symbols](/doc/Select_symbols.PNG)

In this dialog the user has to select:
1. **Folder**: the first selector shows the sub-folders that exist inside the connector's folder.
2. **File**: the second selector shows all the GDX files once a folder has been selected.
3. **Symbols**: the left panel shows the symbols defined in the GDX file once a file has been selected.
4. **Fields**: the central panel shows the columns and a preview of the first rows once a symbol has been selected.

In order to generate the load script, at least one symbol and column has to be selected.

### Script syntax
The script has the following syntax, including the connection to the connector, the LOAD and SQL statements:

```
LIB CONNECT TO 'NAME_OF_THE_GAMS_CONNECTION';

LOAD "field_1", 
     "field_2";
SQL SELECT "field_1",
    "field_2"
FROM "symbol_name <path_to_file>";
```
The path to the file starts from the connector's folder, and can access sub-folders, e.g.

```
SQL SELECT "name"
FROM "countries <.\GAMS results\countries.gdx>";
```

For further knowledge about the script (like filtering), visit [Advance use](/doc/Advanced_use.md)

### Symbols and how they are loaded
The connector will load these types of symbols, representing them as tables:

#### Scalar
Columns |	Description	| Type
--------|-------------|-----
Value | Value of the scalar	| Text
@Comments | Explanatory text of the scalar | Text

#### Set
Columns |	Description	| Type
--------|-------------|------
One column for each used dimension / domain	| Each domain / dimension will appear as a column. The name will be the name of the domain. | Text
Text | Explanatory text of each of the elements. If there isn't one, then it will show "Y" | Text

#### Parameter
Columns |	Description	| Type
--------|-------------|-------
One column for each used dimension / domain | Each domain / dimension will appear as a column. The name will be the name of the domain. | Text
Value | Contains the value of the parameter for each row | If it can be represented as a number | Number
Value (SV) | Contains the value of the parameter for each row | If it cannot be represented as a number | Text

#### Equations and Variables
Columns |	Description	| Type
--------|-------------|------
One column for each used dimension / domain | Each domain / dimension will appear as a column. The name will be the name of the domain |	Text
Level | The "Level" attribute. If it can be represented as a number | Number
Level (SV) | The "Level" attribute. If it cannot be represented as number | Text
Marginal | The "Marginal" attribute. If it can be represented as a number | Number
Marginal (SV)	| The "Marginal" attribute. If it cannot be represented as number | Text
Lower | The "Lower" attribute. If it can be represented as a number | Number
Lower (SV)	| The "Lower" attribute. If it cannot be represented as number | Text
Upper | The "Upper" attribute. If it can be represented as a number | Number
Upper (SV)	| The "Upper" attribute. If it cannot be represented as number | Text
Scale | The "Scale" attribute. If it can be represented as a number | Number
Scale (SV) | The "Scale" attribute. If it cannot be represented as number | Text

### How to work with non-numeric values (infinite, epsilon, acronyms, ...)

Parameter's values and variable/equation's attributes will be separated in two columns when loaded in Qlik, in order to separate the numerical values from epsilon and other special values, as was explained in previous sections.

The values that are treated as special are:
GDX value	| Qlik value
----------|------------
Positive infinite |	+INF
Negative infinite	| -INF
Not available	| NA
Undefined	| UNDEF
Epsilon	| EPS
Acronym	| the value of the acronym

Whenever a special value appears in at least one row, both columns have to be selected in the SQL statement, in other case the load will fail.

After loading these fields in the SQL statement, they can be treated in the LOAD statement.

## Compilation

Download Visual Studio
1. Create a new C# console project.
2. Set “x64” as “Platform target” 
3. Add as reference “GAMS.net4.dll” located at “[GAMS install directory]\[Latest version]” e.g. “C:\Program Files\25.1”
4. Add as reference “QvxLibrary.dll” located into SDK file downloaded earlier.
5. Check that both references has been correctly added
6. Compile the project using Visual Studio
7. Follow the next step in order to name the connector, e.g. GAMS:
[Making your connector recognizable by Qlik Sense or QlikView](https://help.qlik.com/en-US/qlikview-developer/April2019/Subsystems/QVXSDKAPI/Content/QV_QVXSDKAPI/Making-connector-recognizable-by-QlikView.htm)

In order to install the compiled connector, the "web" folder has to be copied from the "src" alongside the generated exe files.

## License

Check the [LICENSE](LICENSE) file
