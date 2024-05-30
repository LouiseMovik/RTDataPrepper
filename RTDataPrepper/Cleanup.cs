using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvilDICOM.Core;
using EvilDICOM.Core.Helpers;
using System.IO;
using EvilDICOM.Core.Element;
using System.Data;
using System.CodeDom.Compiler;
using System.Numerics;

namespace RTDataPrepper
{
    internal class Cleanup
    {
        private static string folderPath;
        static private string[] listCleanup;
        private static StudyRTPlan studyPlan;

        /// <summary>
        /// The purpose of Cleanup is to:
        /// 1. Change the prescribed number of fractions in the treatment plans to what was delivered. 
        /// 2. Change IDs of treatment plans according to structured nomenclature.
        /// 3. Change statuses of treatment plans and structure sets to Unapproved.
        /// 4. Remove connections between treatment plans.
        /// 5. Change IDs of considered OARs to the Swedish standardized nomenclature. 
        /// </summary>
        static public void RunCleanup()
        {
            InData inData = new InData();
            folderPath = inData.folderPath;
            listCleanup = inData.ReadCleanupList();

            PerformCleanupActions();
        }

        /// <summary>
        /// Plan-wise cleanup.
        /// </summary>
        static private void PerformCleanupActions()
        {
            foreach (string planLine in listCleanup)
            {
                try
                {
                    studyPlan = new StudyRTPlan()
                    {
                        PseudoID = planLine.Split('\t').First(),
                        PlanID = planLine.Split('\t').Skip(1).First(),
                        DeliveredFractions = Convert.ToInt32(planLine.Split('\t').Skip(2).First()),
                        DateOfFirstFraction = Convert.ToDateTime(planLine.Split('\t').Skip(3).First()),
                    };

                    DICOMObject currentPlan = FindPlan(out string pathPlan);
                    DICOMObject currentDose = FindDose(currentPlan, out string pathDose);

                    EditNumberOfFractions(currentPlan, currentDose);
                    EditPlanName(currentPlan);
                    EditStructureNames(); // Case-specific modifications!
                    RemoveSetupNotes(currentPlan);

                    currentPlan.Write(pathPlan);
                    currentDose.Write(pathDose);
                }
                catch
                {

                }
            }
        }

        /// <summary>
        /// Finds the DICOM file for the current plan and reads it.
        /// </summary>
        static private DICOMObject FindPlan(out string path)
        {
            var RPpaths = Directory.GetFiles(folderPath + @"\" + studyPlan.PseudoID, "*RP*.dcm", SearchOption.AllDirectories);

            foreach (var RPpath in RPpaths)
            {
                DICOMObject tempPlan = DICOMObject.Read(RPpath);
                var thisPlanID = tempPlan.FindFirst(TagHelper.RTPlanLabel).DData;

                if (thisPlanID.Equals(studyPlan.PlanID))
                {
                    path = RPpath;
                    return tempPlan;
                }
            }
            path = "";
            return null;
        }

        /// <summary>
        /// Finds the DICOM file for the current dose and reads it.
        /// </summary>
        static private DICOMObject FindDose(DICOMObject currentPlan, out string path)
        {
            var RDpaths = Directory.GetFiles(folderPath + @"\" + studyPlan.PseudoID, "*RD*.dcm", SearchOption.AllDirectories);
            foreach (var RD in RDpaths)
            {
                DICOMObject tempDose = DICOMObject.Read(RD);
                var thisReferencedSOPInstanceUID = tempDose.FindFirst(TagHelper.ReferencedSOPInstanceUID).DData.ToString();
                if (thisReferencedSOPInstanceUID.Equals(currentPlan.FindFirst(TagHelper.SOPInstanceUID).DData.ToString()))
                {
                    path = RD;
                    return tempDose;
                }
            }
            path = "";
            return null;
        }

        /// <summary>
        /// Edits the number of prescribed fractions and the dosegridscaling.
        /// </summary>
        static private void EditNumberOfFractions(DICOMObject currentPlan, DICOMObject currentDose)
        {
            var PrescribedNumberOfFractions = currentPlan.FindFirst(TagHelper.NumberOfFractionsPlanned).DData;
            var OldDoseScale = currentDose.FindFirst(TagHelper.DoseGridScaling).DData;

            var NumberOfFractions = new IntegerString
            {
                DData = studyPlan.DeliveredFractions,
                Tag = TagHelper.NumberOfFractionsPlanned
            };
            currentPlan.Replace(NumberOfFractions);

            var DoseScale = new DecimalString
            {
                DData = Convert.ToDouble(OldDoseScale) * studyPlan.DeliveredFractions / Convert.ToDouble(PrescribedNumberOfFractions),
                Tag = TagHelper.DoseGridScaling
            };
            currentDose.Replace(DoseScale);
        }

        /// <summary>
        /// Edits the ID and name of the plan.
        /// </summary>
        static private void EditPlanName(DICOMObject currentPlan)
        {
            System.DateTime[] otherPlansForPatient = listCleanup.Where(r => r.StartsWith(studyPlan.PseudoID)).Select(r => Convert.ToDateTime(r.Split('\t').Skip(3).First())).OrderBy(r => r.Date).ToArray();
            string currentPlanID = currentPlan.FindFirst(TagHelper.RTPlanLabel).DData.ToString();
            int planNumber = 0;

            if (otherPlansForPatient.ToList().Where(r => r.Equals(studyPlan.DateOfFirstFraction)).Count() == 1)
            {
                planNumber = Array.IndexOf(otherPlansForPatient, studyPlan.DateOfFirstFraction) + 1;
            }
            else if (otherPlansForPatient.ToList().Where(r => r.Equals(studyPlan.DateOfFirstFraction)).Count() > 1)
            {
                string[] otherPlanNamesForPatient = listCleanup.Where(r => r.StartsWith(studyPlan.PseudoID)).Select(r => r.Split('\t').Skip(1).First()).ToArray();
                planNumber = Array.IndexOf(otherPlanNamesForPatient, currentPlanID) + 1;
            }

            try
            {
                currentPlan.FindFirst(TagHelper.RTPlanName).DData = currentPlanID;
            }
            catch
            {
                var RTName = new ShortString
                {
                    DData = currentPlanID,
                    Tag = TagHelper.RTPlanName
                };
                currentPlan.Add(RTName);
            }

            currentPlan.FindFirst(TagHelper.RTPlanLabel).DData = "P" + planNumber.ToString();
        }

        /// <summary>
        /// Edits the IDs of the esophagus and heart to the standardized nomenclature used in Sweden.
        /// </summary>
        static private void EditStructureNames()
        {
            string firstPlanForPatient = listCleanup.Where(r => r.StartsWith(studyPlan.PseudoID)).Select(r => r.Split('\t').Skip(1).First()).First();
            if (firstPlanForPatient.Equals(studyPlan.PlanID)) // This is only done one time per patient
            {
                var RSpaths = Directory.GetFiles(folderPath + @"\" + studyPlan.PseudoID, "*RS*.dcm", SearchOption.AllDirectories);
                foreach (string RSpath in RSpaths)
                {
                    DICOMObject currentStructureSet = DICOMObject.Read(RSpath);
                    EditNameHeart(currentStructureSet);
                    EditNameEsophagus(currentStructureSet);

                    currentStructureSet.Write(RSpath);
                }
            }
        }

        /// <summary>
        /// Edits the ID of the heart.
        /// </summary>
        static private void EditNameHeart(DICOMObject currentStructureSet)
        {
            try
            {
                currentStructureSet.FindAll(TagHelper.ROIName).First(r => r.DData.ToString().StartsWith("heart", StringComparison.InvariantCultureIgnoreCase) || r.DData.ToString().StartsWith("hj", StringComparison.InvariantCultureIgnoreCase) && r.DData.ToString().IndexOf("rta", StringComparison.InvariantCultureIgnoreCase) >= 0).DData = "Heart";
            }
            catch
            {

            }
        }

        /// <summary>
        /// Edits the ID of the esophagus.
        /// </summary>
        static private void EditNameEsophagus(DICOMObject currentStructureSet)
        {
            try
            {
                currentStructureSet.FindAll(TagHelper.ROIName).First(r => r.DData.ToString().StartsWith("eso", StringComparison.InvariantCultureIgnoreCase) || r.DData.ToString().StartsWith("eosophagus", StringComparison.InvariantCultureIgnoreCase) || r.DData.ToString().StartsWith("eosopagus", StringComparison.InvariantCultureIgnoreCase)).DData = "Esophagus";
            }
            catch
            {

            }
        }

        /// <summary>
        /// Removes all setup notes to prevent the inclusion of sensitive information that is not handled in the export pseudonymization process.
        /// </summary>
        static private void RemoveSetupNotes(DICOMObject currentPlan)
        {
            currentPlan.Remove(TagHelper.SetupTechniqueDescription);
        }
    }
}
