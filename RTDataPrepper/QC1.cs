using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RTDataPrepper
{
    internal class QC1
    {
        static private string[] listExtraction;
        static private string[] listIDs;
        static private string folderPath;
        static private DataTable resultsQC1;

        /// <summary>
        /// The purpose of QC1 is to control that the number of DICOM files written corresponds to what was requested to export. 
        /// </summary>
        static public void RunQC1()
        {
            InData inData = new InData();
            folderPath = inData.folderPath;
            listExtraction = inData.ReadExtractionList();
            listIDs = listExtraction.Select(r => r.Split('\t').Skip(1).First()).Distinct().ToArray();

            resultsQC1 = CreateResultTable();

            PerformControls();
            SaveResults(resultsQC1.TableName);
        }

        /// <summary>
        /// Patient-wise control. 
        /// </summary>
        static private void PerformControls()
        {
            foreach (string pseudoID in listIDs)
            {
                int numberOfLines = listExtraction.Where(r => r.Split('\t').Skip(1).First().Equals(pseudoID)).ToArray().Length;
                int numberOfRPFiles = Directory.GetFiles(folderPath + @"\" + pseudoID, "*RP*.dcm").Length;
                int numberOfCTFiles = Directory.GetFiles(folderPath + @"\" + pseudoID, "*CT*.dcm").Length;
                int numberOfRDFiles = Directory.GetFiles(folderPath + @"\" + pseudoID, "*RD*.dcm").Length;
                int numberOfRSFiles = Directory.GetFiles(folderPath + @"\" + pseudoID, "*RS*.dcm").Length;
                bool rightNumberOfFiles = numberOfLines == numberOfRPFiles && numberOfLines == numberOfRDFiles && numberOfCTFiles >= 1 && numberOfRSFiles >= 1 ? true : false;

                DataRow row = resultsQC1.NewRow();
                row["Study ID"] = pseudoID;
                row["# Rows In Indata"] = numberOfLines;
                row["Correct (Same # RP and RD as # Rows)"] = rightNumberOfFiles;
                row["# RP Files"] = numberOfRPFiles;
                row["# RD Files"] = numberOfRDFiles;
                row["# RS Files"] = numberOfRSFiles;
                row["# CT Files"] = numberOfCTFiles;
                resultsQC1.Rows.Add(row);
            }
        }

        /// <summary>
        /// Creates the DataTable for resultsQC1.
        /// </summary>
        static private DataTable CreateResultTable()
        {
            DataTable resultsQC1 = new DataTable();
            resultsQC1.TableName = "resultsQC1";

            CreateColumn(resultsQC1, "Study ID", typeof(string));
            CreateColumn(resultsQC1, "# Rows In Indata", typeof(int));
            CreateColumn(resultsQC1, "Correct (Same # RP and RD as # Rows)", typeof(bool));
            CreateColumn(resultsQC1, "# RP Files", typeof(int));
            CreateColumn(resultsQC1, "# RD Files", typeof(int));
            CreateColumn(resultsQC1, "# RS Files", typeof(int));
            CreateColumn(resultsQC1, "# CT Files", typeof(int));

            return resultsQC1;
        }

        /// <summary>
        /// Creates column in DataTable.
        /// </summary>
        static private void CreateColumn(DataTable dt, string columnName, Type dataType)
        {
            DataColumn newColumn = new DataColumn(columnName, dataType);
            dt.Columns.Add(newColumn);
        }

        // <summary>
        /// Saves the DataTable.
        /// </summary>
        static private void SaveResults(string tableName)
        {
            resultsQC1.WriteXml(folderPath + @"\" + tableName + @".xml");
        }
    }
}
