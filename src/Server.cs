using QlikView.Qvx.QvxLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Configuration;

namespace QvGamsConnector
{
    public class PreviewRow
    {
        public PreviewRow()
        {
            this.qValues = new List<string>();
        }
        public List<string> qValues { get; set; }
    }

    public class Owner
    {
        public Owner() : base()
        {

        }

        public string qName { get; set; }
    }

    public class OwnerResponse : QvDataContractResponse
    {
        public Owner[] qOwners { get; set; }
    }


    public class PreviewResponse : QvDataContractResponse
    {
        public PreviewResponse() : base()
        {
            this.qPreview = new List<PreviewRow>();
        }
        public List<PreviewRow> qPreview { get; set; }
    }

    public class PathItem
    {
        public string name { get; set; }

        public string path { get; set; }
    }

    public class ServerFolderResponse : QvDataContractResponse
    {
        public ServerFolderResponse() : base()
        {
            this.qFolders = new List<string>();
            this.qFiles = new List<string>();
            this.qCurrentPathList = new List<PathItem>();
        }

        public List<string> qFolders { get; set; }

        public List<string> qFiles { get; set; }

        public string qCurrentPath { get; set; }

        public List<PathItem> qCurrentPathList { get; set; }
    }

    public class QueryBuilderFieldObject
    {
        public string displayName { get; set; }

        public string identifierName { get; set; }

        public string aliasName { get; set; }
    }

    public class QueryBuilderObject
    {
        public string key { get; set; }

        public string databaseName { get; set; }

        public string ownerName { get; set; }

        public string tableName { get; set; }

        public bool includePrecedingLoad { get; set; }

        public List<QueryBuilderFieldObject> fields { get; set; }
    }

    public class QueryBuilderResponse : QvDataContractResponse
    {
        public string query { get; set; }
    }


    internal class Server : QvxServer
    {

        public override QvxConnection CreateConnection()
        {
            return new QvGamsConnection();
        }

        public override string CreateConnectionString()
        {
            return "";
        }
        
        /// <summary>
        /// Called by the HTML when actions are performed
        /// </summary>
        /// <param name="method">Name of the action specified in the call</param>
        /// <param name="userParameters">Parameters sent by the HTML</param>
        /// <param name="connection">Qlik connection</param>
        /// <returns></returns>
        public override string HandleJsonRequest(string method, string[] userParameters, QvxConnection connection)
        {
            #if DEBUG
            QvxLog.Log(QvxLogFacility.Application, QvxLogSeverity.Debug, "HandleJsonRequest()");
            #endif

            connection.MParameters.TryGetValue("provider", out string provider); // Set to the name of the connector by QlikView Engine
            connection.MParameters.TryGetValue("source", out string _source); // Path to the directory set when creating new connection

            QvDataContractResponse response;            
            switch (method)
            {
                case "getInfo":
                    response = getInfo();
                    break;
                case "getConnectionInfo":
                    response = getConnectionInfo(userParameters[0]);
                    break;
                case "testConnection":
                    response = testConnection(userParameters[0]);
                    break;
                case "getDatabases":
                    response = getDatabases(connection as QvGamsConnection, _source);
                    break;
                case "getOwners":
                    response = getOwners(connection as QvGamsConnection, _source, userParameters[0]);
                    break;
                case "getTables":
                    response = getTables(connection as QvGamsConnection, _source, userParameters[0], userParameters[1]);
                    break;
                case "getFields":
                    response = getFields(connection as QvGamsConnection, _source, userParameters[0], userParameters[1], userParameters[2]);
                    break;
                case "getPreview":
                    response = getPreview(connection as QvGamsConnection, _source, userParameters[0], userParameters[1], userParameters[2]);                       
                    break;
                case "getServerFolders":
                    response = GetServerFolders(userParameters[0], userParameters[1]);
                    break;
                case "getSelectScript":
                    QueryBuilderObject queryBuilder = ParseJson<QueryBuilderObject>(userParameters[0]);
                    response = getSelectScript(queryBuilder);
                    break;
                default:
                    response = new QlikView.Qvx.QvxLibrary.Info { qMessage = "Unknown command" };
                    break;
            }
            return ToJson(response);    // serializes response into JSON string
        }

        /// <summary>
        /// Obtains the data for the preview using the high level Gams API
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="_source"></param>
        /// <param name="database"></param>
        /// <param name="owner"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private PreviewResponse getPreview(QvGamsConnection connection, string _source, string database, string owner, string tableName)
        {
            string file = database + "\\" + owner;

            var result = new PreviewResponse();
            if (database != string.Empty && owner != string.Empty && tableName != string.Empty)
            {
                using(GAMSHelper gh = new GAMSHelper(_source, file))
                {
                    gh.LoadGAMSFile(connection);
                    string symbolName = tableName.Trim();
                    var currentTable = connection.FindTable(tableName, connection.MTables);

                    // Store the table Header 
                    var row = new PreviewRow();
                    foreach (var field in currentTable.Fields)
                    {
                        row.qValues.Add(field.FieldName);
                    }
                    result.qPreview.Add(row);
                    
                    // Getting the preview data
                    string[,] PreviewTableData = gh.GetPreviewData(currentTable, 9);

                    int FinalNumberOfRows = PreviewTableData.GetLength(0);
                    int NumberOfColumns = PreviewTableData.GetLength(1);

                    for (int i = 0; i < FinalNumberOfRows; i++)
                    {
                        row = new PreviewRow();
                        for (int j = 0; j < NumberOfColumns; j++)
                        {
                            row.qValues.Add(PreviewTableData[i, j]);
                        }
                        result.qPreview.Add(row);
                    }
                }
            }
            return result;
        }

        public bool verifyCredentials(string username, string password)
        {
            return true;                
        }

        private QvDataContractResponse getInfo()
        {
            return new Info { qMessage = "Gams connector for Windows"};
        }

        /// <summary>
        /// Obtains the databases (sub-folders)
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="_source"></param>
        /// <returns></returns>
        public QvDataContractResponse getDatabases(QvGamsConnection connection, string _source)
        {
            List<string> directories = GetDirectoriesAux(_source);
            Database[] auxDatabases = new Database[directories.Count()];
            int i = 0;
            foreach (String directory in directories)
            {
                auxDatabases[i++] = new Database { qName = directory };
            }

            return new QvDataContractDatabaseListResponse
            {
                qDatabases = auxDatabases
            };
        }

        /// <summary>
        /// Obtains all the sub-folders of the main folder. The property "folderAmountLimit" specifies the maximum number of folders that will be returned
        /// </summary>
        /// <param name="mainPath"></param>
        /// <returns></returns>
        private List<string> GetDirectoriesAux(string mainPath)
        {
            List<string> result = new List<string>();

            // we load the configuration properties
            string folderAmountLimitStr = ConfigurationManager.AppSettings["folderAmountLimit"];
            Int32.TryParse(folderAmountLimitStr, out int folderAmountLimit);

            // we use a Stack to obtain all the directories and subdirectories
            Stack<string> stack = new Stack<string>();
            stack.Push(mainPath);

            while (stack.Count > 0 && result.Count <= folderAmountLimit)
            {
                string path = stack.Pop();
                try
                {
                    // we add the directories in a reverse order, so they are obtained in the right one
                    string[] directories = Directory.GetDirectories(path);
                    for(int i = directories.Length - 1; i >= 0; i--)
                    {
                        stack.Push(directories[i]);
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }

                string cleanPath = PathUtils.RemoveRootPath(mainPath, path);
                if (cleanPath.Equals(""))
                {
                    result.Add(".");
                }
                else
                {
                    result.Add(cleanPath);
                }
            }

            result.Sort();
            return result;
        }

        /// <summary>
        /// Obtains the owners (files) with extension "gdx"
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="_source"></param>
        /// <param name="database"></param>
        /// <returns></returns>
        public QvDataContractResponse getOwners(QvGamsConnection connection, string _source, string database)
        {
            string finalPath = _source + "\\" + database;

            if(PathUtils.IsBaseOf(_source, finalPath))
            {
                List<string> files = GetFilesAux(finalPath);
                Owner[] auxOwners = new Owner[files.Count()];
                int i = 0;
                foreach (String file in files)
                {
                    Owner owner = new Owner() { qName = file };
                    auxOwners[i++] = owner;
                }

                return new OwnerResponse
                {
                    qOwners = auxOwners
                };
            } else
            {
                return new OwnerResponse
                {
                    qOwners = new Owner[0]
                };
            }
        }

        /// <summary>
        /// Obtains the files in a folder that have the extension "gdx"
        /// </summary>
        /// <param name="mainPath"></param>
        /// <returns></returns>
        private List<string> GetFilesAux(string mainPath)
        {
            List<string> result = new List<string>();

            try
            {
                string[] files = Directory.GetFiles(mainPath);

                if (files != null)
                {
                    foreach (string file in files)
                    {
                        if (file.EndsWith(".gdx"))
                        {
                            // we remove the mainPath from the file path
                            int place = file.IndexOf(mainPath);
                            string cleanFile = file.Remove(place, mainPath.Length);
                            cleanFile = cleanFile.StartsWith("\\") ? cleanFile.Substring(1) : cleanFile;

                            result.Add(cleanFile);
                        }
                    }
                    result.Sort();
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            return result;
        }

        /// <summary>
        /// Obtains the tables (gams symbols)
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="_source"></param>
        /// <param name="database"></param>
        /// <param name="owner"></param>
        /// <returns></returns>
        public QvDataContractResponse getTables(QvGamsConnection connection, string _source, string database, string owner)
        {
            string file = database + "\\" + owner;

            if (database != string.Empty && owner != string.Empty)
            {
                using(GAMSHelper gh = new GAMSHelper(_source, file))
                {
                    gh.LoadGAMSFile(connection);

                    return new QvDataContractTableListResponse
                    {
                        qTables = connection.MTables
                    };
                }
            } else
            {
                return new QvDataContractTableListResponse
                {
                    qTables = new List<QvxTable>()
                };
            }
        }

        /// <summary>
        /// Obtains the fields in a table
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="_source"></param>
        /// <param name="database"></param>
        /// <param name="owner"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public QvDataContractResponse getFields(QvGamsConnection connection, string _source, string database, string owner, string table)
        {
            string file = database + "\\" + owner;

            if (database != string.Empty && owner != string.Empty)
            {
                using(GAMSHelper gh = new GAMSHelper(_source, file))
                {
                    gh.LoadGAMSFile(connection);

                    var currentTable = connection.FindTable(table, connection.MTables);

                    return new QvDataContractFieldListResponse
                    {
                        qFields = (currentTable != null) ? currentTable.Fields : new QvxField[0]
                    };
                }
            } else
            {
                return new QvDataContractFieldListResponse
                {
                    qFields = new QvxField[0]
                };
            }
        }

        /// <summary>
        /// Tests the connection
        /// </summary>
        /// <param name="fileSourcePath"></param>
        /// <returns></returns>
        public QvDataContractResponse testConnection(string fileSourcePath)
        {            
            var message = "Directory not found!";
            if (Directory.Exists(fileSourcePath))
            {
                message = "Directory OK!";
            }
            return new Info { qMessage = message };
        }

        /// <summary>
        /// Calculates the sub-folders from a current path after selecting a new folder. It also obtains the files with extension "gdx"
        /// </summary>
        /// <param name="currentPath"></param>
        /// <param name="selectedFolder"></param>
        /// <returns></returns>
        public QvDataContractResponse GetServerFolders(string currentPath, string selectedFolder)
        {
            string newPath;
            if(currentPath != "" && PathUtils.IsDrive(currentPath) && selectedFolder == "..")
            {
                // if it's a drive and we select the parent, we go to the root
                newPath = "";
            } else
            {
                newPath = currentPath != string.Empty ? Path.GetFullPath(currentPath + "\\" + selectedFolder) : selectedFolder;
            }

            List<string> auxFolders = new List<string>();
            List<string> auxFiles = new List<string>();
            List<PathItem> auxCurrentPath = new List<PathItem>();

            if (newPath == string.Empty)
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives()) {
                    auxFolders.Add(drive.Name);
                }
                auxCurrentPath.Add(new PathItem { name = "PC", path = "" });
            } else
            {
                // we check if the folder has parent
                DirectoryInfo info = new DirectoryInfo(newPath);
                if (PathUtils.IsDrive(newPath) || info.Parent != null)
                {
                    auxFolders.Add("..");
                }
                if (info.Exists)
                {
                    // we obtain the subdirectories
                    foreach (string directory in Directory.GetDirectories(newPath))
                    {
                        if(!new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.Hidden))
                        {
                            string cleanPath = PathUtils.RemoveRootPath(newPath, directory);
                            auxFolders.Add(cleanPath);
                        }
                    }

                    // we obtain the files
                    foreach (string file in Directory.GetFiles(newPath))
                    {
                        if (file.EndsWith(".gdx")) {
                            string cleanPath = PathUtils.RemoveRootPath(newPath, file);
                            auxFiles.Add(cleanPath);
                        }
                    }
                }

                // we prepare the list of path items
                string auxPath = "";
                foreach (string node in newPath.Split('\\'))
                {
                    if (node != "")
                    {
                        auxPath = auxPath != "" ? auxPath + "\\" + node : node;
                        PathItem item = new PathItem { path = auxPath, name = node };
                        auxCurrentPath.Add(item);
                    }
                }
            }
            
            return new ServerFolderResponse {
                qFolders = auxFolders, qFiles = auxFiles, qCurrentPathList = auxCurrentPath, qCurrentPath = newPath
            };
        }

        /// <summary>
        /// Generates the SELECT script from the selections
        /// </summary>
        /// <param name="queryBuilder"></param>
        /// <returns></returns>
        private QueryBuilderResponse getSelectScript(QueryBuilderObject queryBuilder)
        {
            string NEXT_LINE = "\r\n";
            string TAB = "    ";

            StringBuilder query = new StringBuilder();

            StringBuilder load = new StringBuilder();
            if(queryBuilder.includePrecedingLoad)
            {
                foreach (QueryBuilderFieldObject field in queryBuilder.fields)
                {
                    if(load.Length != 0)
                    {
                        load.Append("," + NEXT_LINE + TAB);
                    }
                    load.Append(String.Format("\"{0}\"", field.displayName));
                    if (field.aliasName != "")
                    {
                        load.Append(String.Format(" AS \"{0}\"", field.aliasName));
                    }
                }
                query.Append(String.Format("LOAD {0};" + NEXT_LINE, load));
            }

            StringBuilder select = new StringBuilder();
            foreach (QueryBuilderFieldObject field in queryBuilder.fields)
            {
                if (select.Length != 0)
                {
                    select.Append("," + NEXT_LINE + TAB);
                }
                select.Append(String.Format("\"{0}\"", field.displayName));
            }

            StringBuilder from = new StringBuilder();
            from.Append(String.Format("\"{0} <{1}\\{2}>\"", queryBuilder.tableName, queryBuilder.databaseName, queryBuilder.ownerName));
            
            query.Append(String.Format("SQL SELECT {0}" + NEXT_LINE + "FROM {1};", select, from));

            return new QueryBuilderResponse { query = query.ToString() };
        }


        public QvDataContractResponse getConnectionInfo(string _connectionString)
        {
            var _source = GetSourceFromConnectionString(_connectionString);
            return new Info { qMessage = _source };
        }

        public string GetSourceFromConnectionString(string _connectionString)
        {
            var KeyValuePairs = _connectionString.Trim('"', ';')
                            .Remove(0, 19)
                            .Split(';')
                            .Select(s => s.Trim().Split('='))
                            .ToDictionary(a => a[0], a => a[1]);

            string result = string.Empty;
            KeyValuePairs.TryGetValue("source", out result);
            return result;
        }
    }
}
