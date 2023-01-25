namespace AreaRegionCreator
{
    public class Merger
    {
        public string areaName;
        public int provID;
        public int sharedCoords;
        

        public Merger(Area area, Province porv) {
            this.areaName = area.name;
            this.provID = porv.id;

            sharedCoords = area.coords.Intersect(porv.coords).Count();
        }
    }
}
