using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Schema;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace RTDataPrepper
{
    internal class QC2
    {
        static private Application app;
        static private DataTable resultsQC2;
        static private Patient patient = null;
        static private Structure referenceStructure = null;
        static private string folderPath;
        static private string[] listExtraction;
        static private string[] listQC2;
        static private string[] listIDs;
        static private double doseTolerance;
        static private double distanceTolerance;
        static private double HUTolerance;

        /// <summary>
        /// The purpose of QC2 is to:
        /// 1. Control that all patients have been created in the research ARIA database.
        /// 2. Control that the number of created treatment plans is correct for each patient.
        /// 3. Control correctness of treatment plan IDs.
        /// 4. Control correctness of dose distributions:
        ///     -   That all treatment plans have dose.
        ///     -   That the delivered dose to the selected reference structures from the treatment course is correct.
        /// 5. Control correctness of geometries:
        ///     -	That centre points for reference structures are correct.
        ///     -	That CT numbers in centre points are correct.
        /// </summary>
        static public void RunQC2()
        {
            Preparations();

            // Tolerances
            doseTolerance = 0.1; // [Gy]
            distanceTolerance = 0.3; // [mm]
            HUTolerance = 5; // [HU]

            PerformControl();
            SaveResults(resultsQC2.TableName);
            app.Dispose();
        }

        /// <summary>
        /// Initializes the quality control process by creating an ESAPI application and reading data from the InData class.
        /// </summary>
        static private void Preparations()
        {
            app = Application.CreateApplication();

            InData inData = new InData();
            folderPath = inData.folderPath;
            listExtraction = inData.ReadExtractionList();
            listQC2 = inData.ReadQC2List();
            listIDs = listExtraction.Select(r => r.Split('\t').Skip(1).First()).Distinct().ToArray(); 
            
            CreateDataTable();
        }

        /// <summary>
        /// Patient-wise QA. 
        /// </summary>
        static private void PerformControl()
        {
            foreach (string studyID in listIDs)
            {
                ControlAndWriteResult(studyID);
            }
        }

        /// <summary>
        /// Performs all controls and enters data in the DataTable QA.
        /// </summary>
        static private void ControlAndWriteResult(string studyID)
        {
            DataRow patientRow = resultsQC2.NewRow();
            patientRow["Study ID"] = studyID;

            try
            {
                if (PatientExists(studyID))
                {
                    patientRow["Patient Exists"] = true;
                    patient = app.OpenPatientById(studyID);

                    // Plan controls
                    ControlNumberOfPlans(studyID, patientRow);
                    patientRow["Correct Names Of Plans"] = CorrectNamesOfPlans();

                    // Dose control
                    patientRow["Plans Have Dose"] = PlansHaveDose();

                    // Controls of reference structure
                    SelectReferenceStructure(); // Case-specific method
                    ControlDose(studyID, patientRow);
                    ControlCenterPointLocation(studyID, referenceStructure, patientRow);
                    ControlHUinCenterPoint(studyID, referenceStructure, patientRow);

                    // Controls if all is correct
                    patientRow["Everything Correct"] = IsCorrect(patientRow);
                }
            }
            catch
            {
            }
            
            app.ClosePatient();
            resultsQC2.Rows.Add(patientRow);
        }
        
        /// <summary>
        /// Checks if following is correct:
        /// - Patient Exists
        /// - Correct No Of Plans
        /// - Correct Names Of Plans
        /// - Plans Have Dose
        /// - Plans Have Correct Dose
        /// - Correct CenterPoint
        /// - Correct HU Among Neighbours
        /// </summary>
        static private bool IsCorrect(DataRow patientRow)
        {
            bool isCorrect = true;

            isCorrect = isCorrect && Convert.ToBoolean(patientRow["Patient Exists"].ToString());
            isCorrect = isCorrect && Convert.ToBoolean(patientRow["Correct No Of Plans"].ToString());
            isCorrect = isCorrect && Convert.ToBoolean(patientRow["Correct Names Of Plans"].ToString());
            isCorrect = isCorrect && Convert.ToBoolean(patientRow["Plans Have Dose"].ToString());
            isCorrect = isCorrect && Convert.ToBoolean(patientRow["Plans Have Correct Dose"].ToString());
            isCorrect = isCorrect && Convert.ToBoolean(patientRow["Correct CenterPoint"].ToString());
            isCorrect = isCorrect && Convert.ToBoolean(patientRow["Correct HU Among Neighbours"].ToString());

            return isCorrect;
        }

        /// <summary>
        /// Checks if a patient with the pseudo ID exist in research Eclipse.
        /// </summary>
        static private bool PatientExists(string studyID)
        {
            if (app.PatientSummaries.Where(r => r.Id.Equals(studyID)).Count() > 0)
                return true;
            return false;
        }

        /// <summary>
        /// Case-specific method with the purpose to identify and select the predefined reference structure from a patient's radiotherapy plan. 
        /// </summary>
        static private void SelectReferenceStructure()
        {
            // The code below is an example. In this case the left lung was used as reference structure. If the patient only had one lung delineation that delineation was used (could be left, right or total lung).
            var lungStructures = patient.Courses.First().PlanSetups.First(r => r.Id.Equals("P1")).StructureSet.Structures.Where(r => r.Id.StartsWith("Lung"));
            if (lungStructures.Count() > 1)
            {
                referenceStructure = lungStructures.First(r => r.Id.Equals("Lung_L"));
            }
            else
            {
                referenceStructure = lungStructures.First();
            }
        }

        /// <summary>
        /// Controls whether the number of plans in research Eclipse correspond to the number of plans extracted from clinical Eclipse.
        /// </summary>
        static private void ControlNumberOfPlans(string studyID, DataRow patientRow)
        {
            int plansClinical = listExtraction.Where(r => r.Split('\t').Skip(1).First().Equals(studyID)).ToArray().Length;
            int plansResearch = NumberOfPlans();
            bool correctNoOfPlans = plansResearch == plansClinical;

            patientRow["Correct No Of Plans"] = correctNoOfPlans;
            patientRow["No Of Plans Extracted"] = plansClinical;
            patientRow["No Of Plans In Research"] = plansResearch;
        }

        /// <summary>
        /// Counts the number of plans in research Eclipse.
        /// </summary>
        static private int NumberOfPlans()
        {
            return patient.Courses.First().ExternalPlanSetups.Count();
        }

        /// <summary>
        /// Controls that the plans are named P1, P2,...
        /// </summary>
        static private bool CorrectNamesOfPlans()
        {
            bool correctNames = true;
            for (int i = 1; i <= NumberOfPlans(); i++)
            {
                bool correctName = patient.Courses.First().ExternalPlanSetups.Where(r => r.Id.Equals("P" + i)).Count() == 1;
                correctNames = correctNames && correctName;
            }
            return correctNames;
        }

        /// <summary>
        /// Checks if all plans have valid doses. 
        /// </summary>
        static private bool PlansHaveDose()
        {
            bool plansHaveDose = true;
            foreach (ExternalPlanSetup plan in patient.Courses.First().ExternalPlanSetups)
            {
                plansHaveDose = plan.IsDoseValid && plansHaveDose;
            }
            return plansHaveDose;
        }

        /// <summary>
        /// Controls agreement between extracted mean dose to reference structure (weighted for number of fractions delivered) and the mean dose to the reference structure in research Eclipse.
        /// </summary>
        static private void ControlDose(string studyID, DataRow patientRow)
        {
            double clinicalDose = Math.Round(double.Parse(listQC2.Where(r => r.Split('\t').First().Equals(studyID)).ToList().Select(r => r.Split('\t').Skip(1).First()).First()), 2);
            double researchDose = DoseInEclipse();
            bool correctDose = Math.Abs(clinicalDose - researchDose) < doseTolerance;

            patientRow["Plans Have Correct Dose"] = correctDose;
            patientRow["Dose In Clinical"] = clinicalDose;
            patientRow["Dose In Research"] = researchDose;
        }

        /// <summary>
        /// Calculates the accumulated mean lung dose in the treatment course to the reference structure. Case-specific method.
        /// </summary>
        static private double DoseInEclipse()
        {
            // Calculation of dose to the reference structure
            double dose = 0;
            foreach (ExternalPlanSetup plan in patient.Courses.First().ExternalPlanSetups)
            {
                try
                {
                    dose += plan.GetDVHCumulativeData(referenceStructure, DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.1).MeanDose.Dose;
                }
                catch
                {
                }
            }
            return Math.Round(dose, 2);

            // Calculation of dose to the all lung structures
            //double dose = 0; 
            //foreach (ExternalPlanSetup plan in patient.Courses.First().ExternalPlanSetups)
            //{
            //    int numberOfLungStructures = plan.StructureSet.Structures.Where(r => r.Id.Equals("Lung_R") || r.Id.Equals("Lung_L") || r.Id.Equals("LungTotal")).Count();
            //    if (numberOfLungStructures == 1)
            //    {
            //        try
            //        {
            //            dose += plan.GetDVHCumulativeData(plan.StructureSet.Structures.First(r => r.Id.Equals("LungTotal")), DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.1).MeanDose.Dose;
            //        }
            //        catch
            //        {
            //            try
            //            {
            //                dose += plan.GetDVHCumulativeData(plan.StructureSet.Structures.First(r => r.Id.Equals("Lung_R")), DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.1).MeanDose.Dose;
            //            }
            //            catch
            //            {
            //                dose += plan.GetDVHCumulativeData(plan.StructureSet.Structures.First(r => r.Id.Equals("Lung_L")), DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.1).MeanDose.Dose;
            //            }
            //        }
            //    }
            //    else if (numberOfLungStructures == 2)
            //    {
            //        dose += plan.GetDVHCumulativeData(plan.StructureSet.Structures.First(r => r.Id.Equals("Lung_R")), DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.1).MeanDose.Dose * plan.StructureSet.Structures.First(r => r.Id.Equals("Lung_R")).Volume / (plan.StructureSet.Structures.First(r => r.Id.Equals("Lung_R")).Volume + plan.StructureSet.Structures.First(r => r.Id.Equals("Lung_L")).Volume);
            //        dose += plan.GetDVHCumulativeData(plan.StructureSet.Structures.First(r => r.Id.Equals("Lung_L")), DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.1).MeanDose.Dose * plan.StructureSet.Structures.First(r => r.Id.Equals("Lung_L")).Volume / (plan.StructureSet.Structures.First(r => r.Id.Equals("Lung_R")).Volume + plan.StructureSet.Structures.First(r => r.Id.Equals("Lung_L")).Volume);
            //    }
            //}
            //return Math.Round(dose, 2);
        }

        /// <summary>
        /// Controls agreement between extracted CenterPoint location and the corresponding in research Eclipse.
        /// </summary>
        static private void ControlCenterPointLocation(string studyID, Structure structure, DataRow patientRow)
        {
            try
            {
                VVector clinicalCenterPoint = ArrayToVVector(listQC2.Where(r => r.Split('\t').First().Equals(studyID)).ToList().Select(r => r.Split('\t').Skip(2).First()).First().Split(',').Select(r => double.Parse(r)).ToArray());
                VVector researchCenterPoint = structure.CenterPoint;
                double distance = VVector.Distance(clinicalCenterPoint, researchCenterPoint);

                patientRow["Correct CenterPoint"] = distance < distanceTolerance;
                patientRow["CenterPoint In Clinical"] = VVectorToString(clinicalCenterPoint);
                patientRow["CenterPoint In Research"] = VVectorToString(researchCenterPoint);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Controls agreement between extracted CT number in CenterPoint and the corresponding in research Eclipse. Additionally, it checks the CT numbers in the neighboring voxels. 
        /// </summary>
        static private void ControlHUinCenterPoint(string studyID, Structure structure, DataRow patientRow)
        {
            try
            {
                VVector clinicalCenterPoint = ArrayToVVector(listQC2.Where(r => r.Split('\t').First().Equals(studyID)).ToList().Select(r => r.Split('\t').Skip(2).First()).First().Split(',').Select(r => double.Parse(r)).ToArray());
                VVector researchCenterPoint = structure.CenterPoint;
                double clinicalCenterPointHU = listQC2.Where(r => r.Split('\t').First().Equals(studyID)).ToList().Select(r => r.Split('\t').Skip(3).First()).Select(r => double.Parse(r)).First();
                double researchCenterPointHU = GetHUAtLocation(patient.Courses.First().PlanSetups.First(r => r.Id.Equals("P1")).StructureSet.Image, researchCenterPoint.x, researchCenterPoint.y, researchCenterPoint.z);
                List<double> neighbors = GetHUNeighbors(patient.Courses.First().PlanSetups.First(r => r.Id.Equals("P1")).StructureSet.Image, researchCenterPoint.x, researchCenterPoint.y, researchCenterPoint.z);

                patientRow["Correct HU In CenterPoint"] = Math.Abs(clinicalCenterPointHU - researchCenterPointHU) < HUTolerance;
                patientRow["Correct HU Among Neighbours"] = neighbors.Where(r => r == clinicalCenterPointHU).Count() > 0;
                patientRow["HU In CenterPoint In Clinical"] = clinicalCenterPointHU;
                patientRow["HU In CenterPoint In Research"] = researchCenterPointHU;
                patientRow["HU Among Neighbours In Research"] = string.Join(",", neighbors);
            }
            catch
            {
            }
        }

        static private VVector ArrayToVVector(double[] array)
        {
            VVector vVector = new VVector(array[0], array[1], array[2]);
            return vVector;
        }

        static private string VVectorToString(VVector vv)
        {
            string stringOut = vv.x + "," + vv.y + "," + vv.z;
            return stringOut;
        }

        static private double GetHUAtLocation(Image image, double x0, double y0, double z0)
        {
            int[,] buffer = new int[image.XSize, image.YSize];
            double[,] hu = new double[image.XSize, image.YSize];

            //Calculate the voxel locations (integers) from actual 3D spatial coordinates
            var dx = (x0 - image.Origin.x) / (image.XRes * image.XDirection.x);
            var dy = (y0 - image.Origin.y) / (image.YRes * image.YDirection.y);
            var dz = (z0 - image.Origin.z) / (image.ZRes * image.ZDirection.z);

            //Fill buffer with voxels from current slice
            image.GetVoxels((int)dz, buffer);
            int xmax = image.XSize;
            int ymax = image.YSize;
            for (int x = 0; x < xmax; x++)
            {
                for (int y = 0; y < ymax; y++)
                {
                    //Set HU from "voxel value" - have to convert
                    hu[x, y] = image.VoxelToDisplayValue(buffer[x, y]);
                }
            }
            return hu[(int)Math.Round(dx), (int)Math.Round(dy)];
        }

        static private List<double> GetHUNeighbors(Image image, double x0, double y0, double z0)
        {
            int[,] buffer = new int[image.XSize, image.YSize];
            double[,] hu = new double[image.XSize, image.YSize];
            List<double> huOut = new List<double>();

            //Calculate the voxel locations (integers) from actual 3D spatial coordinates
            var dx = (x0 - image.Origin.x) / (image.XRes * image.XDirection.x);
            var dy = (y0 - image.Origin.y) / (image.YRes * image.YDirection.y);
            var dz = (z0 - image.Origin.z) / (image.ZRes * image.ZDirection.z);

            for (int z = (int)dz - 1; z <= (int)dz + 1; z++)
            {
                //Fill buffer with voxels from current slice
                image.GetVoxels(z, buffer);
                int xmax = image.XSize;
                int ymax = image.YSize;
                for (int x = 0; x < xmax; x++)
                {
                    for (int y = 0; y < ymax; y++)
                    {
                        //Set HU from "voxel value" - have to convert
                        hu[x, y] = image.VoxelToDisplayValue(buffer[x, y]);
                    }
                }

                for (int i = (int)Math.Round(dx) - 1; i <= (int)Math.Round(dx) + 1; i++)
                {
                    for (int j = (int)Math.Round(dy) - 1; j <= (int)Math.Round(dy) + 1; j++)
                    {
                        huOut.Add(hu[i, j]);
                    }
                }
            }

            return huOut;
        }

        /// <summary>
        /// Creates the result DataTable.
        /// </summary>
        static private void CreateDataTable()
        {
            resultsQC2 = new DataTable();
            resultsQC2.TableName = "ResultsQC2";

            CreateColumn(resultsQC2, "Study ID", typeof(string));
            CreateColumn(resultsQC2, "Everything Correct", typeof(bool));

            CreateColumn(resultsQC2, "Patient Exists", typeof(bool));
            CreateColumn(resultsQC2, "Correct No Of Plans", typeof(bool));
            CreateColumn(resultsQC2, "Correct Names Of Plans", typeof(bool));
            CreateColumn(resultsQC2, "Plans Have Dose", typeof(bool));
            CreateColumn(resultsQC2, "Plans Have Correct Dose", typeof(bool));
            CreateColumn(resultsQC2, "Correct CenterPoint", typeof(bool));
            CreateColumn(resultsQC2, "Correct HU In CenterPoint", typeof(bool));
            CreateColumn(resultsQC2, "Correct HU Among Neighbours", typeof(bool));
            CreateColumn(resultsQC2, "Correct No Of Voxels", typeof(bool));
            CreateColumn(resultsQC2, "Correct Mean HU", typeof(bool));
            CreateColumn(resultsQC2, "Correct Std In HU Distribution", typeof(bool));
            CreateColumn(resultsQC2, "Correct Isocenter", typeof(bool));

            CreateColumn(resultsQC2, "No Of Plans Extracted", typeof(int));
            CreateColumn(resultsQC2, "No Of Plans In Research", typeof(int));
            CreateColumn(resultsQC2, "Dose In Clinical", typeof(double));
            CreateColumn(resultsQC2, "Dose In Research", typeof(double));
            CreateColumn(resultsQC2, "CenterPoint In Clinical", typeof(string));
            CreateColumn(resultsQC2, "CenterPoint In Research", typeof(string));
            CreateColumn(resultsQC2, "HU In CenterPoint In Clinical", typeof(double));
            CreateColumn(resultsQC2, "HU In CenterPoint In Research", typeof(double));
            CreateColumn(resultsQC2, "HU Among Neighbours In Research", typeof(string));
            CreateColumn(resultsQC2, "No Of Voxels In Clinical", typeof(double));
            CreateColumn(resultsQC2, "No Of Voxels In Research", typeof(double));
            CreateColumn(resultsQC2, "Mean HU In Clinical", typeof(double));
            CreateColumn(resultsQC2, "Mean HU In Research", typeof(double));
            CreateColumn(resultsQC2, "Std In HU Distribution In Clinical", typeof(double));
            CreateColumn(resultsQC2, "Std In HU Distribution In Research", typeof(double));
            CreateColumn(resultsQC2, "Isocenter In Clinical", typeof(string));
            CreateColumn(resultsQC2, "Isocenter In Research", typeof(string));
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
            resultsQC2.WriteXml(folderPath + @"\" + tableName + @".xml");
        }
    }
}
