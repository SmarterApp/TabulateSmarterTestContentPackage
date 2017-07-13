using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TabulateSmarterTestContentPackage.Models
{
    public class ASLVideo
    {
        public ItemContext ItemContext { get; set; }
        public int StimLength { get; set; }
        public long VideoDurationMilliseconds { get; set; }
    }
}
