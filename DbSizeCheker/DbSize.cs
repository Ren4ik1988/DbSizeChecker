using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbSizeCheker
{
    public class DbSize
    {
        public string DataBaseName { get; set; }
        public decimal DataBaseSize { get; set; }
        public string LastUpdated { get; set; }
    }
}
