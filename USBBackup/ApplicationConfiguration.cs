using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace USBBackup
{
    public static class ApplicationConfiguration
    {
        public static string UniqueIdentifyer { get; set; }
        public static List<string> BackupFolders { get; set; }
        public static List<string> Exclusions { get; set; }
    }
}
