using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
namespace RacingUI
{
    // Render only the painted panel silhouette; the source sheet backdrop is never drawn.
    [UxmlElement]
    public partial class TpsSkinPart : VisualElement
    {
        Texture2D texture; string part="title";
        [UxmlAttribute] public Texture2D skinTexture { get=>texture; set {texture=value;MarkDirtyRepaint();} }
        [UxmlAttribute] public string panelPart {get=>part;set {part=value;MarkDirtyRepaint();}}
        public TpsSkinPart(){generateVisualContent+=Draw;}
        static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        void Draw(MeshGenerationContext ctx)
        {
            if(texture==null||contentRect.width<=0||contentRect.height<=0)return;
            float[] coordinates; Rect region;
            switch(part)
            {
                case "timer":region=new Rect(598,8,680,150);coordinates=new float[]{531,79,547,64,552,58,601,58,631,29,633,26,636,24,636,23,644,15,647,13,648,14,657,14,658,13,689,13,690,14,1097,14,1102,20,1102,30,1096,36,1096,37,1064,68,1053,81,1052,81,1041,93,1040,93,1032,101,1032,102,1026,107,1026,108,1001,133,998,133,997,132,996,133,993,133,992,132,983,132,982,133,972,133,971,132,961,132,958,133,955,131,911,131,910,130,681,130,680,131,679,130,659,130,658,131,639,131,622,113,620,112,609,99,597,88,586,88,585,89,582,88,577,88,576,89,575,88,570,88,568,89,567,88,566,89,554,89,553,88,544,88,543,89,539,89,536,88,530,82};break;
                case "ranking":region=new Rect(1360,85,530,300);coordinates=new float[]{1204,101,1205,101,1211,95,1212,93,1213,93,1222,84,1245,84,1246,83,1249,83,1250,84,1307,84,1308,83,1313,83,1315,84,1316,83,1381,83,1389,91,1400,91,1401,92,1411,92,1412,91,1413,92,1415,91,1420,91,1421,92,1441,91,1442,92,1452,92,1453,91,1462,91,1463,92,1466,92,1467,91,1468,92,1473,92,1474,91,1483,91,1484,92,1493,92,1494,91,1505,91,1508,92,1509,91,1524,91,1525,92,1534,92,1535,91,1610,91,1610,92,1619,100,1624,106,1625,106,1629,112,1636,118,1637,120,1637,126,1636,127,1636,135,1637,136,1637,151,1636,152,1636,155,1637,156,1637,167,1636,168,1636,177,1637,178,1637,182,1636,183,1636,203,1637,204,1637,209,1636,210,1636,217,1637,218,1637,227,1636,228,1636,233,1637,234,1637,305,1607,334,1564,334,1563,333,1559,333,1557,334,1556,333,1555,334,1541,334,1540,333,1539,334,1535,334,1534,333,1526,333,1524,331,1478,331,1477,330,1473,330,1472,331,1471,330,1379,330,1378,329,1369,329,1368,330,1357,330,1356,329,1353,330,1352,329,1349,329,1348,330,1337,330,1336,329,1328,329,1327,330,1317,330,1316,329,1307,329,1306,330,1297,330,1296,329,1286,329,1285,330,1276,330,1275,329,1265,329,1263,331,1223,331,1193,301,1192,124,1193,123,1193,118,1192,117,1192,114};break;
                case "map":region=new Rect(12,475,345,568);coordinates=new float[]{52,421,53,420,156,420,157,419,164,419,165,420,183,419,184,420,199,420,201,419,210,428,274,428,294,449,295,449,301,455,301,666,302,668,301,669,301,679,302,680,302,688,301,689,301,699,302,700,302,705,301,706,301,742,302,745,301,746,301,763,302,764,302,768,301,769,301,832,302,833,301,834,301,867,302,868,302,876,284,893,284,894,273,905,228,905,227,904,200,904,199,903,180,903,179,904,177,903,82,903,81,904,43,904,15,875,15,794,17,789,17,784,16,783,16,766,17,763,16,762,16,753,17,752,17,740,16,739,16,731,17,730,17,721,16,720,16,717,17,715,16,714,16,711,17,710,17,700,16,699,16,690,17,689,17,679,16,678,16,670,17,669,17,658,16,657,16,648,17,647,17,637,16,636,16,596,15,595,15,449,24,441,24,440,43,421};break;
                case "instrument":region=new Rect(1098,628,800,401);coordinates=new float[]{1635,640,1633,642,1636,645,1636,655,1646,665,1646,825,1644,826,1627,844,1626,844,1617,853,1563,853,1526,891,1516,891,1515,892,1514,891,1508,891,1507,892,1506,891,1434,891,1432,889,1419,889,1418,888,1401,889,1400,888,1383,888,1382,889,1379,889,1378,888,1129,888,1125,891,1050,891,1004,844,979,844,964,828,963,828,963,789,962,788,962,784,963,783,963,764,982,746,982,745,993,734,1054,734,1059,729,1059,704,1098,666,1099,666,1129,635,1130,635,1138,627,1139,625,1140,625,1149,616,1149,615,1150,615,1164,601,1203,601,1212,593,1221,593,1222,592,1380,592,1381,591,1387,591,1388,592,1389,591,1399,591,1402,592,1403,591,1468,591,1475,584,1481,580,1484,576,1485,576,1495,566,1495,565,1496,565,1502,559,1605,558,1634,588,1635,588};break;
                default:region=new Rect(12,14,410,115);coordinates=new float[]{359,32,339,51,339,52,285,106,261,106,260,105,252,105,251,106,243,106,238,102,126,102,125,101,121,101,120,102,119,101,116,101,115,102,75,102,74,101,73,102,35,102,20,87,20,73,19,71,20,70,20,65,19,63,20,62,20,50,21,50,50,21,168,21,175,28,179,28,180,27,183,28,184,27,238,27,239,26,240,27,261,26,263,27,264,26,314,26,322,20,354,20,359,24};break;
            }
            var p=new List<Vector2>();var remaining=new List<int>();var triangles=new List<ushort>();
            for(int i=0;i<coordinates.Length;i+=2){p.Add(new Vector2(coordinates[i],coordinates[i+1]));remaining.Add(i/2);}
            int guard=p.Count*p.Count;
            while(remaining.Count>2&&guard-->0)
            {
                bool found=false;
                for(int j=0;j<remaining.Count;j++)
                {
                    int a=remaining[(j+remaining.Count-1)%remaining.Count],b=remaining[j],c=remaining[(j+1)%remaining.Count];
                    if(Cross(p[a],p[b],p[c])<=0)continue;
                    bool inside=false;
                    foreach(int k in remaining)if(k!=a&&k!=b&&k!=c&&Cross(p[a],p[b],p[k])>=0&&Cross(p[b],p[c],p[k])>=0&&Cross(p[c],p[a],p[k])>=0){inside=true;break;}
                    if(inside)continue;
                    triangles.Add((ushort)a);triangles.Add((ushort)b);triangles.Add((ushort)c);remaining.RemoveAt(j);found=true;break;
                }
                if(!found)return;
            }
            // Unity 6 remaps normalized UVs to the texture's atlas region automatically.
            var mesh=ctx.Allocate(p.Count,triangles.Count,texture);Rect uv=new Rect(0,0,1,1);
            foreach(Vector2 point in p)
                mesh.SetNextVertex(new Vertex{position=new Vector3((point.x*1920/1672-region.x)/region.width*contentRect.width,(point.y*1080/941-region.y)/region.height*contentRect.height,Vertex.nearZ),tint=Color.white,uv=new Vector2(uv.x+point.x/1672*uv.width,uv.y+(1-point.y/941)*uv.height)});
            foreach(ushort index in triangles)mesh.SetNextIndex(index);
        }
    }
}
