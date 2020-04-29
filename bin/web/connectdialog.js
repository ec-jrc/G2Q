define( ['qvangular',
		'text!QvGamsConnector.webroot/connectdialog.ng.html',
		'css!QvGamsConnector.webroot/connectdialog.css'
], function ( qvangular, template) {
	return {
		template: template,
		controller: ['$scope', 'input', function ( $scope, input ) {
			function init() {
				$scope.isLoading = true;
			
				$scope.isEdit = input.editMode;
				$scope.id = input.instanceId;
				$scope.titleText = $scope.isEdit ? "Modify connection (GAMS)" : "Create new connection (GAMS)";
				$scope.saveButtonText = $scope.isEdit ? "Update" : "Create";

				$scope.name = "";
				$scope.username = "Username";
				$scope.password = "Password";
				$scope.provider = "QvGamsConnector.exe"; // Connector filename
				$scope.filesourcepath = "";
				$scope.connectionInfo = "";
				$scope.connectionSuccessful = false;
				$scope.connectionString = createCustomConnectionString($scope.provider, "");
				
				$scope.folders = [];
				$scope.files = [];
				$scope.pathItems = [];

				input.serverside.sendJsonRequest( "getInfo" ).then( function ( info ) {
					$scope.info = info.qMessage;
				} );

				if ( $scope.isEdit ) {
					input.serverside.getConnection( $scope.id ).then( function ( result ) {
						$scope.name = result.qConnection.qName;	
						$scope.username = result.qConnection.username;
						$scope.password = result.qConnection.password;
						$scope.connectionString = result.qConnection.qConnectionString;
						
						input.serverside.sendJsonRequest("getConnectionInfo", result.qConnection.qConnectionString, 'true' /*include credentials*/).then(function (connectionInfoResponse) {
							$scope.filesourcepath = connectionInfoResponse.qMessage;
							getServerFolders("");
						});
						$scope.connectionString = createCustomConnectionString($scope.provider, result.qConnection.qfilesourcepath);
						
						$scope.password = result.qConnection.qMessage;
					} );
				} else {
					getServerFolders("");
				}
			}



			$scope.onTestConnectionClicked = function () {
				$scope.isLoading = true;
				input.serverside.sendJsonRequest( "testConnection", $scope.filesourcepath ).then( function ( info ) {
					$scope.connectionInfo = info.qMessage;
					$scope.connectionSuccessful = info.qMessage.indexOf( "OK" ) !== -1;
					$scope.connectionString = createCustomConnectionString($scope.provider, $scope.filesourcepath);
					$scope.isLoading = false;
				},
				function ( error ) {
					console.error(error);
					$scope.isLoading = false;
				});
			};

			$scope.isOkEnabled = function () {
				return $scope.name.length > 0 && $scope.connectionSuccessful;
			};

			$scope.onEscape = $scope.onCancelClicked = function () {
				$scope.destroyComponent();
			};

			$scope.onOKClicked = function () {
				console.info("Estamos en la segunda OkCliked");
				if($scope.isEdit) {
					console.info("Estamos en la segunda OkCliked y isEdit is enabled");
					console.info($scope);
						input.serverside.modifyConnection($scope.id, $scope.name, $scope.connectionString, $scope.provider, true, $scope.username, $scope.password).then(function(result){							
						if(result){
							$scope.destroyComponent();
						}
					},
					function ( error ) {
						console.error(error);
					});
				}
				else {
					input.serverside.createNewConnection($scope.name, $scope.connectionString, $scope.username, $scope.password, $scope.filesourcepath).then(function(result){
						if(result){
							$scope.destroyComponent();
						}
					},
					function ( error ) {
						console.error(error);
					});
					
				}
			};
			
			$scope.getServerFolders = function(folder) {
				getServerFolders(folder);
			};
			
			$scope.goToPath = function(path) {
				$scope.filesourcepath = path;
				getServerFolders("");
			};

			
			/* Helper functions */


			function createCustomConnectionString ( filename, connectionstring ) {
				//console.info("createCustomString:"+connectionstring)
				if (connectionstring != "")
				{
				return "CUSTOM CONNECT TO " + "\"provider=" + filename + ";source=" +  connectionstring + ";\"";
				
				} else {
					return "CUSTOM CONNECT TO " + "\"provider=" + filename + ";\"";			
				}				
			}

			function createConnectionString ( filename, connectionstring ) {
				
				return "CUSTOM CONNECT TO " + "\"provider=" + filename + ";\"";
			}
			
			function getServerFolders (selectedFolder) {
				$scope.isLoading = true;
				input.serverside.sendJsonRequest( "getServerFolders", $scope.filesourcepath, selectedFolder ).then( function ( serverFolderInfo ) {
					$scope.filesourcepath = serverFolderInfo.qCurrentPath;
					$scope.folders = serverFolderInfo.qFolders;
					$scope.files = serverFolderInfo.qFiles;
					$scope.pathItems = serverFolderInfo.qCurrentPathList;
					
					$scope.isLoading = false;
					$(".browserDiv")[0].scrollTo(0,0);
				},
				function ( error ) {
					console.error(error);
					$scope.isLoading = false;
				});
			}

			init();
		}]
	};
} );