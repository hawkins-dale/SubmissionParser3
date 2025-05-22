namespace SubmissionParser3
{
    internal class Constants
    {
        /// <summary>
        /// Validete the company code against the COMPANYDESC table 
        /// </summary>
        public const string SQLValidateCompanyCode = @"SELECT COMPANYDESC.COMPANYDESC FROM admclientprod.COMPANYDESC WHERE COMPANYCODE = @companyCode";

        public const string SQLSelectVersion = @"
                SELECT  
                    versionname, 
                    survey.COUNTRYCODE 
                FROM GdsCompensationDW.gcdbprod.Version AS vers 
                JOIN GdsCompensationDW.gcdbprod.SURVEY AS survey ON survey.SURVEYID = vers.SURVEYID 
                WHERE vers.versionID = @versionID";

        /// <summary>
        /// Identify the columns in the target dataset table
        /// </summary>
        public const string SQLSelectColumnNames = @"SELECT column_name FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = @tablename ORDER BY column_name";

        /// <summary>
        /// get the list of column-names in the standard NonPayCodeMap table
        /// </summary>
        public const string SQLSelectDesiredNonPayColumnNames = @"SELECT GCDB_columnHeader, CS_Label FROM NonPayCodeMap ORDER BY CS_Label";

        /// <summary>
        /// Given a company code and a version, find the snapshotID (and some other stuff for validation) 
        /// </summary>
        public const string SQLFindSnapshotID = @"
        SELECT
		    vers.VERSIONID,
		    versCompany.COMPANYCODE,
		    company.COMPANYDESC,
		    VERSIONNAME,
		    VERSIONSTATUS,
		    snapshotID
	    FROM gcdbprod.VERSION AS vers
	    JOIN gcdbprod.VERSIONCOMPANY  AS versCompany ON versCompany.VERSIONID = vers.VERSIONID
	    JOIN admclientprod.COMPANYDESC AS company ON company.COMPANYCODE = versCompany.COMPANYCODE
	    WHERE 
	    versCompany.COMPANYCODE = @companyCode AND 
	    vers.VERSIONID 	= @versionID
        ";

        /// <summary>
        /// Extract job data from the dataset table
        /// </summary>
        public const string SQLSelectJobInfo = @"
            SELECT DISTINCT
		            JOBSPECIALTYCODE + '-' + JOBLEVELCODE  + '-' + JOBGRADECODE AS jobCode,
	            countrycode AS jobRegion,
	            countryCode AS country,
	            JOBLEVELCODE AS careerLevel,
	            CASE WHEN LEN(JOBSPECIALTYCODE) > 3 THEN RIGHT(JOBSPECIALTYCODE, 3) ELSE '' END AS disciplineCode,
	            JOBSPECIALTYCODE AS functionCode,
	            YOURPOSITIONTITLE AS jobQualifier,
	            YOURPOSITIONTITLE AS jobTitle,
	            JOBSPECIALTYCODE + '-' + JOBLEVELCODE  + '-' + JOBGRADECODE AS WTWSurveyJobCode
            FROM GdsCompensationDS.dbo.@tablename
            WHERE ident IS NOT null
            AND submissionid = @submissionID
            ORDER BY jobCode";

        /// <summary>
        /// Extract the EE data from the dataset table
        /// </summary>
        public const string SQLExtractEEsFromDataset = @"
                SELECT 	
	        ident AS eecode,
	        JOBSPECIALTYCODE + '-' + JOBLEVELCODE  + '-' + JOBGRADECODE AS jobCode,
	        countrycode AS jobRegion,
	        countryCode AS country,
	        JOBLEVELCODE AS careerLevel,
	        CASE WHEN LEN(JOBSPECIALTYCODE) > 3 THEN RIGHT(JOBSPECIALTYCODE, 3) ELSE '' END AS disciplineCode,
	        JOBSPECIALTYCODE AS functionCode,
	        YOURPOSITIONTITLE AS jobQualifier,
	        YOURPOSITIONTITLE AS jobTitle,
	        JOBSPECIALTYCODE + '-' + JOBLEVELCODE  + '-' + JOBGRADECODE AS WTWSurveyJobCode,
	        ISNULL(FC_CARBEN_TYP, '') AS carBenefitType,
	        ISNULL(JOBGRADECODE, '')  AS incumbentGlobalGrade,
	        ISNULL(CORP_NONCORP, '') AS corpNoncorp,
	        ISNULL(FD_DOB_DAT, '')  AS dateOfBirth,
	        ISNULL(FD_DOH_DAT, '')  AS dateOfHire,
	        ISNULL(FC_DEPT_TXT, '')  AS departmentName,
	        ISNULL(BUS_UNIT_NAME, '')  AS businessUnitName,
	        ISNULL(SALES_ELIGIBLE, '')  AS eligibleforCommission,
	        ISNULL(LTIE, '')  AS LTI_Eligible,
	        ISNULL(PC_PB_ELI, '')  AS eligibleforPerformanceBonus,
	        ISNULL(FC_VOPS_ELI, '')  AS eligibleforProfitSharing	,
	        ISNULL(FC_RET_ELI, '')  AS eligibleForRetirementPlan,
	        ISNULL(FC_GEND_COD, '')  AS Gender,
	        ISNULL(GRADE, '')  AS internalGradeLevelBand,
	        BASE  AS basePay ,
	        TOT_FIX_CASH  AS  TFCA,
	        FIXED_GUAR_BON  AS FIXB,
	        FN_DISCBON_ANN   AS DISC,
	        FN_PB_ANN   AS BONU,
	        SALES_INCENTIVE  AS ASI,
	        FN_PBTARG_PBS  AS TARP,
	        TGT_SALES  AS TSI,
	        AREACODE  AS areaCode
        FROM GdsCompensationDS.dbo.@tablename
        WHERE IDENT IS NOT null
        AND submissionid = @submissionID";

        public const string SQLFindTheSubmissionID = @"
            SELECT submissionID, versionid, company_name GdsCompensationDS.dbo.@tablename
	        WHERE companycode = @companyCode";

        /// <summary>
        /// Get the list of column-names in the standard PayCodeMap table
        /// </summary>
        public const string SQLSelectDesiredPayColumnNames = @"SELECT * 
                        FROM dbo.PayCodeMap 
                        ORDER BY CS_pay_code";
    }
}
