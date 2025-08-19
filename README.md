RTDataPrepper contains scripts for processes included in an automated workflow for cohort-wise oncology information system (OIS) data preparation for risk modeling (for details: doi.org/10.1002/acm2.70152). The code for data injection is found here: https://github.com/LouiseMovik/RTDataInjector.  

Following data extraction from a clinical OIS (ARIA in our case), do the following in the class InData:
  1. Enter path to the working directory.
  2. Specify requested actions (QC1, Cleanup, QC2 and/or Collection).
  3. Create lists (described in the script).
  4. Search for instances of "case-specific" throughout the entire solution and review the code that might require modifications.
