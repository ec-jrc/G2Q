using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using GAMS;
using QlikView.Qvx.QvxLibrary;
using System.Globalization;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace QvGamsConnector
{

    class GAMSHelper : IDisposable
    {
        private const string Y_STRING = "Y";
        private const string TEXT = "Text";
        private const string VALUE = "Value";
        private const string LEVEL = "Level";
        private const string MARGINAL = "Marginal";
        private const string LOWER = "Lower";
        private const string UPPER = "Upper";
        private const string SCALE = "Scale";

        private const string SPECIAL_VALUE_SUFFIX = " (SV)";

        private readonly string GDXFileLocation;
        
        public GAMSDatabase Db { get; set; }

        public GAMSHelper(string source, string file)
        {
            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(file)) {
                this.GDXFileLocation = source + "\\" + file; ;
                if (PathUtils.IsBaseOf(source, this.GDXFileLocation))
                {
                    // check that the final file is inside the original folder
                    Db = new GAMSWorkspace().AddDatabaseFromGDX(GDXFileLocation);
                }
            }
            if(Db == null)
            {
                throw new QvxPleaseSendReplyException(QvxResult.QVX_CONNECT_ERROR, "The file cannot be accessed");
            }
        }

        /// <summary>
        /// This method obtains the list of tables and assigns it to connection.MTables
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="_source"></param>
        /// <param name="_file"></param>
        public void LoadGAMSFile(QvGamsConnection connection)
        {
            connection.MTables = new List<QvxTable>();
            foreach (GAMSSymbol sym in this.Db)
            {
                var GamsFields = new QvxField[this.GetColumnCount(sym)];
                int i = 0;
                foreach (var item in this.GetColumnHeaders(sym))
                {
                    GamsFields[i++] = new QvxField(item.Item1, item.Item2, QvxNullRepresentation.QVX_NULL_FLAG_SUPPRESS_DATA);
                }
                connection.MTables.Add(
                    new QvxTable
                    {
                        TableName = sym.Name,
                        GetRows = connection.GetData,
                        Fields = GamsFields
                    }
                    );
            };
        }
        
        /// <summary>
        ///  This method obtains the data from the GAMS file using the low level API
        /// </summary>
        /// <param name="_source"></param>
        /// <param name="file"></param>
        /// <param name="table"></param>
        /// <param name="where"></param>
        /// <returns></returns>
        public static IEnumerable<QvxDataRow> GetGamsDataLowLevel(string _source, string file, QvxTable table, List<string> selectedFields, List<QueryExtractor.WhereCondition> where)
        {
            const int GMS_VAL_LEVEL = 0;
            const int GMS_VAL_MARGINAL = 1;
            const int GMS_VAL_LOWER = 2;
            const int GMS_VAL_UPPER = 3;
            const int GMS_VAL_SCALE = 4;
            //const int GMS_VAL_MAX = 5;

            string msg = string.Empty, VarName = string.Empty, sText = string.Empty, UelName = string.Empty;
            int ErrNr = 0, SymNr = 0, SymTyp = 0, ADim = 0, ACount = 0, AUser = 0, NRec = 0, FDim = 0, j = 0, i = 0, IDum = 0;

            using (gdxcs gdx = new gdxcs(ref msg))
            {
                gdx.gdxOpenRead(_source + "\\" + file, ref ErrNr);
                gdx.gdxFindSymbol(table.TableName, ref SymNr);
                gdx.gdxSymbolInfo(SymNr, ref VarName, ref ADim, ref SymTyp);
                gdx.gdxSymbolInfoX(SymNr, ref ACount, ref AUser, ref sText);
                gdx.gdxDataReadRawStart(SymNr, ref NRec);

                Dictionary<int, string>[] dimensionCache = new Dictionary<int, string>[ADim];

                if (where.Count == 0)
                {
                    // No WHERE clause
                    double[] Vals = new double[gamsglobals.val_max];
                    int[] Keys = new int[gamsglobals.maxdim];
                    while (gdx.gdxDataReadRaw(ref Keys, ref Vals, ref FDim) != 0)
                    {
                        QvxDataRow row = mapRow(Keys, Vals);
                        if(row != null) yield return row;
                    }
                }
                else
                {
                    // WHERE clause
                    string[] strFilter = Enumerable.Repeat("", table.Fields.Count()).ToArray();
                    var emptyFilters = new List<QvxField>();
                    foreach (var whereCondition in where)
                    {
                        int m = Array.FindIndex(table.Fields, element => element.FieldName == whereCondition.Field);
                        if (m >= 0)
                        {
                            if(m > (ADim - 1))
                            {
                                // Only dimensions can be filtered
                                throw new QvxPleaseSendReplyException(QvxResult.QVX_UNSUPPORTED_COMMAND, String.Format("Field \"{0}\" is not a dimension and can't be filtered", whereCondition.Field));
                            } else if ("".Equals(whereCondition.Value))
                            {
                                // GAMS API doesn't allow empty filters, so we have to filter them ourselves
                                emptyFilters.Add(table.Fields[m]);
                            } else
                            {
                                strFilter[m] = whereCondition.Value;
                            }
                        }
                        else
                        {
                            throw new QvxPleaseSendReplyException(QvxResult.QVX_FIELD_NOT_FOUND, String.Format("The field \"{0}\" is not valid", whereCondition.Field));
                        }
                    }

                    using (BlockingCollection<QvxDataRow> buffer = new BlockingCollection<QvxDataRow>())
                    {
                        // Start reading thread
                        Exception exception = null;
                        var readTask = Task.Run(() =>
                        {
                            try
                            {
                                gdx.gdxDataReadRawFastFilt(SymNr, strFilter, FilterProc);
                            }
                            catch(Exception e)
                            {
                                exception = e;
                            }
                            finally
                            {
                                // Signal the end of the data
                                buffer.CompleteAdding();
                            }
                        });

                        int FilterProc(IntPtr IndxPtr, IntPtr ValsPtr, IntPtr Uptr)
                        {
                            double[] managedArrayVals = new double[gamsglobals.val_max];
                            Marshal.Copy(ValsPtr, managedArrayVals, 0, gamsglobals.val_max);

                            int[] managedArrayIndx = new int[gamsglobals.maxdim];
                            Marshal.Copy(IndxPtr, managedArrayIndx, 0, gamsglobals.maxdim);

                            QvxDataRow row = mapRow(managedArrayIndx, managedArrayVals, emptyFilters);
                            if(row != null) buffer.Add(row);
                            return 1;
                        }

                        // Writing process
                        foreach (QvxDataRow row in buffer.GetConsumingEnumerable())
                        {
                            yield return row;
                        }
                        if(exception != null)
                        {
                            throw exception;
                        }
                    }
                }

                QvxDataRow mapRow(int[] Keys, double[] Vals, List<QvxField> emptyFilters = null)
                {
                    i = 0;
                    var row = new QvxDataRow();

                    // Read the dimensions
                    for (j = 0; j < ADim; j++)
                    {
                        if(dimensionCache[j] == null) dimensionCache[j] = new Dictionary<int, string>();

                        UelName = null;
                        if (dimensionCache[j].ContainsKey(Keys[j])) {
                            UelName = dimensionCache[j][Keys[j]];
                        } else
                        {
                            gdx.gdxUMUelGet(Keys[j], ref UelName, ref IDum);
                            dimensionCache[j][Keys[j]] = UelName;
                        }

                        if(UelName != null)
                        {
                            QvxField field = table.Fields[i++];
                            if (selectedFields.Contains(field.FieldName)) row[field] = UelName;

                            // we check the empty filters, as GAMS API doesn't do it
                            if (emptyFilters != null && emptyFilters.Contains(field) && !string.IsNullOrEmpty(UelName))
                            {
                                return null;
                            }
                        }
                    }
                    
                    switch (SymTyp)
                    {
                        // SET
                        case gamsglobals.dt_set:
                            if (gdx.gdxGetElemText((int)Vals[GMS_VAL_LEVEL], ref msg, ref IDum) != 0)
                            {
                                QvxField field2 = table.Fields[i++];
                                if (selectedFields.Contains(field2.FieldName)) row[field2] = msg;
                            }
                            else
                            {
                                QvxField field2 = table.Fields[i++];
                                if (selectedFields.Contains(field2.FieldName)) row[field2] = Y_STRING;
                            }
                            break;

                        // PARAMETER
                        case gamsglobals.dt_par:
                            // Value
                            readValueField(row, table.Fields[i++], table.Fields[i++], Vals[GMS_VAL_LEVEL]);

                            if (!string.IsNullOrEmpty(sText) && ADim == 0)
                            {
                                QvxField field = table.Fields[i++];
                                if (selectedFields.Contains(field.FieldName)) row[field] = sText;
                            }
                            break;

                        // EQUATION and VARIABLE
                        case gamsglobals.dt_equ:
                        case gamsglobals.dt_var:
                            int[] gms_values = { GMS_VAL_LEVEL, GMS_VAL_MARGINAL, GMS_VAL_LOWER, GMS_VAL_UPPER, GMS_VAL_SCALE };
                            foreach (int gms_value in gms_values)
                            {
                                // Value
                                readValueField(row, table.Fields[i++], table.Fields[i++], Vals[gms_value]);
                            }
                            break;
                    }
                    return row;
                }

                /// <summary>This method reads a value separated in two fields, the first with the numeric value and the second with the special value description.</summary>
                void readValueField(QvxDataRow pRow, QvxField numberField, QvxField specialField, double pVal)
                {
                    Boolean isSpecialValue = false;
                    // Value
                    if (selectedFields.Contains(numberField.FieldName))
                    {
                        pRow[numberField] = val2str(pVal, msg, out isSpecialValue, true, false);
                    }
                    // Value (Special)
                    if (selectedFields.Contains(specialField.FieldName))
                    {
                        pRow[specialField] = val2str(pVal, msg, out isSpecialValue, false, true);
                    } else if(isSpecialValue)
                    {
                        // If the value is special, but the "Special value" column is not selected, we throw an error
                        throw new QvxPleaseSendReplyException(QvxResult.QVX_FIELD_NOT_FOUND, String.Format("The field \"{0}\" contains special values, so the field \"{1}\" has to be selected", numberField.FieldName, specialField.FieldName));
                    }
                }

                /// <summary>This method generates the final value of a field, that can be a normal number, an acronym, infinite, ...</summary>
                /// <param name="returnNumber">If true, the value of a number will be returned as a double</param>
                /// <param name="returnSpecial">If true, the value of a special value (infinite, epsilon, acronym, ...) will be returned as a string</param>
                dynamic val2str(double val, string s, out Boolean isSpecial, Boolean returnNumber = true, Boolean returnSpecial = true)
                {
                    string[] gmsSVText = { "UNdef", "NA", "+Inf", "-Inf", "Eps", "0", "AcroN" };
                    int sv = 0;
                    if (gdx.gdxAcronymName(val, ref s) != 0)
                    {
                        isSpecial = true;
                        return (returnSpecial ? s : null);
                    }
                    else
                    {
                        gdx.gdxMapValue(val, ref sv);
                        if (gamsglobals.sv_normal != sv)
                        {
                            isSpecial = true;
                            return (returnSpecial ? gmsSVText[sv] : null);
                        }
                        else
                        {
                            isSpecial = false;
                            if (returnNumber) return val;
                            else return null;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Obtains the amount of columns
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public int GetColumnCount(GAMSSymbol symbol)
        {
            int result = 0;
            switch (symbol)
            {
                case GAMSSet gset:
                    // SET
                    result = gset.Dim + 1;
                    break;
                case GAMSParameter gparam:
                    // PARAMETER
                    result = gparam.Dim + 1*2;
                    if (!string.IsNullOrEmpty(gparam.Text) && gparam.Dim == 0) result++;
                    break;
                case GAMSEquation geq:
                case GAMSVariable gvar:
                    // EQUATION AND VARIABLE
                    result = symbol.Dim + 5*2;
                    break;
            }
            return result;
        }

        /// <summary>
        /// Obtains the column's headers
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public List<Tuple<string, QvxFieldType>> GetColumnHeaders(GAMSSymbol symbol)
        {
            List<Tuple<string, QvxFieldType>> returnList = new List<Tuple<string, QvxFieldType>>();
            // Dimensions
            GenerateDimensionColumnNames(symbol.DomainsAsStrings).ForEach(item => returnList.Add(new Tuple<string, QvxFieldType>(item, QvxFieldType.QVX_TEXT)));

            switch (symbol)
            {
                case GAMSSet gset:
                    // SET
                    returnList.Add(new Tuple<string, QvxFieldType>(TEXT, QvxFieldType.QVX_TEXT));
                    break;
                case GAMSParameter gparam:
                    // PARAMETER
                    returnList.Add(new Tuple<string, QvxFieldType>(VALUE, QvxFieldType.QVX_IEEE_REAL));
                    returnList.Add(new Tuple<string, QvxFieldType>(VALUE + SPECIAL_VALUE_SUFFIX, QvxFieldType.QVX_TEXT));
                    if (!string.IsNullOrEmpty(gparam.Text) && gparam.Dim == 0) returnList.Add(new Tuple<string, QvxFieldType>("@Comments", QvxFieldType.QVX_TEXT));
                    break;
                case GAMSEquation geq:
                case GAMSVariable gvar:
                    // EQUATION AND VARIABLE
                    string[] gms_labels = { LEVEL, MARGINAL, LOWER, UPPER, SCALE };
                    foreach (string label in gms_labels)
                    {
                        returnList.Add(new Tuple<string, QvxFieldType>(label, QvxFieldType.QVX_IEEE_REAL));
                        returnList.Add(new Tuple<string, QvxFieldType>(label + SPECIAL_VALUE_SUFFIX, QvxFieldType.QVX_TEXT));
                    }
                    break;
                default:
                    returnList = new List<Tuple<string, QvxFieldType>>();
                    break;
            }
            return returnList;
        }

        /// <summary>
        /// Obtains the default names for dimension columns
        /// </summary>
        /// <param name="domains"></param>
        /// <returns></returns>
        private List<string> GenerateDimensionColumnNames(List<string> domains)
        {
            List<string> returnList = new List<string>();
            int count;
            foreach (var str in domains)
            {
                count = 0;
                string auxStr = str;
                while ((auxStr == "*") || returnList.Contains(auxStr))
                {
                    auxStr = "@" + string.Format("{0}{1}", str, count++);
                }
                returnList.Add(auxStr);
            }            
            return returnList;
        }

        public string NumericValueToStr(double number, Boolean returnNumber = true, Boolean returnSpecialValues = true)
        {
            string result;
            switch (number)
            {
                case double.Epsilon: result = "EPS"; break;
                case double.NaN: result = "NA"; break;
                case double.PositiveInfinity: result = "+INF"; break;
                case double.NegativeInfinity: result = "-INF"; break;
                case 1E+300: result = "UNDEF"; break;
                //case 10.0E300: return "Acronym"; //Acronyms are not supported for C# API
                default: return (returnNumber ? number.ToString() : "");
            }
            return (returnSpecialValues ? result : "");
        }

        /// <summary>
        /// Obtains the preview data using the high level API
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="NumberOfRows"></param>
        /// <returns></returns>
        public string[,] GetPreviewData(QvxTable table, int NumberOfRows = int.MaxValue)
        {
            string[,] returnTable;
            GAMSSymbol symbol = Db.GetSymbol(table.TableName);
            switch (symbol)
            {
                case GAMSSet gset:
                    GetPreviewColumnsData(out returnTable, gset, NumberOfRows);
                    break;
                case GAMSParameter gparam:
                    GetPreviewColumnsData(out returnTable, gparam, NumberOfRows);
                    break;
                case GAMSEquation geq:
                    GetPreviewColumnsData(out returnTable, geq, NumberOfRows);
                    break;
                case GAMSVariable gvar:
                    GetPreviewColumnsData(out returnTable, gvar, NumberOfRows);
                    break;
                default:
                    returnTable = new string[0,0];
                    break;
            }

            int FinalNumberOfRows = returnTable.GetLength(0);
            int NumberOfColumns = returnTable.GetLength(1);

            //Using advanced api for retrieve acronyms if existing
            string msg = "";
            int ErrNr = 0, SymNr = 0;

            using (gdxcs gdx = new gdxcs(ref msg))
            {
                gdx.gdxOpenRead(GDXFileLocation, ref ErrNr);
                gdx.gdxFindSymbol(table.TableName, ref SymNr);

                int dimensions = symbol.Dim;
                for (int i = 0; i < FinalNumberOfRows; i++)
                {
                    for (int j = dimensions; j < NumberOfColumns; j++)
                    {
                        if (double.TryParse(returnTable[i, j], out double doubleForParse))
                        {
                            string value = val2str(doubleForParse, msg, out Boolean isSpecial);
                            if (isSpecial)
                            {
                                // Values use two columns, and special values appear in the second one. So we increase the counter
                                returnTable[i, j++] = "";
                            }
                            returnTable[i, j] = value;
                        }
                    }
                }

                string val2str(double val, string s, out Boolean isSpecial)
                {
                    string[] gmsSVText = { "UNdef", "NA", "+Inf", "-Inf", "Eps", "0", "AcroN" };
                    isSpecial = false;
                    int sv = 0;
                    if (gdx.gdxAcronymName(val, ref s) != 0)
                    {
                        isSpecial = true;
                        return s;
                    }
                    else
                    {
                        gdx.gdxMapValue(val, ref sv);
                        if (gamsglobals.sv_normal != sv)
                        {
                            isSpecial = true;
                            return gmsSVText[sv];
                        }
                        else return val.ToString("N", new CultureInfo("en-US", false).NumberFormat);
                    }
                }
            }

            return returnTable;
        }

        private void GetPreviewColumnsData(out string[,] returnTable, GAMSSet gset, int NumberOfRows = int.MaxValue)
        {
            returnTable = new string[Math.Min(gset.NumberRecords, NumberOfRows), gset.Dim + 1];
            int j;
            int i = 0;
            foreach (GAMSSetRecord setRecord in gset)
            {
                j = 0;
                foreach (var key in setRecord.Keys)
                {
                    returnTable[i, j++] = key;
                }

                if (setRecord.Text.Length == 0) returnTable[i, j++] = Y_STRING;
                else returnTable[i, j++] = setRecord.Text;

                i++;
                if (i >= NumberOfRows) break;
            }
        }

        private void GetPreviewColumnsData(out string[,] returnTable, GAMSParameter parameter, int NumberOfRows = int.MaxValue)
        {
            int CommentAsNewColumn = 0;
            if (!string.IsNullOrEmpty(parameter.Text) && parameter.Dim == 0) CommentAsNewColumn++;

            returnTable = new string[Math.Min(parameter.NumberRecords, NumberOfRows), parameter.Dim + 2 + CommentAsNewColumn];
            int j;
            int i = 0;
            foreach (GAMSParameterRecord parameterRecord in parameter)
            {
                j = 0;                                                
                foreach (var key in parameterRecord.Keys)
                {
                    returnTable[i, j++] = key;
                }
                returnTable[i, j++] = NumericValueToStr(parameterRecord.Value, true, false); // Value
                returnTable[i, j++] = NumericValueToStr(parameterRecord.Value, false, true); // Special value

                if (!string.IsNullOrEmpty(parameter.Text) && parameter.Dim == 0) returnTable[i, j++] = parameter.Text;
                i++;
                if (i >= NumberOfRows) break;
            }
        }

        private void GetPreviewColumnsData(out string[,] returnTable, GAMSEquation equation, int NumberOfRows = int.MaxValue)
        {
            returnTable = new string[Math.Min(equation.NumberRecords, NumberOfRows), equation.Dim + 5*2];
            int j;
            int i = 0;
            foreach (GAMSEquationRecord record in equation)
            {
                j = 0;
                foreach (var key in record.Keys)
                {
                    returnTable[i, j++] = key;
                }

                double[] gms_values = { record.Level, record.Marginal, record.Lower, record.Upper, record.Scale };
                foreach (double value in gms_values)
                {
                    returnTable[i, j++] = NumericValueToStr(value, true, false); // Value
                    returnTable[i, j++] = NumericValueToStr(value, false, true); // Special value
                }

                i++;
                if (i >= NumberOfRows) break;                
            }
        }

        private void GetPreviewColumnsData(out string[,] returnTable, GAMSVariable variable, int NumberOfRows = int.MaxValue)
        {
            returnTable = new string[Math.Min(variable.NumberRecords, NumberOfRows), variable.Dim + 5*2];
            int j;
            int i = 0;
            foreach (GAMSVariableRecord record in variable)
            {
                j = 0;
                foreach (var key in record.Keys)
                {
                    returnTable[i, j++] = key;
                }

                double[] gms_values = { record.Level, record.Marginal, record.Lower, record.Upper, record.Scale };
                foreach (double value in gms_values)
                {
                    returnTable[i, j++] = NumericValueToStr(value, true, false); // Value
                    returnTable[i, j++] = NumericValueToStr(value, false, true); // Special value
                }

                i++;
                if (i >= NumberOfRows) break;                
            }
        }

        public void Dispose()
        {
            if (Db != null) Db.Dispose();
        }
    }
}
