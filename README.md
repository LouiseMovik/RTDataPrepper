RTDataPrepper contains scripts for processes included in an automated workflow for cohort-wise oncology information system (OIS) data preparation for risk modeling. The automated workflow is described in a manuscript currently submitted to a journal. The code for the data injection process is found here: https://github.com/LouiseMovik/RTDataInjector.  

Following data extraction from a clinical OIS (ARIA in our case), do the following in the class InData:
  1. Enter path to the working directory.
  2. Specify requested actions (QC1, Cleanup and/or QC2).
  3. Create lists (described in the script).
  4. Search for instances of "case-specific" throughout the entire solution and review the code that might require modifications.
