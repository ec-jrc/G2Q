define( ['qvangular',
"underscore"
], function ( qvangular, underscore ) {
	return ['serverside', 'standardSelectDialogService', function ( serverside, standardSelectDialogService ) {
		var eventlogDialogContentProvider = {
			getConnectionInfo: function () {
				var scopeOptions = $("#selectAllFields").scope().options;
				scopeOptions.databaseTitle = 'Folder';
				scopeOptions.databasePlaceholder = 'Select a folder...';
				scopeOptions.ownerTitle = 'File';
				scopeOptions.ownerPlaceholder = 'Select a file...';
				scopeOptions.tablesTitle = 'Symbols';
				
				return qvangular.promise( {
					dbusage: false,
					ownerusage: false,
					dbseparator: '.',
					ownerseparator: '.',
					specialchars: '! "$&\'()*+,-/:;<>`{}~[]',
					quotesuffix: '"',
					quoteprefix: '"',
					dbfirst: true,
					keywords: []
				} );
			},
			getDatabases: function () {				
				return serverside.sendJsonRequest( "getDatabases" ).then( function ( response ) {
					return response.qDatabases;
				} );
			},
			getOwners: function (qDatabaseName) {
				return serverside.sendJsonRequest( "getOwners", qDatabaseName ).then( function ( response ) {
					return response.qOwners;
				} );
			},
			getTables: function ( qDatabaseName, qOwnerName ) {
				return serverside.sendJsonRequest( "getTables", qDatabaseName, qOwnerName ).then( function ( response ) {
					return response.qTables;
				} );
			},
			getFields: function ( qDatabaseName, qOwnerName, qTableName ) {
				return serverside.sendJsonRequest( "getFields", qDatabaseName, qOwnerName, qTableName ).then( function ( response ) {
					return response.qFields;
				} );
			},
			getPreview: function( qDatabaseName, qOwnerName, qTableName ) {
				return serverside.sendJsonRequest("getPreview", qDatabaseName, qOwnerName, qTableName).then(function (response) {				
				   return qvangular.promise(response.qPreview);				
				} );
				
			}
		};
		
		function filterSelected(list) {
			return underscore.filter(list, function (x) {
				return x.checked;
			});
		}

		function atLeastOneFieldHasAlias(selectedFields) {
			for (var field in selectedFields) {
				if (selectedFields[field].alias && selectedFields[field].alias !== selectedFields[field].name) {
					return true;
				}
			}
			return false;
		}

		function fieldSelectionChanged(newFields, existingFields) {
			if (newFields.fields.length != existingFields.fields.length) {
				return true;
			}

			// if any field is not found in one list, the selections are different
			for (var listIndex in newFields.fields) {
				if (newFields.fields[listIndex].identifierName != existingFields.fields[listIndex].identifierName || newFields.fields[listIndex].aliasName != existingFields.fields[listIndex].aliasName) {
					return true;
				}
			}

			return false;
		}

		function tableLoadPropertiesChanged(newLoadProperties, oldLoadProperties) {
		    if(!newLoadProperties && !oldLoadProperties) {
		        return false;
		    }
		    if(!newLoadProperties || !oldLoadProperties) {
		        return true;    // only one can be null so they are different
		    }

            // filter checks
		    if(!newLoadProperties.filterInfo && !oldLoadProperties.filterInfo) {
		        return false;
		    }

		    if(!newLoadProperties.filterInfo || !oldLoadProperties.filterInfo) {
		        return true;
		    }

		    if(newLoadProperties.filterInfo.filterType !== oldLoadProperties.filterInfo.filterType) {
		        return true;
		    }
		    
		    if(newLoadProperties.filterInfo.filterClause !== oldLoadProperties.filterInfo.selectGeneratedFilterClause) {
		        return true;
		    }

		    return false;
		}
		
		function isAddScriptButtonDisabled() {
			return getCurrentScript().trim() == "";
		}
		
		function createQueryBuilderInfo(selectedFields, precedingLoad, tableLoadProperties) {
			var queryBuilderObject = {};

			var table = selectedFields[0].table;
			queryBuilderObject.tableName = table.name;
			queryBuilderObject.ownerName = table.ownerName;
			queryBuilderObject.databaseName = table.databaseName;

			//queryBuilderObject.tableQualifiers = [];
			//queryBuilderObject.tableQualifiers.push(table.databaseName);
			//queryBuilderObject.tableQualifiers.push(table.ownerName);

			queryBuilderObject.key = ((table.databaseName || "") != "" ? table.databaseName + "." : "") + 
										((table.ownerName || "") != "" ? table.ownerName + "." : "") + 
										table.name;

			queryBuilderObject.fields = [];

			selectedFields.forEach(function (field) {
				var aliasName = field.name !== field.alias ? field.alias : "";

				queryBuilderObject.fields.push( { displayName: field.name, identifierName: field.identifierName, aliasName: aliasName });
			});

			queryBuilderObject.includePrecedingLoad = precedingLoad;
			queryBuilderObject.loadProperties = tableLoadProperties;

			return queryBuilderObject;
		}
		
		function getCurrentScript() {
			var script = "";

			for (var index in currentQueryBuilderInfoList) {
				var queryInfo = currentQueryBuilderInfoList[index];

				//if there was error during select script load process or not all data are loaded return empty script
				if (queryInfo.hasOwnProperty("success") && queryInfo.success != true ||  queryInfo.isLoading === true)
					return "";

				script += queryInfo.script;
				script += "\r\n\r\n";
			}

			return script.trim();
		}
		
		var fieldsListCache = [];
		var includeLoadStatementState = null;

		function generateSelectScript(datasourceSelectionModel, datasourceInfo, precedingLoad, selectAll) {
			var databases, selectedFields;
			var updatedQueryBuilderInfoList = [];

			if (datasourceInfo) {
				//script += datasourceInfo.connectString + "\r\n";
				databases = datasourceSelectionModel.databases;
				if (databases) {

					var allTables = $.map(databases, function (database) {
						var dbOwners = database.owners;
						if (dbOwners && dbOwners.length > 0) {
							return $.map(dbOwners,function(owner) {
								if (owner.tables && owner.tables.length > 0) {
									return owner.tables;
								}
							});
						}
					});

					//filter tables that contain any selected fields
					var tablesFields=$.map(allTables,function(table) {
						selectedFields = filterSelected(table.fields);
						if (selectedFields && selectedFields.length > 0) {
							return { 
								key:table.ownerName + "_" + table.databaseName+ "_" +	table.name,
								fields: selectedFields,
								loadProperties: table.loadProperties
						};
						}
					});

					//check is current tables list allready loaded to prevent "currentQueryBuilderInfoList" clearence
					var tablesAreSame=underscore.isEqual(
						underscore.sortBy(fieldsListCache, function(table) {return table.key;}),
						underscore.sortBy(tablesFields, function(table) { return table.key;})
					);
					if (tablesAreSame && includeLoadStatementState === precedingLoad)
						return getCurrentScript();

					tablesFields.forEach(function (table) {
						var fields = table.fields;
						var queryBuilderInfo = createQueryBuilderInfo(fields, precedingLoad || atLeastOneFieldHasAlias(fields), table.loadProperties ); //, selectAll

						// check if this table is already in the list
						var currentQueryBuilderInfo = underscore.find(currentQueryBuilderInfoList, function(info) { return info.key == queryBuilderInfo.key; });

						if (currentQueryBuilderInfo === undefined || queryBuilderInfo.tableName != currentQueryBuilderInfo.tableName || queryBuilderInfo.includePrecedingLoad != currentQueryBuilderInfo.includePrecedingLoad || fieldSelectionChanged(queryBuilderInfo, currentQueryBuilderInfo) || tableLoadPropertiesChanged(queryBuilderInfo.loadProperties, currentQueryBuilderInfo.loadProperties)) {
							currentQueryBuilderInfo = queryBuilderInfo;
							currentQueryBuilderInfo.script = ""; // Clear out the script since it will be retrieved from the connector
							currentQueryBuilderInfo.isLoading = true; //setting "table is loading" flag
							//currentQueryBuilderInfo.loadProperties.filterInfo.selectGeneratedFilterClause = currentQueryBuilderInfo.loadProperties.filterInfo.filterClause; // copy the state of filter when select is generated because it can change often as the user types
							var queryBuilderModelJson = queryBuilderInfo;

							serverside.sendJsonRequest("getSelectScript", queryBuilderModelJson).then(function(responseScript) {
								currentQueryBuilderInfo.success = true;
								currentQueryBuilderInfo.script = responseScript.query;	
								currentQueryBuilderInfo.isLoading = false;
							});
							
							//dataSelectionModel.getSelectScript(queryBuilderModelJson, []).then(function(responseScript) {

							//	currentQueryBuilderInfo.success = responseScript.success;

								//serverside.sendJsonRequest("getSelectScript", queryBuilderModelJson).then(function(responseScript) {
								//for (var index in responseScript.generatedScriptList) {
								//	var generatedScriptInfo = responseScript.generatedScriptList[index];
                                //
								//	currentQueryBuilderInfo.script = generatedScriptInfo.script;
								//	currentQueryBuilderInfo.isLoading = false; //table is loaded
								//}
								
							//});
						} else {
							//script = currentQueryText;

						}

						// Add the new or existing item to the list
						updatedQueryBuilderInfoList.push(currentQueryBuilderInfo);
					});
					//update loaded tables list. fieldsListCache can be differ from "updatedQueryBuilderInfoList" if several "generateSelectScript" executed simultaneously
					fieldsListCache = tablesFields;
					includeLoadStatementState === precedingLoad;
				}

				// Update the list
				currentQueryBuilderInfoList = updatedQueryBuilderInfoList;

				// Generate the script for all selections
				var script = getCurrentScript();
				
				return script;
			} else {
				return "";
			}			
		}

		standardSelectDialogService.showStandardDialog( eventlogDialogContentProvider, {
			precedingLoadVisible: true,
			fieldsAreSelectable: true,
			allowFieldRename: true,
			scriptGenerator: { generateScriptForSelections: generateSelectScript }
		});
	}];
} );