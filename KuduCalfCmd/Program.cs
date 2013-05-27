using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;
using SmartSync;
namespace KuduCalfCmd
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                var config = new Config();
                var cmds = new Cmds(config);
                if (cmds.Run(args))
                {
                    return 0;
                }
                else
                {
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error");
                Console.WriteLine(ex.ToString());
                return -1;
            }
        }
    }
}
