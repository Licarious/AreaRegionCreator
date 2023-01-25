using System.Drawing;

namespace AreaRegionCreator
{
    public class Regions
    {
        public string name;
        //creat a new clear color
        public Color color = Color.FromArgb(0,0,0,0); 
        public List<Area> areas = new();

        public Regions(string name) {
            this.name = name;
        }
        public Regions() { }
    }
}
