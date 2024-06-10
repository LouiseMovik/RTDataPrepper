using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTDataPrepper
{
    /// <summary>
    /// TO DO: 
    /// 1. Enter path to the working directory.
    /// 2. Specify requested actions (QC1, Cleanup and/or QC2).
    /// 3. Create lists (described below).
    /// 4. Search for instances of "case-specific" throughout the entire solution and review the code that might require modifications. 
    /// </summary>
    internal class InData
    {
        public string folderPath;
        private string locationOfExtractionList;
        private string locationOfCleanupList;
        private string locationOfQC2List;
        public bool performQC1;
        public bool performCleanup;
        public bool performQC2;

        /// <summary>
        /// Default constructor of InData.
        /// </summary>
        public InData()
        {
            // Enter path to working directory
            //folderPath = @"path_to_working_directory";
            folderPath = @"";

            // Specify requested actions using true/false
            performQC1 = false; 
            performCleanup = true; 
            performQC2= false;

            // listExtraction:
            // A textfile containing a list of what's been requested to export from the clinical ARIA database. 
            // The structure should be Patient ID    Study ID   Course ID    PlanSetupID (tabs delimited).
            // One patient can have several lines in the list since one line per treatment plan is used.
            locationOfExtractionList = folderPath + @"\listExtraction.txt";

            // listCleanup:
            // A textfile containing a list of what's been requested to clean as well as information used to perform cleanup.
            // The structure should be Study ID   Course ID    PlanSetupID  Number of fractions delivered with treatment plan   Date of first fraction delivered with treatment plan (tabs delimited).
            // One patient can have several lines in the list since one line per treatment plan is used.
            locationOfCleanupList = folderPath + @"\listCleanup.txt";

            // listQC2:
            // A textfile containing a list of information to a selected reference structure (we used the left lung).
            // The structure should be Study ID   Corrected dose to reference structure over treatment course  Center point (x)     Center point (y)    Center point (z)    CT number in center point   (tabs delimited).
            // Corrected dose to reference structure over treatment course can be any dose considered suitable. We used the delivered mean dose to the reference structure which was calculated by summarizing the planned mean dose to the reference structure per fraction in each treatment plan multiplied with the number of delivered fractions of that treatment plan. 
            // In this list there should only be one line per patient.
            locationOfQC2List = folderPath + @"\listQC2.txt";
        }
        
        /// <summary>
        /// Reads the list used for extraction. 
        /// </summary>
        public string[] ReadExtractionList()
        {
            string[] listExtraction = File.ReadAllLines(locationOfExtractionList);
            return listExtraction;
        }

        /// <summary>
        /// Reads the list used for extraction. 
        /// </summary>
        public string[] ReadCleanupList()
        {
            string[] listCleanup = File.ReadAllLines(locationOfCleanupList);
            return listCleanup;
        }

        /// <summary>
        /// Reads the list used for QC2. 
        /// </summary>
        public string[] ReadQC2List()
        {
            string[] listQC2 = File.ReadAllLines(locationOfQC2List);
            return listQC2;
        }
    }
}
