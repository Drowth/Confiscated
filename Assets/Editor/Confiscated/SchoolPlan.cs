using UnityEngine;

namespace Confiscated.EditorTools
{
    /// <summary>Traced from the user's supplied plan. Coordinates are image pixels; 27 px represents 3 metres.</summary>
    public static class SchoolPlan
    {
        public const float PixelsPerMetre=9f;
        public readonly struct Area
        {
            public readonly string name;
            public readonly Rect rect;
            public readonly int zone; // 0: connected halls; positive: room; negative: open-air yard
            public Area(string name,int zone,float x0,float y0,float x1,float y1)
            {this.name=name;this.zone=zone;rect=Rect.MinMaxRect(x0,y0,x1,y1);}
        }
        public readonly struct Entrance
        {
            public readonly string name;
            public readonly bool vertical, doubleDoor;
            public readonly float line, centre;
            public Entrance(string name,bool vertical,float line,float centre,bool doubleDoor=false)
            {this.name=name;this.vertical=vertical;this.line=line;this.centre=centre;this.doubleDoor=doubleDoor;}
            public Vector3 Position=>vertical?Point(line,centre):Point(centre,line);
            public float Gap=>doubleDoor?2.84f:1.76f;
        }
        public static Vector3 Point(float x,float y,float height=0)=>new Vector3((x-585)/PixelsPerMetre,height,(1183-y)/PixelsPerMetre);
        public static readonly Area[] Areas={
            new("West perimeter",0,264,253,296,1183),new("East perimeter",0,876,253,908,1183),
            new("North hall",0,264,253,908,286),new("North cross hall",0,264,450,908,484),
            new("North link A",0,464,286,487,450),new("North link B",0,583,286,606,450),new("North link C",0,710,286,733,450),
            new("Central spine",0,523,484,553,858),new("East inner spine",0,686,484,718,858),
            new("Middle cross link",0,523,638,718,671),new("South cross hall",0,264,858,908,891),
            new("South link A",0,451,891,479,1151),new("South link B",0,598,891,624,1151),new("South link C",0,764,891,793,1151),
            new("Lower east cross link",0,624,1008,793,1027),new("South perimeter",0,264,1151,908,1183),
            new("Dining entrance",0,383,484,451,504),new("East cross passage A",0,718,588,876,606),
            new("East cross passage B",0,718,730,876,750),new("South west cross passage",0,296,1011,451,1028),
            new("North vestibule",0,438,223,523,253),new("West vestibule",0,235,623,264,697),
            new("East vestibule",0,908,643,936,714),new("South vestibule",0,548,1183,624,1218),
            new("Caretaker office",1,134,367,264,503),
            new("North room A",2,296,286,464,450),new("North classroom A",3,487,286,583,450),
            new("North room B",4,606,286,710,450),new("North classroom B",5,733,286,876,450),
            new("Dining hall",6,296,504,523,805),new("East room A",7,718,484,876,588),
            new("East room B",8,718,606,876,730),new("East classroom",9,718,750,876,858),
            new("Year 6 classroom",10,296,891,451,1011),new("South room A",11,296,1028,451,1151),
            new("South room B",12,479,891,598,1098),new("South classroom",13,624,1027,764,1151),
            new("Store cupboard",14,793,1074,846,1111),
            new("North yard",-1,412,96,551,223),new("North outside path",-1,438,57,524,96),new("North outside step",-1,465,29,494,57),
            new("West yard",-2,98,592,235,728),new("West outside path",-2,72,620,98,697),new("West outside step",-2,45,648,72,676),
            new("East yard",-3,936,611,1074,749),new("East outside path",-3,1074,638,1102,720),new("East outside step",-3,1102,666,1130,694),
            new("South yard",-4,520,1218,653,1289),new("South outside step",-4,571,1289,599,1305)
        };
        public static readonly Entrance[] Doors={
            new("Caretaker office",true,264,444),new("North room A west",true,296,374),new("North room A north",false,286,441),
            new("North classroom A north",false,286,556),new("North classroom A south",false,450,515),
            new("North room B north",false,286,679),new("North room B south",false,450,659),
            new("North classroom B east",true,876,389),new("North classroom B south",false,450,798),
            new("Dining west A",true,296,550),new("Dining west B",true,296,644),
            new("Dining east A",true,523,584),new("Dining east B",true,523,657),new("Dining main entrance",false,504,415,true),
            new("East room A north",false,484,798),new("East room A west",true,718,530),new("East room A south",false,588,771),
            new("East room B west",true,718,654),new("East room B east",true,876,672),
            new("East classroom north",false,750,798),new("East classroom south",false,858,798),
            new("Year 6 north",false,891,425),new("Year 6 west",true,296,950),new("South room A east",true,451,1092),
            new("South room B north",false,891,503),new("South room B east",true,598,1000),
            new("South classroom north",false,1027,710),new("Store cupboard",true,793,1092),
            new("North yard doors",false,223,479,true),new("West yard doors",true,235,656,true),
            new("East yard doors",true,936,676,true),new("South yard doors",false,1218,585,true)
        };
        public static int ZoneAt(float x,float y)
        {
            // Room boundaries meet hall boundaries; no overlapping floor slabs.
            for(int i=Areas.Length-1;i>=0;i--)if(Areas[i].rect.Contains(new Vector2(x,y)))return Areas[i].zone;
            return int.MinValue;
        }
        public static Area Room(int zone){foreach(var a in Areas)if(a.zone==zone)return a;throw new System.ArgumentException("Unknown room");}
    }
}
