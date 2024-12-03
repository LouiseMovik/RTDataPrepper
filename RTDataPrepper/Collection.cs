using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

[assembly: ESAPIScript(IsWriteable = true)]

namespace RTDataPrepper
{
    internal class Collection
    {
        static private Application app;
        static private Patient patient;
        static private string[] listExtraction;
        static private string[] listIDs;
        static private string folderPath;
        static private DataTable resultsCollection;

        /// <summary>
        /// This is an example of data collection from the research ARIA database. The data has been cleaned and prepared within the automated workflow. 
        /// Therefore, only relevant treatment plans exist in the research ARIA database, they have a structured nomenclature and the doses are weighed 
        /// for the fraction of the treatment that was actually delivered. Because of this preparation, the dose to an OAR can easily be summarized. 
        /// Two different methods to collect the dose are shown in the code below. 
        /// </summary>
        static public void RunCollection()
        {
            // An ESAPI application
            app = Application.CreateApplication();

            // Information from the indata class
            InData inData = new InData();
            folderPath = inData.folderPath;

            // List of strings holding the patientsIDs to loop through
            listExtraction = inData.ReadExtractionList();
            listIDs = listExtraction.Select(r => r.Split('\t').Skip(1).First()).Distinct().ToArray();

            // DataTable to store the results in
            CreateDataTable();

            foreach (var ID in listIDs)
            {
                // Instance variable holding the current patient
                patient = app.OpenPatientById(ID);

                // Calculate the mean dose to an OAR
                double meanDose = MeanDose("Heart");

                // Calculate volume at dose (V_XGy) from a generated PlanSum
                double volumeAtDose = VolumeAtDose(20, "Heart");

                // Crate one row in the DataTable for the considered patient. The 
                DataRow row = resultsCollection.NewRow();
                row["Study ID"] = ID;
                row["Mean dose [Gy]"] = meanDose;
                row["V_20Gy [%]"] = volumeAtDose;
                resultsCollection.Rows.Add(row);

                app.ClosePatient();
            }
            // Writes the resulting DataTable to disk
            SaveResults("CollectedResults");

            app.Dispose();
        }

        /// <summary>
        /// Calculates the mean lung dose to the structure with the ID used as argument.
        /// </summary>
        static private double MeanDose(string structureID)
        {
            double dose = 0;
            foreach (ExternalPlanSetup plan in patient.Courses.First().ExternalPlanSetups)
            {
                try
                {
                    dose += plan.GetDVHCumulativeData(plan.StructureSet.Structures.First(r => r.Id.Equals(structureID)), DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.1).MeanDose.Dose;
                }
                catch
                {
                    // The structure does not exist
                }
            }
            return Math.Round(dose, 2);
        }

        /// <summary>
        /// Calculates the mean lung dose to the structure with the ID used as argument.
        /// </summary>
        static private double VolumeAtDose(int doseLevel, string structureID)
        {
            patient.BeginModifications();
            double volumeAtDose = 0;

            // Create a PlanSum with all treatment plans
            List<PlanningItem> planningItems = new List<PlanningItem>();
            Image image = null;
            foreach (ExternalPlanSetup plan in patient.Courses.First().ExternalPlanSetups.Where(r => r.IsDoseValid))
            {
                planningItems.Add(plan);
                if (image == null)
                {
                    image = plan.StructureSet.Image;
                }
                else
                {
                    Image newImage = plan.StructureSet.Image;
                    if (image != newImage)
                    {
                        // The treatment plans are based on more than one CT scan. Volume at dose cannot be calculated.
                        return volumeAtDose;
                    }
                }
            }
            PlanSum planSum = patient.Courses.First().CreatePlanSum(planningItems, image);

            try
            {
                // Identify the considered OAR
                Structure structure = planSum.StructureSet.Structures.First(r => r.Id.Equals(structureID));

                // Calculate the dose volume parameter
                var dvh = planSum.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                volumeAtDose = dvh.CurveData.First(c => Math.Round(c.DoseValue.Dose, 3) == doseLevel).Volume;
            }
            catch 
            {
                // The structure does not exist
            }
            
            return volumeAtDose;
        }

        /// <summary>
        /// Creates the DataTable QA.
        /// </summary>
        static private void CreateDataTable()
        {
            resultsCollection = new DataTable();
            resultsCollection.TableName = "Results";

            CreateColumn(resultsCollection, "Study ID", typeof(string));
            CreateColumn(resultsCollection, "Mean dose [Gy]", typeof(double));
            CreateColumn(resultsCollection, "V_20Gy [%]", typeof(double));
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
            resultsCollection.WriteXml(folderPath + @"\" + tableName + @".xml");
        }
    }
}
