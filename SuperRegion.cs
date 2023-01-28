using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AreaRegionCreator
{
    public class SuperRegion
    {
        public string name;
        public HashSet<Regions> regions = new();
        public bool restrict_charter = false;

        public SuperRegion(string name) {
            this.name = name;
        }
        public SuperRegion() { }

    }
}
