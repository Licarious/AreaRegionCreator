using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AreaRegionCreator
{
    public class Province
    {
        public Color color;
        public int id;
        public string name;
        public HashSet<(int x, int y)> coords = new();
        public List<HashSet<(int x, int y)>> contigAreas = new();
        public Color suroundingColor;
        public (int x, int y) center = (0, 0);
        public bool finishedGrowth = false;
        public HashSet<(int x, int y)> addedCoords = new();

        public Province(Color color, int id) {
            this.color = color;
            this.id = id;
        }
        public Province(Color color, int id, string name) {
            this.color = color;
            this.id = id;
            this.name = name;
        }
    }
}
