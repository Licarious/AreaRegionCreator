using System.Drawing;

namespace AreaRegionCreator
{
    public class Area
    {
        public string name;
        public Color color = Color.FromArgb(0, 255, 255, 255);
        public HashSet<Province> provs = new();
        public HashSet<(int x, int y)> coords = new();
        public bool writeColor = false;

        public Area(string name, Color color, bool writeColor) {
            this.name = name;
            this.color = color;
            this.writeColor = writeColor;
        }

        public Area(string name) {
            this.name = name;
        }

        public Area() { }

        //toString
        public override string ToString() {
            return name + " " + color.ToString();
        }
    }
}
