using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Linq.Expressions;
using System.Diagnostics;

namespace RTDataPrepper
{
    internal class PrepManager
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                InData inData = new InData();
                if (inData.performQC1) QC1.RunQC1();
                if (inData.performCleanup) Cleanup.RunCleanup();
                if (inData.performQC2) ; QC2.RunQC2();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                Console.ReadKey();
            }
        }

        
    }
}
