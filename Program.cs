using AreaRegionCreator;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Drawing.Drawing2D;

internal class Program
{
    private static void Main() {
        double minimumSharedAreaToCount = 0.05; //prevents a few pixels from filling the entire prov that would otherwise be not counted
        bool debug = true;

        //check if the correct version of .NET is installed
        if (Environment.Version.Major < 6) {
            Console.WriteLine("You need .NET 6.0 or higher to run this program");
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
            return;
        }
        
        string localDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
        //stopwach
        Stopwatch stopwatch = new();
        stopwatch.Start();

        
        //true draws area_region.png and writes area_region_definition.csv
        //false takes area_region.png and area_region_definition.csv from Input folder and matches them to provinces
        bool convertPNGToIDs = true;
        string game = "EU4"; //EU4 or IR
        string modName = "test"; //name of the output files
        string areaSorting = "alphabetical"; //alphabetical sort the area names in the output file, group groups them by region, "" keeps the order from area_region_definition.csv
        ParseConfig();

        string[] eu4Files = { "provinces.bmp", "definition.csv", "area.txt", "region.txt", "superregion.txt" };
        string[] irFiles = { "provinces.png", "definition.csv", "areas.txt", "regions.txt"};
        string[] files = game == "EU4" ? eu4Files : irFiles;
        string[] areaRegionFiles = { "area_region.png", "area_region_definition.csv" };


        //check if the first 2 entries in the files array exist in localDir\Input\area_region\
        if (!File.Exists(localDir + @"\Input\map_data\" + files[0]) || !File.Exists(localDir + @"\Input\map_data\" + files[1])) {
            Console.WriteLine("provinces image or definition.csv could not found\nPlease add them");
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
            return;

        }        
        //check if both areaRegionFiles exist in localDir\Input\area_region\
        else if (!File.Exists(localDir + @"\Input\area_region\" + areaRegionFiles[0]) || !File.Exists(localDir + @"\Input\area_region\" + areaRegionFiles[1])) {
            Console.WriteLine("area_region.png or area_region_definition.csv could not found\nGenerating them");
            convertPNGToIDs = false;

            //check if the last 2-3 entries in the files array exist in localDir\Input\map_data\
            if (!File.Exists(localDir + @"\Input\map_data\" + files[2]) || !File.Exists(localDir + @"\Input\map_data\" + files[3]) || (game == "EU4" && !File.Exists(localDir + @"\Input\map_data\" + files[4]))) {
                Console.WriteLine("area.txt, region.txt or superregion.txt could not found\nPlease add them");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                return;
            }
        }

        

        Dictionary <Color, Province> provDict = ParseDefinitions();
        (Dictionary<string, Regions> regions, Dictionary<string, Area> areas, Dictionary<string, SuperRegion> superRegions) = ParseAreaRegion(provDict);        
        
        ParseProvMap(provDict);

        if (convertPNGToIDs) {
            ParseAreaRegionMap(areas);
            MapProvincesToAreas(provDict, areas);
            WriteAreas(areas, regions, superRegions);
            Localize(provDict, areas, regions);
        }
        WriteAreaRegionDefinitions(regions, superRegions);
        DrawAreas(areas);

        if (debug) {
            DebugDrawRegions(regions);
        }

        stopwatch.Stop();
        Console.WriteLine("Time elapsed: {0}", stopwatch.Elapsed);

        //parse areaName and regions
        (Dictionary<string, Regions> regions, Dictionary<string, Area> areas, Dictionary<string, SuperRegion> superRegion) ParseAreaRegion(Dictionary<Color, Province> provDict) {
            Console.WriteLine("Parsing Areas and Regions...");

            //Dictionary for regions
            Dictionary<string, Regions> regions = new();
            //Dictionary for areas
            Dictionary<string, Area> areas = new();
            //Dictionary of super regions
            Dictionary<string, SuperRegion> superRegions = new();

            if (!convertPNGToIDs) {
                //create a blank string aray
                string[] areaLines = Array.Empty<string>();
                if (game == "EU4") {
                    //get all files in the area folder
                    areaLines = File.ReadAllLines(localDir + @"\Input\map_data\area.txt");
                }
                else if (game == "IR") {
                    //get all files in the area folder
                    areaLines = File.ReadAllLines(localDir + @"\Input\map_data\areas.txt");
                }

                //dictionary with key prov.id and value of prov
                Dictionary<int, Province> provDictID = new();
                foreach (Province prov in provDict.Values) {
                    provDictID.Add(prov.id, prov);
                }

                HashSet<Color> usedAreaColors = new();

                int indentation = 0;
                bool provFound = false;
                bool colorFound = false;
                Area currentArea = new();
                foreach (string line in areaLines) {
                    string l1 = CleanLine(line);
                    if (l1 == "") continue;


                    if (indentation == 0) {
                        if (l1.Contains('=')) {
                            string name = l1.Split("=")[0].Trim();

                            currentArea = new Area(name);
                            areas.Add(name, currentArea);

                            if (game == "EU4") {
                                provFound = true;
                            }
                        }
                        //lets stop that fuck up from happing again
                        else if (l1.Contains('{')) {
                            string name = l1.Split("{")[0].Trim();

                            currentArea = new Area(name);
                            areas.Add(name, currentArea);

                            Console.WriteLine("Error in areas.txt: {0} is missing = sign", name);
                        }
                    }
                    else if(indentation == 1) {
                        if (l1.StartsWith("provinces")) provFound = true;
                        else if (l1.StartsWith("color")) {
                            colorFound = true;
                            if (game == "EU4") provFound = false;
                        }
                    }

                    if (provFound) {
                        string[] l2 = l1.Split();
                        foreach(string w in l2) {
                            //try pars w into int the find prov.id the same as int
                            if (int.TryParse(w, out int provId)) {
                                if (provDictID.ContainsKey(provId)) {
                                    currentArea.provs.Add(provDictID[provId]);
                                }
                            }
                        }
                    }

                    if (colorFound) {
                        currentArea.color = GetColor(l1);
                        currentArea.writeColor = true;
                        usedAreaColors.Add(currentArea.color);
                    }

                    //change indentation
                    if (l1.Contains('{') || l1.Contains('}')) {
                        string[] l2 = l1.Split();
                        foreach(string w in l2) {
                            if (w.Contains('{')) indentation++;
                            if (w.Contains('}')) {
                                indentation--;
                                provFound = false;
                                colorFound = false;
                                if (game == "EU4") provFound = true;
                            }
                        }
                    }
                }

                //parse regions
                string[] regionLines = Array.Empty<string>();
                if (game == "EU4") {
                    //get all files in the region folder
                    regionLines = File.ReadAllLines(localDir + @"\Input\map_data\region.txt");
                }
                else if (game == "IR") {
                    //get all files in the region folder
                    regionLines = File.ReadAllLines(localDir + @"\Input\map_data\regions.txt");
                }

                indentation = 0;
                bool areaFound = false;
                colorFound = false;
                bool monsoonFound = false;
                (string start, string end) monsoon = ("", "");
                Regions currentRegion = new();
                foreach (string line in regionLines) {
                    string l1 = CleanLine(line);
                    if (l1 == "") continue;


                    if (indentation == 0) {
                        if (l1.Contains('=')) {
                            string name = l1.Split("=")[0].Trim();

                            currentRegion = new Regions(name);
                            regions.Add(name, currentRegion);
                        }
                        else if (l1.Contains('{')) {
                            string name = l1.Split("{")[0].Trim();

                            currentRegion = new Regions(name);
                            regions.Add(name, currentRegion);

                            Console.WriteLine("Error in regions.txt: {0} is missing = sign", name);
                        }
                    }
                    else if (indentation == 1) {
                        if (l1.StartsWith("areas")) areaFound = true;
                        else if (l1.StartsWith("color")) colorFound = true;
                        else if (l1.StartsWith("monsoon")) monsoonFound = true;
                    }

                    if (areaFound) {
                        string[] l2 = l1.Split();
                        foreach (string w in l2) {
                            if (areas.ContainsKey(w)) {
                                currentRegion.areas.Add(areas[w]);
                            }
                        }
                    }

                    if (colorFound) {
                        currentRegion.color = GetColor(l1);
                    }

                    if (monsoonFound) {
                        if (l1.Contains("00.")) {
                            //set l1 to first empty space in monsoon
                            if (monsoon.start == "") {
                                monsoon.start = l1.Split()[0];
                            }
                            else if (monsoon.end == "") {
                                monsoon.end = l1.Split()[0];
                                currentRegion.monsoonList.Add(monsoon);
                                monsoon = ("", "");
                            }
                        }

                    }

                    //change indentation
                    if (l1.Contains('{') || l1.Contains('}')) {
                        string[] l2 = l1.Split();
                        foreach (string w in l2) {
                            if (w.Contains('{')) indentation++;
                            if (w.Contains('}')) {
                                indentation--;
                                areaFound = false;
                                colorFound = false;
                                monsoonFound = false;
                            }
                        }
                    }
                }


                //for each area that does not have a color generate a random color not in usedAreaColors and give it to the area
                Random rnd = new();
                foreach (Area area in areas.Values) {
                    if (area.color.A == 0) {
                        Color color = StringToColor(area.name);
                        while (usedAreaColors.Contains(color)){
                            Console.WriteLine(area.name + " color {0} is already used, generating new color", color);
                            color = Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256));
                        }
                        area.color = color;
                    }
                }

                if(game == "EU4") {
                    //parse super regions
                    indentation = 0;
                    SuperRegion currentSuperRegion = new();
                    string[] superRegionLines = File.ReadAllLines(localDir + @"\Input\map_data\superregion.txt");
                    foreach(string line in superRegionLines) {
                        string l1 = CleanLine(line);
                        if (l1 == "") continue;


                        if (indentation == 0) {
                            if (line.Contains('=')) {
                                string name = l1.Split("=")[0].Trim();

                                currentSuperRegion = new SuperRegion(name);
                                superRegions.Add(name, currentSuperRegion);
                            }
                            else if (l1.Contains('{')) {
                                string name = l1.Split("{")[0].Trim();

                                currentSuperRegion = new SuperRegion(name);
                                superRegions.Add(name, currentSuperRegion);

                                Console.WriteLine("Error in superregions.txt: {0} is missing = sign", name);
                            }
                            
                        }
                        else if (indentation == 1) {
                            if (l1.Contains("restrict_charter")){
                                Console.WriteLine(currentSuperRegion.name);
                                currentSuperRegion.restrict_charter = true;
                            }
                            else {
                                //if line is a region name add the region to the super region
                                if (regions.ContainsKey(l1)) {
                                    currentSuperRegion.regions.Add(regions[l1]);
                                }
                            }
                        }


                        //update indentation
                        if (l1.Contains('{') || l1.Contains('}')) {
                            string[] l2 = l1.Split();
                            foreach (string w in l2) {
                                if (w.Contains('{')) indentation++;
                                if (w.Contains('}')) indentation--;
                            }
                        }
                    }
                }

            }
            else {
                //areaRegionDefinitions.csv read
                string[] areaRegionDefinitions = File.ReadAllLines(localDir + @"\Input\area_region\area_region_definition.csv");

                foreach (string line in areaRegionDefinitions) {
                    string l1 = line.Trim();
                    if (l1.StartsWith("#") || l1 == "") {
                        continue;
                    }

                    string[] split = l1.Split(';');
                    //color R G B are the first 3 values
                    Color color = Color.FromArgb(int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]));
                    //areaName name is the 4th value
                    string areaName = split[3];
                    //region name is the 5th value
                    string regionName = split[4];
                    //if there is a 6th value try to parse it to a bool
                    bool writeColor = false;
                    if (split.Length > 5) {
                        writeColor = bool.Parse(split[5]);
                    }
                    bool setRegionColor = false;
                    Color regionColor = new();
                    List<(string start, string end)> mounsuneList = new();
                    string superRegionName = "";
                    bool setRestrict = false;

                    if (game == "IR") {
                        //region color is the 7th - 9th values                        
                        if (split.Length >= 8) {
                            regionColor = Color.FromArgb(255, int.Parse(split[6]), int.Parse(split[7]), int.Parse(split[8]));
                            setRegionColor = true;
                        }
                    }
                    else if (game == "EU4") {
                        //super region is the 7th value
                        superRegionName = split[6];
                        setRestrict = bool.Parse(split[7]);
                        //7th value is a list of monsunes tuples (start, end) seperate by a space
                        if (split.Length > 9) {
                            string[] mounsuneSplit = split[8].Split();
                            foreach (string mounsune in mounsuneSplit) {
                                string[] mounsuneSplit2 = mounsune.Replace("(","").Replace(")","").Split('-');
                                mounsuneList.Add((mounsuneSplit2[0], mounsuneSplit2[1]));
                            }
                        }
                    }


                    //create areaName if it doesn't exist
                    Area tmpArea = new(areaName, color, writeColor);
                    if (!areas.ContainsKey(areaName)) {
                        areas.Add(areaName, tmpArea);
                    }
                    else {
                        //print error if areaName already exists and continue
                        Console.WriteLine("Area " + areaName + " already exists");
                        continue;
                    }

                    Regions tmpRegion = new();
                    //if the region does not exist, create it
                    if (!regions.ContainsKey(regionName)) {
                        tmpRegion = new(regionName);
                        regions.Add(regionName, tmpRegion);
                    }
                    else {
                        tmpRegion = regions[regionName];
                    }
                    tmpRegion.areas.Add(tmpArea);
                    if (setRegionColor) {
                        tmpRegion.color = regionColor;
                    }
                    if (game == "EU4") {
                        if (mounsuneList.Count > 0) {
                            tmpRegion.monsoonList = mounsuneList;
                        }

                        //remove region from all super regions to prevent duplicates
                        foreach (SuperRegion superRegion in superRegions.Values) {
                            superRegion.regions.Remove(tmpRegion);
                        }

                        SuperRegion tmpSuperRegion = new();
                        if (!superRegions.ContainsKey(superRegionName)) {
                            tmpSuperRegion = new(superRegionName) {
                                restrict_charter = setRestrict
                            };
                            superRegions.Add(superRegionName, tmpSuperRegion);
                        }
                        else {
                            tmpSuperRegion = superRegions[superRegionName];
                        }
                        tmpSuperRegion.regions.Add(tmpRegion);
                    }

                    
                }
            }

            return (regions, areas, superRegions);
        }

        string CleanLine(string line) {
            return line.Replace("{", " { ").Replace("}", " } ").Replace("=", " = ").Replace("  ", " ").Split('#')[0].Trim();
        }

        Color GetColor(string line) {
            //split on space and add the 3 values to the color list
            string[] l2 = line.Split(' ');
            List<double> colorList = new();
            //if l2 is a doubel then add it to colorList
            foreach (string s in l2) {
                if (double.TryParse(s, out double d)) {
                    colorList.Add(d);
                }
            }
            if (line.Contains("hsv360")) {
                //while there is less than 3 values in colorList add 0
                while (colorList.Count < 3) {
                    colorList.Add(0);
                }
                //convert the colorList from hsv360 to rgb
                return ColorFromHSV360(colorList[0], colorList[1], colorList[2]);
            }
            //if hsv is in the line then convert the colorList to rgb
            else if (line.Contains("hsv")) {
                //while there is less than 3 values in colorList add 0.5
                while (colorList.Count < 3) {
                    colorList.Add(0.5);
                }
                //convert the colorList from hsv to rgb
                return ColorFromHSV(colorList[0], colorList[1], colorList[2]);
            }

            //rgb values
            return ColorRGB(colorList);
        }

        Color ColorFromHSV(double v1, double v2, double v3) {
            //convert hsv to rgb
            double r, g, b;
            if (v3 == 0) {
                r = g = b = 0;
            }
            else {
                if (v2 == -1) v2 = 1;
                int i = (int)Math.Floor(v1 * 6);
                double f = v1 * 6 - i;
                double p = v3 * (1 - v2);
                double q = v3 * (1 - f * v2);
                double t = v3 * (1 - (1 - f) * v2);
                switch (i % 6) {
                    case 0: r = v3; g = t; b = p; break;
                    case 1: r = q; g = v3; b = p; break;
                    case 2: r = p; g = v3; b = t; break;
                    case 3: r = p; g = q; b = v3; break;
                    case 4: r = t; g = p; b = v3; break;
                    case 5: r = v3; g = p; b = q; break;
                    default: r = g = b = v3; break;
                }
            }
            List<double> colorList = new() { r, g, b };

            return ColorRGB(colorList);
        }
        Color ColorFromHSV360(double v1, double v2, double v3) {
            //converts hsv360 to rgb
            return ColorFromHSV(v1 / 360, v2 / 100, v3 / 100);
        }
        Color ColorRGB(List<double> colorList) {
            //if all doubles in colorList are between 0 and 1 then convert them to 0-255
            bool allBetween0and1 = true;
            foreach (double d in colorList) {
                if (d < 0 || d > 1) {
                    allBetween0and1 = false;
                    break;
                }
            }
            if (allBetween0and1) {
                for (int i = 0; i < colorList.Count; i++) {
                    colorList[i] = colorList[i] * 255;
                }
            }
            //while there is less than 3 values in colorList add 128
            while (colorList.Count < 3) {
                colorList.Add(128);
            }
            //if the values are outside of the 0-255 range then set them to 0 or 255
            for (int i = 0; i < colorList.Count; i++) {
                if (colorList[i] < 0) {
                    colorList[i] = 0;
                }
                else if (colorList[i] > 255) {
                    colorList[i] = 255;
                }
            }
            return Color.FromArgb((int)(colorList[0]), (int)(colorList[1]), (int)(colorList[2]));
        }

        //parse Provs return a dictionary of provinces
        Dictionary<Color, Province> ParseDefinitions() {
            Console.WriteLine("Parsing definitions...");
            Dictionary<Color, Province> provDict = new();

            //string[] lines = File.ReadAllLines(localDir + @"\_Input\FillColors.txt");
            string[] lines = File.ReadAllLines(localDir + @"\Input\map_data\definition.csv");
            foreach (string line in lines) {                
                string[] parts = line.Trim().Split(";");
                
                if (parts.Length < 4) continue;
                else if (parts[1].Contains('#')) continue;
                
                //try parse the id
                if (int.TryParse(parts[0], out int id)) {
                    if (id == 0) continue; //games do not use id 0
                    int r = int.Parse(parts[1]);
                    int g = int.Parse(parts[2]);
                    int b = int.Parse(parts[3]);
                    string name = parts[4];

                    //if key does not exist, add it
                    if (!provDict.ContainsKey(Color.FromArgb(r, g, b))) {
                        provDict.Add(Color.FromArgb(r, g, b), new Province(Color.FromArgb(r, g, b), id, name));
                    }
                }
            }
            
            return provDict;
        }

        //parse prov map
        void ParseProvMap(Dictionary<Color, Province> provDict) {
            Console.WriteLine("Parsing prov map...");
            
            //read the map
            Bitmap map = new(1, 1);
            if (game == "EU4") {
                map = new Bitmap(localDir + @"\Input\map_data\provinces.bmp");
            }
            else if (game == "IR") {
                map = new(localDir + @"\Input\map_data\provinces.png");
            }
            
            //loop through the map
            for (int x = 0; x < map.Width; x++) {
                for (int y = 0; y < map.Height; y++) {
                    //get the color of the pixel
                    Color c = map.GetPixel(x, y);
                    //if the color is in the dictionary
                    if (provDict.ContainsKey(c)) {
                        //add coord to the list of provinces
                        provDict[c].coords.Add((x, y));
                    }
                }
                //print progress every 20%
                if (x % (map.Width / 5) == 0) {
                    Console.WriteLine("\t" + (x / (map.Width / 100)) + "%\t" + stopwatch.Elapsed + "s");
                }
            }
        }

        //parse area region map
        void ParseAreaRegionMap(Dictionary<string, Area> areas) {
            Console.WriteLine("Parsing area map...");

            //setup areaColorDict with the color of each areaName and the areaName
            Dictionary<Color, Area> areaColorDict = new();
            foreach (KeyValuePair<string, Area> area in areas) {
                areaColorDict.Add(area.Value.color, area.Value);
            }


            //read the map
            Bitmap map = new(localDir + @"\Input\area_region\area_region.png");

            //loop through the map
            for (int x = 0; x < map.Width; x++) {
                for (int y = 0; y < map.Height; y++) {
                    //get the color of the pixel
                    Color c = map.GetPixel(x, y);
                    //if the color is in the dictionary
                    if (areaColorDict.ContainsKey(c)) {
                        //add coord to the list of provinces
                        areaColorDict[c].coords.Add((x, y));
                    }
                }
                //print progress every 20%
                if (x % (map.Width / 5) == 0) {
                    Console.WriteLine("\t" + (x / (map.Width / 100)) + "%\t" + stopwatch.Elapsed + "s");
                }
            }
        }

        

        //map provinces to areas
        void MapProvincesToAreas(Dictionary<Color, Province> provDict, Dictionary<string, Area> areas) {
            Console.WriteLine("Mapping provinces to areas...");

            List<Merger> mergerList = new();
            int count = 0;

            //parallel for loop throu provinces 
            Parallel.ForEach(provDict, prov => {
                //loop through areas
                foreach (KeyValuePair<string, Area> area in areas) {
                    //if the province is in the area
                    if (prov.Value.coords.Intersect(area.Value.coords).Any()) {

                        //if the shared coords are more than minimumSharedAreaToCount of the province
                        if (prov.Value.coords.Intersect(area.Value.coords).Count() > prov.Value.coords.Count * minimumSharedAreaToCount) {
                            //add the merger to the merger list
                            mergerList.Add(new Merger(area.Value, prov.Value));
                        }
                        else {
                            if (debug)
                                Console.WriteLine(prov.Value.id + " - " + prov.Value.name + " is in area " + area.Value.name + " but not enough to merge");
                            continue;
                        }

                        //if the shared coords is >50% of prov coords then majority of the province is in the area and we can break
                        if (prov.Value.coords.Intersect(area.Value.coords).Count() > (prov.Value.coords.Count / 2)) {
                            break;
                        }
                    }
                }
                count++;
                //print progress every 5% and print every 1% after 95%
                if ((count % (provDict.Count / 20) == 0) || (count % (provDict.Count / 100) == 0 && count > (provDict.Count / 100) * 95)) {
                    Console.WriteLine("\t" + (count / (provDict.Count / 100)) + "%\t" + stopwatch.Elapsed + "s");
                }

            });

            Console.WriteLine("Grouping provs to areas");
            
            //create a dictionry of provID and prov
            Dictionary<int, Province> provIDDict = new();
            foreach (KeyValuePair<Color, Province> prov in provDict) {
                provIDDict.Add(prov.Value.id, prov.Value);
            }

           
            //group merger by provIDs
            var groupedMerger = mergerList.GroupBy(x => x.provID);

            //find the merger with the highest sharedCoords for each group
            foreach (var group in groupedMerger) {
                //get the merger with the highest sharedCoords
                Merger tmpMerger = group.OrderByDescending(x => x.sharedCoords).First();

                Province p = provIDDict[tmpMerger.provID];
                Area a = areas[tmpMerger.areaName];

                a.provs.Add(p);
                p.name = CapitalizeString(a.name, "area");
            }

        }

        //write Areas and Regions to file
        void WriteAreas(Dictionary<string, Area> areas, Dictionary<string, Regions> regions, Dictionary<string, SuperRegion> superRegions) {
            Console.WriteLine("Writing areas and regions to file...");

            //check if output folder exists
            if (!Directory.Exists(localDir + @"\Output\"+modName)) {
                Directory.CreateDirectory(localDir + @"\Output\" + modName);
            }

            //create a list of areas
            List<Area> areaList = new();
            foreach (KeyValuePair<string, Area> area in areas) {
                areaList.Add(area.Value);
            }
            if (areaSorting == "alphabetical") {
                //sort the list by areaNameNamearea
                areaList.Sort((x, y) => x.name.CompareTo(y.name));
            }
            else if (areaSorting == "group") {
                //clear the list
                areaList.Clear();
                foreach (KeyValuePair<string, Regions> region in regions) {
                    //add each area in the region to the list
                    foreach (Area area in region.Value.areas) {
                        areaList.Add(area);
                    }
                }
            }

            //write the areas to file
            string fName = "areas.txt";
            if (game == "EU4") fName = "area.txt";
            using (StreamWriter sw = new(localDir + @"\Output\" + modName + @"\"+fName)) {
                if (game == "EU4") sw.WriteLine("random_new_world_region = {\n}");
                string lastRegion = "";
                foreach (Area area in areaList) {
                    if (areaSorting == "group") {
                        //find the region the area is in
                        foreach (KeyValuePair<string, Regions> region in regions) {
                            if (region.Value.areas.Contains(area)) {
                                //if the region is different from the last region
                                if (region.Key != lastRegion) {
                                    //write the region
                                    sw.WriteLine("\n#" + region.Key);
                                    lastRegion = region.Key;
                                }
                                break;
                            }
                        }
                    }
                    sw.WriteLine(area.name + " = { #" + area.provs.Count);
                    if (area.writeColor) {
                        if(game == "IR")
                            sw.WriteLine("\tcolor = rgb { " + area.color.R + " " + area.color.G + " " + area.color.B + " }");
                        else if (game == "EU4")
                            sw.WriteLine("\tcolor = { " + area.color.R + " " + area.color.G + " " + area.color.B + " }");
                    }
                    //add all area.prov.id to list
                    List<int> provIDList = new();
                    foreach (Province prov in area.provs) {
                        provIDList.Add(prov.id);
                    }
                    //sort the list
                    provIDList.Sort();

                    //write provIDList join on space
                    if (game == "IR") {
                        sw.WriteLine("\tprovinces = { " + string.Join(" ", provIDList) + " }");
                    }
                    else if(game == "EU4") {
                        sw.WriteLine("\t" + string.Join(" ", provIDList));
                    }
                    sw.WriteLine("}\n");
                }
            }

            //create a list of regions
            List<Regions> regionList = new();
            foreach (KeyValuePair<string, Regions> region in regions) {
                regionList.Add(region.Value);
            }

            //sort the list by regionName
            regionList.Sort((x, y) => x.name.CompareTo(y.name));

            //write the regions to file
            fName = "regions.txt";
            if (game == "EU4") fName = "region.txt";
            using (StreamWriter sw = new(localDir + @"\Output\" + modName + @"\"+ fName)) {
                foreach (Regions region in regionList) {
                    //sort region.areas by area.name
                    if (region.areas.Count > 1) {
                        region.areas.Sort((x, y) => x.name.CompareTo(y.name));
                    }
                    
                    sw.WriteLine(region.name + " = { #" + region.areas.Count + " areas");
                    if (region.color.A != 0) {
                        sw.WriteLine("\tcolor = rgb { " + region.color.R + " " + region.color.G + " " + region.color.B + " }");
                    }
                    sw.WriteLine("\tareas = {");
                    foreach (Area area in region.areas) {
                        sw.WriteLine("\t\t" + area.name);
                    }
                    sw.WriteLine("\t}");
                    if(region.monsoonList.Count > 0) {
                        foreach ((string start, string end) in region.monsoonList) {
                            sw.WriteLine("\tmonsoon = {");
                            sw.WriteLine("\t\t" + start);
                            sw.WriteLine("\t\t" + end);
                            sw.WriteLine("\t}");
                        }
                    }


                    sw.WriteLine("}\n");
                }
            }

            if (game == "EU4") {
                //create a list of superRegions
                List<SuperRegion> superRegionList = new();
                foreach (KeyValuePair<string, SuperRegion> superRegion in superRegions) {
                    superRegionList.Add(superRegion.Value);
                }

                //sort the list by superRegionName
                superRegionList.Sort((x, y) => x.name.CompareTo(y.name));

                //write the superRegions to file
                fName = "superregion.txt";
                using StreamWriter sw = new(localDir + @"\Output\" + modName + @"\" + fName);
                sw.WriteLine("new_world_superregion = {\n}");
                foreach (SuperRegion superRegion in superRegionList) {
                    sw.WriteLine(superRegion.name + " = { #" + superRegion.regions.Count + " regions");

                    if (superRegion.restrict_charter) {
                        sw.WriteLine("\n\trestrict_charter\t\t# harder to get TC here.\n");
                    }

                    foreach (Regions region in superRegion.regions) {
                        sw.WriteLine("\t" + region.name);
                    }
                    sw.WriteLine("}\n");
                }
            }
        }

        //localize provs
        void Localize(Dictionary<Color, Province> provDict, Dictionary<string, Area> areas, Dictionary<string, Regions> regions) {
            Console.WriteLine("Localizing provinces...");

            //check if output\english folder exists
            if (!Directory.Exists(localDir + @"\Output\" + modName + @"\english")) {
                Directory.CreateDirectory(localDir + @"\Output\" + modName + @"\english");
            }

            //create a list of provinces
            List<Province> provList = new();
            foreach (KeyValuePair<Color, Province> prov in provDict) {
                provList.Add(prov.Value);
            }

            //sort the list by provID
            provList.Sort((x, y) => x.id.CompareTo(y.id));

            //write the provinces to file
            using (StreamWriter sw = new(localDir + @"\Output\" + modName + @"\english\03_provinces_l_english.yml")) {
                sw.WriteLine("l_english:");
                foreach (Province prov in provList) {
                    sw.WriteLine(" PROV" + prov.id + ":0 \"" + prov.name + "\"");
                }
            }

            //write regions and areas to localization
            using (StreamWriter sw = new(localDir + @"\Output\" + modName + @"\english\03_areas_regions_l_english.yml")) {
                sw.WriteLine("l_english:");
                if (game == "EU4") {
                    sw.WriteLine(" #SUPERREGIONS");
                    sw.WriteLine(" NEW_WORLD_SUPERREGION:0 \"New World Superregion\"");
                    
                    foreach (KeyValuePair<string, SuperRegion> superRegion in superRegions) {
                        sw.WriteLine(" " + superRegion.Key + ":0 \"" + superRegion.Value.name + "\"");
                    }
                    sw.WriteLine("\n");
                }

                sw.WriteLine(" #REGIONS");
                foreach (KeyValuePair<string, Regions> region in regions) {
                    sw.WriteLine(" " + region.Value.name + ":0 \"" + CapitalizeString(region.Value.name, "region") + "\"");
                }
                sw.WriteLine("\n");
                sw.WriteLine(" #AREAS");
                foreach (KeyValuePair<string, Area> area in areas) {
                    sw.WriteLine(" " + area.Value.name + ":0 \"" + CapitalizeString(area.Value.name, "area") + "\"");
                }
            }
        }
        void WriteAreaRegionDefinitions(Dictionary<string, Regions> regions, Dictionary<string, SuperRegion> superRegions) {
            Console.WriteLine("Writing area_region_deff.txt...");

            //check if output folder exists
            if (!Directory.Exists(localDir + @"\Output\"+ modName + @"\area_region")) {
                Directory.CreateDirectory(localDir + @"\Output\" + modName + @"\area_region");
            }
            
            HashSet<Color> usedColors = new();
            Random rnd = new();

            //write areaRegionDefinitions.csv
            using StreamWriter sw = new(localDir + @"\Output\"+ modName + @"\area_region\area_region_definition.csv");
            if (game == "IR") {
                sw.WriteLine("#Red;Green;Blue;AreaName;RegionName;SaveColorForArea;RegionRed(optional);RegionGreen(optional);RegionBlue(optional)");
                foreach (KeyValuePair<string, Regions> region in regions) {
                    sw.WriteLine("#" + region.Key);
                    foreach (Area area in region.Value.areas) {
                        sw.Write(area.color.R + ";");
                        sw.Write(area.color.G + ";");
                        sw.Write(area.color.B + ";");
                        sw.Write(area.name + ";");
                        sw.Write(region.Key + ";");
                        sw.Write(area.writeColor + ";");
                        if (region.Value.color.A != 0) {
                            sw.Write(region.Value.color.R + ";");
                            sw.Write(region.Value.color.G + ";");
                            sw.Write(region.Value.color.B + ";");
                        }
                        sw.WriteLine();
                        usedColors.Add(area.color);
                    }
                }
            }
            else if (game == "EU4") {
                sw.WriteLine("#Red;Green;Blue;AreaName;RegionName;SaveColorForArea;SuperRegionName;restrict_charter;Monsoons(optional)");
                foreach (KeyValuePair<string, SuperRegion> superRegion in superRegions) {
                    sw.WriteLine("#" + superRegion.Key);
                    foreach (Regions region in superRegion.Value.regions) {
                        sw.WriteLine("\t#" + region.name);
                        foreach (Area area in region.areas) {
                            sw.Write("\t");
                            sw.Write(area.color.R + ";");
                            sw.Write(area.color.G + ";");
                            sw.Write(area.color.B + ";");
                            sw.Write(area.name + ";");
                            sw.Write(region.name + ";");
                            sw.Write(area.writeColor + ";");
                            sw.Write(superRegion.Key + ";");
                            sw.Write(superRegion.Value.restrict_charter + ";");
                            if (region.monsoonList.Count > 0) {
                                string m = "";
                                foreach ((string start, string end) in region.monsoonList) {
                                    m+="(" + start + "-" + end + ") ";
                                }
                                sw.Write(m.Trim() + ";");
                            }
                            sw.WriteLine();
                            usedColors.Add(area.color);
                        }
                        sw.WriteLine();
                    }
                    sw.WriteLine();
                }
            }
            sw.WriteLine();
            
            //generate 100 random colors not in usedColors
            sw.WriteLine("#Unused Areas");
            int count = 0;
            Color color = new();
            while (count < 100) {
                do {
                    color = Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256));
                } while (usedColors.Contains(color));
                sw.Write("#" + color.R + ";" + color.G + ";" + color.B + ";unused_area_" + count + ";usused_region;False");
                if (game == "EU4") {
                    sw.Write(";unused_super_region;False;");
                }
                sw.WriteLine();

                count++;

            }
            sw.Close();
        }

        void DrawAreas(Dictionary<string, Area> areas, string sufex = "") {
            Console.WriteLine("Drawing area_region.png...");

            //check if output folder exists
            if (!Directory.Exists(localDir + @"\Output\"+modName+@"\area_region")) {
                Directory.CreateDirectory(localDir + @"\Output\"+modName+@"\area_region");
            }

            Bitmap map = new(1, 1);
            if (game == "EU4") {
                map = new Bitmap(localDir + @"\Input\map_data\provinces.bmp");
            }
            else if (game == "IR") {
                map = new(localDir + @"\Input\map_data\provinces.png");
            }

            //create new image file the same size as provinces.png
            Bitmap areaMap = new(map.Width, map.Height);

            //iterate throu each value in areas
            foreach(KeyValuePair<string, Area> area in areas) {
                //Console.WriteLine(area.Value);
                foreach(Province prov in area.Value.provs) {
                    foreach((int x, int y) in prov.coords) {
                        areaMap.SetPixel(x, y, area.Value.color);
                    }
                }
            }

            //save image
            areaMap.Save(localDir + @"\Output\"+ modName + @"\area_region\area_region" + sufex + ".png");

        }

        void DebugDrawRegions(Dictionary<string, Regions> regions) {
            //if Output\debug folder does not exist, create it
            if (!Directory.Exists(localDir + @"\Output\" + modName + @"\debug")) {
                Directory.CreateDirectory(localDir + @"\Output\"+ modName + @"\debug");
            }

            //create new image file the same size as provinces.png
            Bitmap regionMap = new(localDir + @"\Input\map_data\provinces.png");
            regionMap = new Bitmap(regionMap.Width, regionMap.Height);

            //draw each region
            foreach (KeyValuePair<string, Regions> region in regions) {
                //set color of region to first area in region if alpha is 0
                if (region.Value.color.A == 0 && region.Value.areas.Count > 0) {
                    region.Value.color = region.Value.areas[0].color;
                }
                foreach (Area area in region.Value.areas) {                   
                    foreach (Province prov in area.provs) {
                        region.Value.coords.UnionWith(prov.coords);
                        area.coords.UnionWith(prov.coords);
                        foreach ((int x, int y) in prov.coords) {
                            regionMap.SetPixel(x, y, region.Value.color);
                        }
                    }
                }
            }
            
            //save image
            regionMap.Save(localDir + @"\Output\"+ modName + @"\debug\Region.png");

            WriteNamesRegion(regions);
            WriteNamesArea(regions);
        }

        void WriteNamesRegion(Dictionary<string, Regions> regions) {
            //load merged image from localDir + "\\Output\\" + modName+ "\\" + holdingLevel + modName + ".png"
            Bitmap? newBmp = new(localDir + @"\Output\"+ modName + @"\debug\Region.png");

            MaximumRectangle mr = new();

            Graphics g = Graphics.FromImage(newBmp);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            StringFormat stringFormat = new() {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            float sizeMult = 1.0f;

            int minFontSize = 7;
            string font = "Verdana";

            //for each region in regions dictionray
            foreach (KeyValuePair<string, Regions> region in regions) {
                Regions r = region.Value;

                //skip empty regions
                if (r.coords.Count == 0) continue;

                //print progress
                Console.WriteLine("\t" + r.name + " " + r.areas.Count);

                //find largest rectangle in region
                (int x, int y) center = (0, 0);
                (int h, int w) size = (0, 0);

                bool floodFill = true;
                //if any area name in the region contains mare, mara, oceanus, sinus, freti, or river
                foreach (Area area in r.areas) {
                    if (area.name.ToLower().Contains("mare") || area.name.ToLower().Contains("mara") || area.name.ToLower().Contains("oceanus") || area.name.ToLower().Contains("sinus") || area.name.ToLower().Contains("freti") || area.name.ToLower().Contains("river")) {
                        floodFill = false;
                        break;
                    }
                }


                (center, size) = mr.center(r.coords.ToList(), floodFill);

                string[] words = r.name.Replace("region", "").Replace("_", " ").Trim().Split();
                //capitilize first leter of each word
                for (int i = 0; i < words.Length; i++) {
                    words[i] = string.Concat(words[i][..1].ToUpper(), words[i].AsSpan(1));
                }


                //get all but first element of words and join them with " "
                string name = string.Join(" ", words).Trim();


                Font font1 = new(font, minFontSize);

                //calculate the size of the string
                SizeF stringSize = g.MeasureString(name, font1);

                //increase the size of the string until it fills the holding
                while (stringSize.Width < (size.h * sizeMult) && stringSize.Height < (size.w * sizeMult)) {
                    font1 = new Font(font, font1.Size + 1);
                    stringSize = g.MeasureString(name, font1);
                }

                if (name.Split().Length > 1) {
                    string name2 = string.Join("\n", words).Trim();
                    Font font2 = new(font, minFontSize);
                    SizeF stringSize2 = g.MeasureString(name2, font2);

                    while (stringSize2.Width < (size.h * sizeMult) && stringSize2.Height < (size.w * sizeMult)) {
                        font2 = new Font(font, font2.Size + 1);
                        stringSize2 = g.MeasureString(name2, font2);
                    }

                    //if area covered by font2 is greater than area covered by font1 then use font2
                    if (stringSize2.Width * stringSize2.Height > stringSize.Width * stringSize.Height) {
                        font1 = font2;
                        stringSize = stringSize2;
                        name = name2;
                    }
                }

                //invert the color of the holding color
                Color textColor = opisiteExtremeColor(r.color);
                /*
                //Color.FromArgb(255 - r.color.R, 255 - r.color.G, 255 - r.color.B);

                //if the 2 colors are too close to eachother
                int colorClosness = 50;
                if (Math.Abs(textColor.R - r.color.R) < colorClosness && Math.Abs(textColor.G - r.color.G) < colorClosness && Math.Abs(textColor.B - r.color.B) < colorClosness) {
                    //rotate the color of the text 120degrees around the color wheel
                    textColor = Color.FromArgb(r.color.R, (r.color.G + 85) % 255, (r.color.B + 170) % 255);
                    Console.WriteLine("\tRoating color of " + r.name);
                }
                */
                //if text size is greater than 20px then set antialiasing to none
                if (font1.Size > 20) {
                    g.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
                }

                //write holding name to map centered at h.center with h.color and font Arial 12 bold
                g.DrawString(name, font1, new SolidBrush(textColor), new Point(center.x, center.y), stringFormat);

            }

            newBmp.Save(localDir + @"\Output\"+ modName + @"\debug\region_names.png");

        }

        void WriteNamesArea(Dictionary<string, Regions> regions) {
            //load merged image from localDir + "\\Output\\" + modName+ "\\" + holdingLevel + modName + ".png"
            Bitmap? newBmp = new(localDir + @"\Output\"+ modName + @"\area_region\area_region.png");

            MaximumRectangle mr = new();

            Graphics g = Graphics.FromImage(newBmp);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            StringFormat stringFormat = new() {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            float sizeMult = 1.0f;

            int minFontSize = 7;
            string font = "Verdana";

            //for each region in regions dictionray
            foreach (KeyValuePair<string, Regions> region in regions) {
                foreach (Area r in region.Value.areas) {

                    //skip empty regions
                    if (r.coords.Count == 0 || r.name.Contains("unused")) continue;

                    //print progress
                    Console.WriteLine("\t" + r.name);

                    //find largest rectangle in region
                    (int x, int y) center = (0, 0);
                    (int h, int w) size = (0, 0);

                    bool floodFill = true;
                    
                    (center, size) = mr.center(r.coords.ToList(), floodFill);
                    
                    string[] words = r.name.Replace("area", "").Replace("_"," ").Trim().Split(" ");
                    //capitilize first leter of each word
                    for (int i = 0; i < words.Length; i++) {
                        words[i] = string.Concat(words[i][..1].ToUpper(), words[i].AsSpan(1));
                    }


                    //get all but first element of words and join them with " "
                    string name = string.Join(" ", words).Trim();


                    Font font1 = new(font, minFontSize);

                    //calculate the size of the string
                    SizeF stringSize = g.MeasureString(name, font1);

                    //increase the size of the string until it fills the holding
                    while (stringSize.Width < (size.h * sizeMult) && stringSize.Height < (size.w * sizeMult)) {
                        font1 = new Font(font, font1.Size + 1);
                        stringSize = g.MeasureString(name, font1);
                    }

                    if (name.Split().Length > 1) {
                        string name2 = string.Join("\n", words).Trim();
                        Font font2 = new(font, minFontSize);
                        SizeF stringSize2 = g.MeasureString(name2, font2);

                        while (stringSize2.Width < (size.h * sizeMult) && stringSize2.Height < (size.w * sizeMult)) {
                            font2 = new Font(font, font2.Size + 1);
                            stringSize2 = g.MeasureString(name2, font2);
                        }

                        //if area covered by font2 is greater than area covered by font1 then use font2
                        if (stringSize2.Width * stringSize2.Height > stringSize.Width * stringSize.Height) {
                            font1 = font2;
                            stringSize = stringSize2;
                            name = name2;
                        }
                    }

                    //invert the color of the holding color
                    Color textColor = opisiteExtremeColor(r.color);
                    /*
                    //Color.FromArgb(255 - r.color.R, 255 - r.color.G, 255 - r.color.B);
                    
                    //if the 2 colors are too close to eachother
                    int colorClosness = 50;
                    if (Math.Abs(textColor.R - r.color.R) < colorClosness && Math.Abs(textColor.G - r.color.G) < colorClosness && Math.Abs(textColor.B - r.color.B) < colorClosness) {
                        //rotate the color of the text 120degrees around the color wheel
                        textColor = Color.FromArgb(r.color.R, (r.color.G + 85) % 255, (r.color.B + 170) % 255);
                        Console.WriteLine("\tRoating color of " + r.name);
                    }
                    */
                    //if text size is greater than 20px then set antialiasing to none
                    if (font1.Size > 20) {
                        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
                    }

                    //write holding name to map centered at h.center with h.color and font Arial 12 bold
                    g.DrawString(name, font1, new SolidBrush(textColor), new Point(center.x, center.y), stringFormat);

                }
            }

            newBmp.Save(localDir + @"\Output\"+ modName + @"\debug\area_names.png");

        }

        //string to color
        Color StringToColor(string str) {
            //takes a string and converts it to a random constant color
            using SHA256 sha256Hash = SHA256.Create();
            byte[] data = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(str));
            return Color.FromArgb(data[0], data[1], data[2]);
        }

        string CapitalizeString(string str, string remove="") {
            List<string> name = str.Replace(remove, "").Replace("_", " ").Trim().Split(" ").ToList();
            for (int i = 0; i < name.Count; i++) {
                //sequence contains no elements try catch
                try {
                    name[i] = string.Concat(name[i][..1].ToUpper(), name[i].AsSpan(1));
                }
                catch (Exception) {
                    name[i] = name[i].ToUpper();
                }

                //name[i] = name[i].First().ToString().ToUpper() + name[i][1..];
            }
            return string.Join(" ", name);
        }

        Color opisiteExtremeColor(Color c) {
            //for each rgb value in color find if it is closer to 0 or 255 and set it to the opposite
            int r = c.R;
            int g = c.G;
            int b = c.B;

            if (r > 127) r = 0;
            else r = 255;

            if (g > 127) g = 0;
            else g = 255;

            if (b > 127) b = 0;
            else b = 255;

            return Color.FromArgb(r, g, b);

        }

        void ParseConfig() {
            
            string[] lines = File.ReadAllLines(localDir + @"\Input\config.txt");
            string[] validKeys = { "game", "name", "areaSorting" };
            foreach(string line in lines) {
                string l1 = CleanLine(line);
                
                if (l1.Contains('=')) {
                    //split l1 on = the first is key and the second is value
                    string[] split = l1.Split("=");
                    string key = split[0].Trim();
                    string value = split[1].Trim();
                    
                    //switch on key
                    switch (key) {
                        case "game":
                            if (value.ToUpper() == "EU4" || value.ToUpper() == "IR") {
                                game = value.ToUpper();
                            }
                            else {
                                Console.WriteLine("Invalid game in config.txt\nusing default game IR");
                                game = "IR";
                            }
                            break;
                        case "name":
                            if (value.Length > 0) {
                                modName = value.Replace("\"", "").Trim();
                            }
                            else {
                                Console.WriteLine("Invalid name in config.txt \nusing default as mod name");
                                modName = "default";
                            }
                            break;
                        case "areaSorting":
                            if (value.ToLower() == "alphabetical" || value.ToLower() == "group") {
                                areaSorting = value.ToLower();
                            }
                            else {
                                Console.WriteLine("Invalid areaSorting in config.txt \nno organization will be used");
                                areaSorting = "";
                            }
                            break;

                    }
                    Console.WriteLine(l1);
                }
            }

        }
    }
    
}