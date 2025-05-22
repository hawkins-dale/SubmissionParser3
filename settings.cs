namespace SubmissionParser3
{
    public class Settings
    {

        public string ApplicationName { get; set; }

        public string AppVersion { get; set; }

        /// <summary>
        /// Do we want verbose error messages?  A lame substitute for a real logging framework.
        /// </summary>
        public bool ShowDebugMessages { get; set; }

        /// <summary>
        /// The connection string to the database, not including the database name
        /// </summary>
        public string Connectionstring {get; set;}

        /// <summary>
        /// The name of the database that contains the reference tables
        /// </summary>
        public string? StagingDatabaseName { get; set; } = "PremiumProductStaging";

        /// <summary>
        /// The name of the database that contains the datasets
        /// </summary>
        public string GDSDatabaseName { get; set; } = "GdsCompensationDS";

        /// <summary>
        /// To what share will we write the output files?
        /// </summary>
        public string OutputFilePath { get; set; }

        /// <summary>
        /// Where does the log file go?
        /// </summary>
        public string logFileFullPath { get; set; }

    }

}
