using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Data;
using Serilog;
using Serilog.Core;



namespace SubmissionParser3
{

    class Program
    {

        // These are the things that we want to be in scope everywhere
        //
        static Settings? settings;
        static Serilog.Core.Logger ? log;

        static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: SubmissionParser3 companyCode versionID [append]");
                return -1;
            }

            // Hook up a config object
            //
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            // Build the configuration
            IConfiguration config = configurationBuilder.Build();

            // Bind the configuration section to a new Settings object
            settings = new();
            config.GetSection("AppSettings").Bind(settings);

            // Read the debug-level setting from the config
            //
            var loggingSwitch = new LoggingLevelSwitch()
            {
                MinimumLevel = settings.ShowDebugMessages ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information
            };
            
            // Set up a little logger
            //        
            log = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(loggingSwitch)
                .WriteTo.Console()
                .WriteTo.File(settings.logFileFullPath, rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 1000000, retainedFileCountLimit: 200)   // new file every day                
                .CreateLogger();

            // Start writing to the log.  Start with some nice blank lines.
            log.Information("");
            log.Information("");
            log.Information("---------------------");
            log.Information("");

            log.Information($"Starting submission parser version {settings.AppVersion}");

            // Hello, parameters!
            //
            string companyCode = args[0];
            string versionID= args[1];
            

            // Trim whitespace and junk off the end of each parm
            //
            char[] badChars = { '\t', '\r', '\n', ' ' };

            
            versionID = versionID.TrimEnd(badChars);
            companyCode = companyCode.TrimEnd(badChars);

            log.Information($"GCDB Company Code: {companyCode}");
            log.Information($"VersionID: {versionID}");            

            // By default, we will APPEND to the existing files, if they exist.  If the user specifies a third parameter, and it is "YES" or "Y" or "TRUE", then we will start new files.
            //
            bool startNewFiles = false;

            // If they pass THREE arguments, then maybe the third one is a Y or a YES or something else affirming? 
            //
            if (args.Length == 3)
            {
                string _thirdArgument = args[3].TrimEnd(badChars).ToUpper();
                if ((_thirdArgument[..3] == "YES") || (_thirdArgument == "Y") || (_thirdArgument == "TRUE") || (_thirdArgument == "KILL"))
                {
                    // Yes!  They want us to DISCARD any existing EES.TXT, and JOBS.TXT and start new files.
                    //
                    startNewFiles = true;
                }
            }

            try
            {

                // write out what we got for settings
                //
                WriteTheSettingsToTheConsole(settings);

                if (startNewFiles)
                {
                    log.Information($"StartNewFiles was specified as YES, so we are starting new files in {settings.OutputFilePath}");                    
                }
                else
                {
                    log.Information($"StartNewFiles was specified as false, or was omitted, so we are appending to existing files in {settings.OutputFilePath}");
                }
                // Connecting to the database.
                //
                log.Debug($"Connecting to the database");

                string GDSconnectionString = settings.Connectionstring + "Database=" + settings.GDSDatabaseName;
                string StagingConnectionString = settings.Connectionstring + "Database=" + settings.StagingDatabaseName;

                using (SqlConnection connGDS = new SqlConnection(GDSconnectionString))
                using (SqlConnection connStaging = new SqlConnection(StagingConnectionString))
                {
                    connStaging.Open();
                    log.Debug($"Connected to db {settings.GDSDatabaseName}.");

                    // Now connect to the staging database
                    //
                    log.Debug($"Connecting to the staging database {settings.StagingDatabaseName}");

                    connGDS.Open();
                    log.Debug($"Connected to db {settings.StagingDatabaseName}.");

                    string countryCode = ValidateVersionID(versionID, connGDS);
                    if (string.IsNullOrEmpty(countryCode))
                    {
                        log.Fatal($"No country code found for version {versionID}, or no such version.  Quitting");

                        return -1;
                    }

                    if (!ValidateCompanyCode(companyCode, connGDS))
                    {
                        log.Fatal($"No company found for {companyCode}.  Quitting");
                        return -1;
                    }

                    // Get the snapshotID for this company and version
                    //
                    string snapshotID = FindTheSnapshotID(companyCode, versionID, connGDS);
                    if (string.IsNullOrEmpty(snapshotID))
                    {
                        log.Fatal($"No snapshotID found for {companyCode} and {versionID}.  Quitting");
                        return -1;
                    }

                    log.Information($"SnapshotID for {companyCode} and {versionID} is {snapshotID}");

                    string submissionID = FindTheSubmissionID(companyCode, snapshotID, connGDS);

                    // Cook the files
                    //
                    if (CookAllFiles(snapshotID, versionID, submissionID, countryCode, connGDS, connStaging, startNewFiles))
                    {
                        log.Information($"All files cooked successfully.");
                    }
                    else
                    {
                        log.Fatal($"Error cooking files");
                        return -1;
                    }

                    return 0;

                }
            }
            catch (Exception ex)
            {
                log.Information($"Error: {ex.Message}");
                return -1;
            }

            static string FindTheSubmissionID(string companyCode, string snapshotID, SqlConnection connGDS)
            {
                string submissionID = "";
                string _tablename = "datasetincumbent" + snapshotID;
                
                
                using (SqlCommand cmd = new SqlCommand(Constants.SQLFindTheSubmissionID, connGDS))
                {
                    cmd.Parameters.AddWithValue("@companyCode", companyCode);
                    cmd.CommandText = cmd.CommandText.Replace("@tablename", _tablename);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            submissionID = reader["submissionID"].ToString();
                            log.Information($"Submission ID for {companyCode} in  is {submissionID}");
                        }
                        else
                        {
                            log.Fatal($"No submission found for {companyCode}.  Quitting");
                            throw new Exception($"No submission found for {companyCode}.  Quitting");
                        }
                    }
                }
                return submissionID;
            }

            static string FindTheSnapshotID(string companyCode, string versionID, SqlConnection connGDS)
            {
                string _snapshotID = "";
                using (SqlCommand cmd = new SqlCommand(Constants.SQLFindSnapshotID, connGDS))
                {
                    cmd.Parameters.AddWithValue("@companyCode", companyCode);
                    cmd.Parameters.AddWithValue("@versionID", versionID);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            _snapshotID = reader["snapshotID"].ToString();
                            string _versionName = reader["VERSIONNAME"].ToString();
                            log.Information($"Snapshot ID for {companyCode} and {versionID} is {_snapshotID}, which is for the {_versionName} survey");
                        }
                        else
                        {
                            log.Fatal($"No snapshot found for {companyCode} and {versionID}.  Quitting");
                            throw new Exception($"No snapshot found for {companyCode} and {versionID}.  Quitting");
                        }
                    }
                }
                return _snapshotID;
            }

            static bool ValidateCompanyCode(string companyCode, SqlConnection connGDS)
            {
                using (SqlCommand cmd = new SqlCommand(Constants.SQLValidateCompanyCode, connGDS))
                {
                    cmd.Parameters.AddWithValue("@companyCode", companyCode);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            log.Information($"Company code {companyCode} is valid, and belongs to {reader[0]}");
                            return true;
                        }
                        else
                        {                            
                            return false;
                        }
                    }
                }
            }
            /// <summary> Write out all the settings from the config file. </summary>
            /// 
            static void WriteTheSettingsToTheConsole(Settings settings)
            {
                if (settings.ShowDebugMessages.Equals(false))
                {
                    return;
                }

                Type type = typeof(Settings);
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

                log.Information("Settings:");
                foreach (PropertyInfo property in properties)
                {
                    string _name = property.Name;
                    object? _value = property.GetValue(settings);
                    log.Information($"{_name}: {_value}");
                }
                return;
            }



            /// <summary>Get the country code for the versionID.  </summary>
            /// 
            static string ValidateVersionID(string versionID, SqlConnection connGDS)
            {
                string countryCode = "";

                using (SqlCommand cmd = new SqlCommand(Constants.SQLSelectVersion, connGDS))
                {
                    cmd.Parameters.AddWithValue("@versionID", versionID);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            countryCode = reader["COUNTRYCODE"].ToString();
                            log.Information($"Country code for version {versionID} is {countryCode}");
                        }
                        else
                        {
                            log.Fatal($"No country code found for version {versionID}, or version does not exist");
                            throw new Exception($"No country code found for version {versionID}, or version does not exist");
                        }
                    }
                }
                return countryCode;
            }
            


            /// <summary>do the work</summary>
            /// <returns>false if fail </returns>
            static bool CookAllFiles(string snapshotID, string versionID, string submissionID, string country, SqlConnection connGDS, SqlConnection connStaging, bool startNewFiles)
            {
                // the name of the incumbent data table is formed by the string "datasetincumbent" plus the snapshot ID
                //
                string tableName = "datasetincumbent" + snapshotID;

                List<string> columnsInSnapshotTable = new List<string>();
                Dictionary<string, string> nonPayEEColumnsWeWant = new Dictionary<string, string>();


                /// This is the list of PAY columns that we WANT (pay), along with mappings to Comp Software pay codes, plus element types
                DataTable dtPayStandardColumns = new DataTable();

                try
                {


                    using (SqlCommand cmd = new SqlCommand(Constants.SQLSelectColumnNames, connGDS))
                    {
                        cmd.Parameters.AddWithValue("@tablename", tableName);
                        //cmd.CommandText = cmd.CommandText.Replace("@tablename", "'" + tableName + "'");

                        using SqlDataReader reader = cmd.ExecuteReader();

                        while (reader.Read())
                        {
                            string columnName = reader["column_name"]?.ToString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(columnName))
                            {
                                columnsInSnapshotTable.Add(columnName);
                            }
                        }
                        log.Information($"Got {columnsInSnapshotTable.Count} column names from the {tableName} table");
                    }

                    using (SqlCommand cmd = new SqlCommand(Constants.SQLSelectDesiredNonPayColumnNames, connStaging))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string columnName = reader["GCDB_columnHeader"]?.ToString() ?? string.Empty;
                                string csLabel = reader["CS_Label"]?.ToString() ?? string.Empty;
                                if (!string.IsNullOrEmpty(columnName) && !string.IsNullOrEmpty(csLabel))
                                {
                                    nonPayEEColumnsWeWant.Add(columnName, csLabel);
                                }
                            }
                            log.Information($"Got {nonPayEEColumnsWeWant.Count} rows from the non-pay data elements table");
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand(Constants.SQLSelectDesiredPayColumnNames, connStaging))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            dtPayStandardColumns.Load(reader);
                        }
                        log.Information($"Got {dtPayStandardColumns.Rows.Count} rows from the PayCodeMap table");
                    }

                    if (!ValidateColumnsInDataSet(columnsInSnapshotTable, nonPayEEColumnsWeWant, dtPayStandardColumns))
                    {
                        log.Fatal("snapshot table doesn't contain required columns");

                        return false;
                    }

                    log.Information($"Snapshot table {tableName} contains all required columns. Onward!");

                    // Create the job extract:
                    //
                    using (SqlCommand cmd = new SqlCommand(Constants.SQLSelectJobInfo, connGDS))
                    {
                        cmd.Parameters.AddWithValue("@submissionID", submissionID);
                        cmd.CommandText = cmd.CommandText.Replace("@tablename", tableName);
                        log.Debug($"SQL: {cmd.CommandText}");

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            // Create the job extract
                            //
                            DataTable dtJobInfo = new DataTable();
                            dtJobInfo.Load(reader);
                            log.Debug($"Got {dtJobInfo.Rows.Count} jobs");

                            // Write out the job info to a file
                            //
                            string jobFileName = settings.OutputFilePath + "JOBS.txt";
                            if (startNewFiles)
                            {
                                File.Delete(jobFileName);
                            }
                            using (StreamWriter writer = new StreamWriter(jobFileName, true))
                            {
                                foreach (DataRow row in dtJobInfo.Rows)
                                {
                                    string line = string.Join("\t", row.ItemArray);
                                    writer.WriteLine(line);
                                }
                                writer.Close();
                                log.Information($"Wrote {dtJobInfo.Rows.Count} rows to {jobFileName}");
                            }
                        }
                    }

                    // Create the EE extract
                    //
                    using (SqlCommand cmd = new SqlCommand(Constants.SQLExtractEEsFromDataset, connGDS))
                    {
                        cmd.Parameters.AddWithValue("@submissionID", submissionID);
                        cmd.CommandText = cmd.CommandText.Replace("@tablename", tableName);
                        log.Debug($"EE Extract SQL: {cmd.CommandText}");
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            // Create the EE extract
                            //
                            DataTable dtEEInfo = new DataTable();
                            dtEEInfo.Load(reader);
                            log.Debug($"Got {dtEEInfo.Rows.Count} employees");

                            // Write out the EE info to a file
                            //
                            string eeFileName = settings.OutputFilePath + "EES.txt";
                            if (startNewFiles)
                            {
                                File.Delete(eeFileName);
                            }

                            using (StreamWriter writer = new StreamWriter(eeFileName, true))
                            {
                                foreach (DataRow row in dtEEInfo.Rows)
                                {
                                    string line = string.Join("\t", row.ItemArray);
                                    writer.WriteLine(line);
                                }
                                writer.Close();
                                log.Information($"Wrote {dtEEInfo.Rows.Count} rows to {eeFileName}");
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    log.Fatal($"Error: {ex.Message}");
                    throw;
                }
                finally
                {
                    // Clean up the connections
                    //
                    connGDS.Close();
                    connStaging.Close();
                }
                return true;
            }
        }

        /// If the user has asked for verbose messages, or if this is a mustWrite message, then write it to the console.
        /// Pass a true to override the ShowDebugMessages setting.
        private static void WriteToConsoleIfVerbose(string message, bool mustWrite = false)
        {
            if (!settings.ShowDebugMessages && !mustWrite)
            {
                return;
            }
            log.Information(message);
        }

        /// <summary>
        /// Validate the columns in the snapshot table against the columns we want. 
        /// </summary>
        /// <param name="columnsInSnapshotTable">the list of columns in the snapshot table that we are trying to extract from</param>
        /// <param name="nonPayEEColumnsWeWant">A data table containing the non-pay columns that we want</param>
        /// <param name="dtPayStandardColumns">A data table containing the pay columns that we want</param>
        /// <returns>false if the snapshot table missing required columns</returns>
        /// <remarks>logging of WHICH columns are missing is handled here</remarks>
        static bool ValidateColumnsInDataSet(List<string> columnsInSnapshotTable, Dictionary<string, string> nonPayEEColumnsWeWant, DataTable dtPayStandardColumns)
        {
            bool _missingAtLeastOneColumn = false;

            // for each column in the non-pay-columns table, check to see if it is in the snapshot table
            foreach (KeyValuePair<string, string> kvp in nonPayEEColumnsWeWant)
            {
                if (!columnsInSnapshotTable.Contains(kvp.Key))
                {
                    log.Fatal($"The snapshot table doesn't contain a column for  {kvp.Key}, which is requred.  This is bad.");
                    _missingAtLeastOneColumn = true;
                }
            }
            // for each column in the pay-columns table, check to see if it is in the snapshot table
            foreach (DataRow row in dtPayStandardColumns.Rows)
            {
                string columnName = row["GCDB_columnHeader"].ToString();
                if (!columnsInSnapshotTable.Contains(columnName))
                {
                    log.Fatal($"The snapshot table doesn't contain a column for  {columnName}, which is requred.  This is bad.");
                    _missingAtLeastOneColumn = true;
                }
            }

            return !_missingAtLeastOneColumn;

        }
    }

}