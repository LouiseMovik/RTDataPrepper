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
                if (inData.performQCofExtraction) QCofExtraction.RunQC1();
                if (inData.performCleanup) Cleanup.RunCleanup();
                if (inData.performQCofCleanupInjection) QCofCleanupInjection.RunQC2();
                if (inData.performCollection) Collection.RunCollection();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                Console.ReadKey();
            }
        }

        
    }
}
