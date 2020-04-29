using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using QlikView.Qvx.QvxLibrary;

namespace QvGamsConnector
{

    internal class QvGamsConnection : QvxConnection
    {
        private string _source = "";

        private List<string> selectedFields;
        private QvxTable qTable;
        private string file;
        private List<QueryExtractor.WhereCondition> where = new List<QueryExtractor.WhereCondition>();

        public override void Init()
        {
            #if DEBUG
            Debugger.Launch();
            QvxLog.Log(QvxLogFacility.Application, QvxLogSeverity.Notice, "Init()");
            #endif
            
            if (this.MParameters != null)
            {
                this.MParameters.TryGetValue("source", out _source); //Set when loading data
            }
        }

        /// <summary>
        /// Parses the query and fills the following properties: fields, liveTable, file, where
        /// </summary>
        /// <param name="query"></param>
        /// <param name="qvxTables"></param>
        /// <returns>the table in use</returns>
        public override QvxDataTable ExtractQuery(string query, List<QvxTable> qvxTables)
        {
            #if DEBUG
            QvxLog.Log(QvxLogFacility.Application, QvxLogSeverity.Notice, "ExtractQuery()");
            #endif

            QueryExtractor.ExtractQueryDetails(query, out selectedFields, out string liveTable, out file, out where);

            using (GAMSHelper gh = new GAMSHelper(_source, file))
            {
                gh.LoadGAMSFile(this);
                qTable = FindTable(liveTable, MTables);
                if (qTable == null) throw new QvxPleaseSendReplyException(QvxResult.QVX_TABLE_NOT_FOUND, String.Format("The symbol \"{0}\" is not valid", liveTable));

                // create a list with all fields that appear in SELECT and WHERE
                HashSet<string> referencedFields = new HashSet<string>(selectedFields);
                referencedFields.UnionWith(where.ConvertAll(item => item.Field));

                // before returning the table to Qlik, we check if the fields were selected by position or name, in order to use the name selected by the user
                Regex rx = new Regex("^@([0-9]+)$");
                foreach(string referencedField in referencedFields)
                {
                    Match match = rx.Match(referencedField);
                    if(match.Success)
                    {
                        // the column is selected by position
                        Group group = match.Groups[1];
                        int columnPosition = int.Parse(group.Value);
                        if(columnPosition >= qTable.Fields.Length)
                        {
                            throw new QvxPleaseSendReplyException(QvxResult.QVX_FIELD_NOT_FOUND, String.Format("The field position \"{0}\" is not valid", referencedField));
                        } else if(referencedFields.Contains(qTable.Fields[columnPosition].FieldName))
                        {
                            throw new QvxPleaseSendReplyException(QvxResult.QVX_FIELD_NOT_FOUND, String.Format("The same field cannot be selected by position and name: \"{0}\", \"{1}\"",
                                referencedField, qTable.Fields[columnPosition].FieldName));
                        } else {
                            // we update the QvxTable, so internally the field is always called by the name selected by the user
                            qTable.Fields[columnPosition].FieldName = referencedField;
                        }
                    } else
                    {
                        // the column is selected by name, so we only check if the name is right
                        if (!Array.Exists(qTable.Fields, tableField => tableField.FieldName.Equals(referencedField)))
                        {
                            throw new QvxPleaseSendReplyException(QvxResult.QVX_FIELD_NOT_FOUND, String.Format("The field \"{0}\" is not valid", referencedField));
                        }
                    }
                }
                return new QvxDataTable(qTable);
            }
        }

        /// <summary>
        /// Method responsible for the obtention of data from the file. It will be called automatically by Qlik, as it is referenced in the MTables property
        /// </summary>
        /// <returns></returns>
        public IEnumerable<QvxDataRow> GetData()
        {
            return GAMSHelper.GetGamsDataLowLevel(_source, file, qTable, selectedFields, where);
        }

    }
}
