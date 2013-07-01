using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace lnE
{
    [DishAttribute("http://www.bilibili.tv/video/", Level = 2, Ext = ".xml")]
    public class bilibiliDish : WebDish
    {
        public override List<Index> GetIndex(HtmlAgilityPack.HtmlDocument html, string url, uint level, string path)
        {
            var books = new List<Index>();

            if (level == 1)
            {
                books.Add(new Index(level) { name = path, url = GetTargetUrl(html) });
                return books;
            }

            var page = html.DocumentNode.SelectNodes("//body/div[@class='z']/div[@class='videobox']/div[@class='viewbox']").First();
            
            var title = page.SelectNodes("./div[@class='info']/h2").First().InnerText;
            var toc = page.SelectNodes("./div[@class='alist']/div[@id='alist']").First();
            if (toc.HasChildNodes)
            {
                foreach (var t in toc.SelectNodes(".//option"))
                {
                    var subTitle = t.InnerText;
                    var n = String.Format("[{0}]{1}", XTrim(title), XTrim(subTitle));
                    var u = t.Attributes["value"].Value;
                    books.Add(new Index(level) { name = n, url = u });
                }
            }
            
            if (!books.Any())
            {
                books.Add(new Index() { name = XTrim(title), url = GetTargetUrl(html), level = 2 });
            }
            else
            {
                var first = books.First();
                first.url = GetTargetUrl(html);
                first.level = 2;
            }

            return books;
        }

        private string GetTargetUrl(HtmlAgilityPack.HtmlDocument html)
        {
            string id = null;
            try 
            {
                var target = html.DocumentNode.SelectNodes("//body/div[@class='z']/div[@class='videobox']/div[@class='scontent']/iframe").FirstOrDefault();
                if (target == null)
                    throw new Exception();
                var ex = new Regex("cid=(\\d+)");
                var m = ex.Match(target.Attributes["src"].Value);
                if (m.Groups.Count < 2)
                    return null;
                id = m.Groups[1].Value;
            }
            catch 
            {
            }

            if (String.IsNullOrWhiteSpace(id))
            {
                try
                {
                    var target = html.DocumentNode.SelectNodes("//body/div[@class='z']/div[@class='videobox']/div[@class='scontent']/script[last()]").FirstOrDefault();
                    if (target == null)
                        throw new Exception();
                    string js = target.InnerText;
                    StringBuilder sb = new StringBuilder();
                    using (var reader = new StringReader(js))
                    {
                        do
                        {
                            var line = reader.ReadLine().Trim();
                            if (line.StartsWith("var flashvars"))
                            {
                                sb.AppendLine(line);
                            }
                        }
                        while (reader.Peek() > 0);
                    }
                    js = sb.ToString();
                    var flashvars = AssemblyHelper.EvalJs(js, "flashvars");
                    id = flashvars["cid"];
                }
                catch
                {
                }

            }

            if (String.IsNullOrWhiteSpace(id))
                throw new Exception("Not supported");

            return String.Format("http://comment.bilibili.tv/{0}.xml", id);
        }

        public override void Eat(HtmlAgilityPack.HtmlDocument html, string url, string path)
        {
            html.Save(path, Encoding.UTF8);
            ExportAss(path);
        }

        void ExportAss(string filename)
        {
            var content = File.ReadAllText(filename);
            var xml = new XmlDocument();
            xml.LoadXml(content);

            List<Bullet> bullets = new List<Bullet>();

            var list = xml.SelectNodes("/i/d").OfType<XmlNode>();
            foreach (var d in list)
            {
                var attrs = d.Attributes["p"].Value;
                var b = new Bullet(d);
                bullets.Add(b);
            }

            var s = new Screen(540, 460, 384);
            var events = s.Conv(bullets);
            var assfile = Path.ChangeExtension(filename, ".ass");

            File.WriteAllText(assfile, @"[Script Info]
Title: 
Original Script: BiliBili
Script Updated By: BiliBili Translator
ScriptType: v4.00
Collisions: Normal
PlayResX: 540
PlayResY: 384
PlayDepth: 32
Timer: 100.0000
WrapStyle: 2

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColor, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,SimHei,15,11861244,11861244,0,-2147483640,-1,0,0,0,100,100,1,0.00,1,1,0,10,30,30,30,1
Style: Static,黑体,25,11861244,11861244,0,-2147483640,-1,0,0,0,100,100,2,0.00,1,1,0,2,0,0,0,1
Style: Scroll,黑体,25,11861244,11861244,0,-2147483640,-1,0,0,0,100,100,2,0.00,1,1,0,2,0,0,0,1

[Events]
Format: Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
");
            File.AppendAllLines(assfile, events.ConvertAll(a => a.ToString()));
        }

    }

    class Bullet
    {
        public Bullet()
        {
            start = String.Empty;
            type = String.Empty;
            size = String.Empty;
            color = String.Empty;
            timestamp = String.Empty;
            owner = String.Empty;
            uid = String.Empty;
            bid = String.Empty;
            content = String.Empty;
        }

        public Bullet(XmlNode d)
            : this()
        {
            var attrs = d.Attributes["p"].Value;
            var slices = attrs.Split(',');
            if (slices.Length != 8)
                return;

            start = slices[0];
            type = slices[1];
            size = slices[2];
            color = slices[3];
            timestamp = slices[4];
            owner = slices[5];
            uid = slices[6];
            bid = slices[7];

            content = d.InnerText;
        }

        public string start;
        public string type;
        public string size;
        public string color;
        public string timestamp;
        public string owner;
        public string uid;
        public string bid;
        public string content;
    }

    class ASSEvent
    {
        public ASSEvent()
        {
            Marked = "0";
            Start = "0";
            End = "0";
            Style = "";
            Name = "BiliBili";
            MarginL = "0000";
            MarginR = "0000";
            MarginV = "0000";
            Effect = "";
            Text = "";
        }

        public string Marked;
        public string Start;
        public string End;
        public string Style;
        public string Name;
        public string MarginL;
        public string MarginR;
        public string MarginV;
        public string Effect;
        public string Text;

        public string GetTimeString(int time)
        {
            int ms = time % 100;
            int sec = time / 100 % 60;
            int min = time / 6000 % 60;
            int hour = time / 360000;

            return String.Format("{0}:{1:d2}:{2:d2}.{3:d2}", hour, min, sec, ms);
        }

        public override string ToString()
        {
            return SpecialFormat("Dialogue: Marked={Marked},{Start},{End},{Style},{Name},{MarginL},{MarginR},{MarginV},{Effect},{Text}");
        }

        string SpecialFormat(string fmt)
        {
            Regex ex = new Regex("{([^}]+)}");
            var matches = ex.Matches(fmt).OfType<Match>();
            var obj = this;
            return ex.Replace(fmt, (MatchEvaluator)(m => (string)obj.GetType().GetField(m.Groups[1].Value).GetValue(obj)));
        }
    }

    class Screen
    {
        enum BulletType
        {
            Scroll = 1,
            Static_Bottom = 4,
            Static_Top = 5,
        }

        class bullet
        {
            public int start;
            public int end;
            public int width;
            public int row;
            public int fs;
            public BulletType type;
            public Bullet data;
        }

        int screenWidth;
        int Width;
        int Height;
        List<bullet> bullets;

        Dictionary<int, List<bullet>> scrolls;
        Dictionary<int, List<bullet>> staticsTop;
        Dictionary<int, List<bullet>> staticsBottom;

        public Screen(int sw, int w, int h)
        {
            screenWidth = sw;
            Width = w;
            Height = h;
            scrolls = new Dictionary<int, List<bullet>>();
            staticsTop = new Dictionary<int, List<bullet>>();
            staticsBottom = new Dictionary<int, List<bullet>>();

            bullets = new List<bullet>();
        }

        public List<ASSEvent> Conv(List<Bullet> bs)
        {
            var ordered = bs.OrderBy(b => Convert.ToDouble(b.start));

            foreach (var b in ordered)
            {
                Put(b);
            }

            List<ASSEvent> events = new List<ASSEvent>();
            foreach (var b in bullets)
            {
                var ass = new ASSEvent();

                ass.Start = ass.GetTimeString(b.start);
                ass.End = ass.GetTimeString(b.end);
                ass.Style = b.type == BulletType.Scroll ? "Scroll" : "Static";
                ass.Name = String.Format("{0}{1}", ass.Name, (int)b.type);

                int rgb = Convert.ToInt32(b.data.color);
                int bgr = ((rgb & 0x0000ff) << 16) | ((rgb & 0xff0000) >> 16) | (rgb & 0x00ff00);

                var fmt = b.type == BulletType.Scroll ?
                            @"{{\a6\move(" + Width + @", {0}, 0, {0})\c&H{1:X6}\fs{2}}}{3}"
                            : @"{{\a5\pos(" + screenWidth / 2 + @", {0})\c&H{1:X6}\fs{2}}}{3}";
                ass.Text = String.Format(fmt, b.row, bgr, b.fs, b.data.content);

                events.Add(ass);
            }

            return events;
        }

        private void Put(Bullet b)
        {
            var bt = new bullet();
            bt.start = Convert.ToInt32(Math.Round(Convert.ToDouble(b.start) * 100));
            bt.fs = Convert.ToInt32(b.size);
            bt.data = b;
            bt.width = bt.fs * b.content.Length;
            Enum.TryParse(b.type, out bt.type);

            int defaultRow = 1;
            Dictionary<int, List<bullet>> s = scrolls;
            bt.end = bt.start + 400;
            if (bt.type == BulletType.Static_Bottom)
            {
                s = staticsBottom;
                defaultRow = Height - bt.fs - 3;
                bt.end = bt.start + 350;
            }
            else if (bt.type == BulletType.Static_Top)
            {
                s = staticsTop;
                bt.end = bt.start + 350;
            }
            bt.row = defaultRow;

            if (s.Any())
            {
                List<bullet> list;
                bool put = false;
                foreach (var pair in s)
                {
                    list = pair.Value;
                    //if (s.TryGetValue(bt.row, out list))
                    //{
                    switch (bt.type)
                    {
                        case BulletType.Scroll:
                            put = CanPutScroll(bt, list);
                            break;
                        case BulletType.Static_Bottom:
                            put = CanPutStaticBottom(bt, list);
                            break;
                        case BulletType.Static_Top:
                            put = CanPutStaticTop(bt, list);
                            break;
                    }

                    if (put)
                    {
                        bt.row = pair.Key;
                        list.Add(bt);
                        break;
                    }
                    //}
                }
                if (!put)
                {
                    var pair = s.Last();
                    if (bt.type == BulletType.Static_Bottom)
                    {

                        int next = pair.Key - bt.fs - 1;
                        if (next > 0)
                            bt.row = next;
                    }
                    else
                    {
                        int next = pair.Key + bt.fs + 1;
                        if (next < Height)
                            bt.row = next;
                    }
                    if (!s.TryGetValue(bt.row, out list))
                    {
                        list = new List<bullet>();
                        list.Add(bt);
                        s.Add(bt.row, list);
                    }
                    else
                    {
                        list.Add(bt);
                    }
                }
            }
            else
            {
                var list = new List<bullet>();
                list.Add(bt);
                s.Add(bt.row, list);
            }

            bullets.Add(bt);
        }

        bool CanPutScroll(bullet b, List<bullet> list)
        {
            return list.All(lb =>
            {
                if (lb.end < b.start)
                    return true;

                return false;
                //var diff = Math.Round((double)(lb.start - b.start) * Width / 400) - b.width;
                //return diff > 0;
            });
        }

        bool CanPutStaticBottom(bullet b, List<bullet> list)
        {
            return list.All(lb =>
            {
                if (lb.end < b.start)
                    return true;

                return false;
                //var diff = Math.Round((double)(lb.start - b.start) * (Height - 3) / 350) - b.fs;
                //return diff > 0;
            });
        }

        bool CanPutStaticTop(bullet b, List<bullet> list)
        {
            return list.All(lb =>
            {
                if (lb.end < b.start)
                    return true;

                return false;
                //var diff = Math.Round((double)(lb.start - b.start) * (Height - 3) / 350) - b.fs;
                //return diff > 0;
            });
        }
    }
}
