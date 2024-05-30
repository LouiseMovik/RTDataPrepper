RTDataPrepper contains functions included in the automated workflow for cohort-wise oncology information system (OIS) data preparation for risk modeling. The functions are described in a manuscript currently submitted to a journal. 

Following data extraction from a clinical OIS (ARIA in our case), do the following in the class InData:
  1. Enter path to the working directory.
  2. Specify requested actions (QC1, Cleanup and/or QC2).
  3. Create lists (described in the script).
  4. Search for instances of "case-specific" throughout the entire solution and review the code that might require modifications.
