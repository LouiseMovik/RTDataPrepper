using System;
using System.Collections.Generic;
using System.Text;

namespace RTDataPrepper
{
    class StudyRTPlan
    {
        private string pseudoID;
        private string planID;
        private int deliveredFractions;
        private DateTime dateOfFirstFraction;

        public string PseudoID
        {
            get { return pseudoID; }
            set { pseudoID = value; }
        }

        public string PlanID
        {
            get { return planID; }
            set { planID = value; }
        }

        public int DeliveredFractions
        {
            get { return deliveredFractions; }
            set { deliveredFractions = value; }
        }

        public DateTime DateOfFirstFraction
        {
            get { return dateOfFirstFraction; }
            set { dateOfFirstFraction = value; }
        }
    }
}
