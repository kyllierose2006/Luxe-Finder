using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace graph_dsa
{
    // --- 1. DATA CLASSES ---
    public class ShopItem
    {
        public string Title { get; set; }
        public decimal Cost { get; set; }
        public ShopItem(string title, decimal cost) { Title = title; Cost = cost; }
    }

    public class ShopLocation
    {
        public string ShopName { get; set; }
        public string Address { get; set; }
        public Point Coords { get; set; }
        public List<ShopItem> Stock { get; set; } = new List<ShopItem>();

        public ShopLocation(string name, string address, Point pt)
        {
            ShopName = name;
            Address = address;
            Coords = pt;
        }

        public void AddProduct(string name, decimal price)
        {
            Stock.Add(new ShopItem(name, price));
        }
    }

    public class RoadPath
    {
        public ShopLocation StartNode { get; set; }
        public ShopLocation EndNode { get; set; }
        public double Length { get; set; }
        public double TrafficWeight { get; set; }

        public RoadPath(ShopLocation start, ShopLocation end)
        {
            StartNode = start;
            EndNode = end;
            // Hypothetical Distance: Multiply pixel distance by 2.2 to simulate meters
            double pixelDist = Math.Sqrt(Math.Pow(end.Coords.X - start.Coords.X, 2) + Math.Pow(end.Coords.Y - start.Coords.Y, 2));
            Length = pixelDist * 2.2;

            // Simulate traffic (Red Flags impact speed/effort)
            double[] weights = { 1.0, 1.1, 1.3, 2.5 };
            TrafficWeight = weights[new Random(Guid.NewGuid().GetHashCode()).Next(weights.Length)];
        }
    }

    public class TripResult
    {
        public List<ShopLocation> Route { get; set; } = new List<ShopLocation>();
        public double FullDistance { get; set; }
        public ShopLocation FinalShop { get; set; }
        public string ItemFound { get; set; }
        public decimal ItemCost { get; set; }
        public bool IsBestPrice { get; set; }
        public bool IsNearest { get; set; }
    }

    // --- 2. MAIN FORM ---
    public partial class Form1 : Form
    {
        private List<ShopLocation> _allShops = new List<ShopLocation>();
        private List<RoadPath> _allRoads = new List<RoadPath>();
        private ShopLocation _userPos = null;
        private TripResult _chosenPath = null;

        // UI Variables
        private Panel _mySidebar;
        private TextBox _mySearchBox;
        private ListBox _myResultList;
        private Label _myStatusLabel;
        private ComboBox _mySortBox;
        private Label _lblLocationDetail;

        public Form1()
        {
            // NOTE: "InitializeComponent()" removed to fix your error. 
            // We build the UI manually in BuildUserInterface().

            this.Text = "Valenzuela Jewel Map";
            this.Size = new Size(1280, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            SetupData();
            BuildUserInterface();

            this.Paint += RenderMap;
            this.MouseClick += HandleMapClick;
            this.Resize += (s, e) => { this.Invalidate(); if (_mySidebar != null) _mySidebar.Height = this.Height; };
        }

        // --- 3. UI BUILDER (With fixed Location Text) ---
        private void BuildUserInterface()
        {
            _mySidebar = new Panel
            {
                Dock = DockStyle.Right,
                Width = 350,
                BackColor = Color.FromArgb(40, 45, 55),
                Padding = new Padding(20)
            };
            this.Controls.Add(_mySidebar);

            // Title
            Label lblMain = new Label { Text = "LUXE FINDER", Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.FromArgb(212, 175, 55), AutoSize = true, Location = new Point(20, 30) };
            _mySidebar.Controls.Add(lblMain);
            _mySidebar.Controls.Add(new Label { Text = "Valenzuela City", ForeColor = Color.Gray, Location = new Point(22, 70), AutoSize = true });

            // --- LOCATION PANEL ---
            Panel pLoc = new Panel { Location = new Point(20, 110), Size = new Size(300, 60), BackColor = Color.Transparent };
            pLoc.Controls.Add(new Label { Text = "1", Font = new Font("Arial", 24, FontStyle.Bold), ForeColor = Color.Gold, AutoSize = true, Location = new Point(0, 0) });
            pLoc.Controls.Add(new Label { Text = "Location", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.White, Location = new Point(40, 5), AutoSize = true });

            _lblLocationDetail = new Label
            {
                Text = "Click anywhere on the map.",
                ForeColor = Color.Gray,
                Location = new Point(40, 25),
                AutoSize = true
            };
            pLoc.Controls.Add(_lblLocationDetail);
            _mySidebar.Controls.Add(pLoc);
            // ---------------------

            // Search
            _mySidebar.Controls.Add(new Label { Text = "SEARCH ITEM", ForeColor = Color.Gray, Font = new Font("Segoe UI", 8, FontStyle.Bold), Location = new Point(20, 200), AutoSize = true });
            _mySearchBox = new TextBox { Location = new Point(20, 225), Width = 310, Font = new Font("Segoe UI", 12), BorderStyle = BorderStyle.FixedSingle };
            _mySidebar.Controls.Add(_mySearchBox);

            // Sort
            _mySidebar.Controls.Add(new Label { Text = "SORT BY", ForeColor = Color.Gray, Font = new Font("Segoe UI", 8, FontStyle.Bold), Location = new Point(20, 265), AutoSize = true });
            _mySortBox = new ComboBox { Location = new Point(20, 290), Width = 310, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10) };
            _mySortBox.Items.Add("Nearest Distance");
            _mySortBox.Items.Add("Cheapest Price");
            _mySortBox.SelectedIndex = 1;
            _mySidebar.Controls.Add(_mySortBox);

            // Button
            Button btn = new Button { Text = "FIND DEALS", Location = new Point(20, 335), Width = 310, Height = 45, BackColor = Color.FromArgb(212, 175, 55), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btn.Click += RunSearch;
            _mySidebar.Controls.Add(btn);

            // Status
            _myStatusLabel = new Label { Text = "Ready.", ForeColor = Color.Gray, Location = new Point(20, 390), AutoSize = true };
            _mySidebar.Controls.Add(_myStatusLabel);

            // Results List
            _myResultList = new ListBox
            {
                Location = new Point(20, 420),
                Width = 310,
                Height = 300,
                BackColor = Color.FromArgb(30, 35, 45),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 85,
                Font = new Font("Segoe UI", 10)
            };
            _myResultList.DrawItem += DrawListItem;
            _myResultList.SelectedIndexChanged += (s, e) => {
                if (_myResultList.SelectedItem is TripResult t) { _chosenPath = t; this.Invalidate(); }
            };
            _mySidebar.Controls.Add(_myResultList);
        }

        // --- 4. DATA SETUP (Replicating the real map layout) ---
        private void SetupData()
        {
            // 1. SHOPS (Coordinates adjusted to look like the map)

            // MacArthur Highway (Left side, Vertical)
            var s1 = new ShopLocation("Palawan Pawnshop", "MacArthur Hwy (North)", new Point(80, 50));
            s1.AddProduct("Rematado Gold Ring", 3500);
            s1.AddProduct("Pawned Watch", 1200);
            s1.AddProduct("21k Saudi Gold",10000);
            s1.AddProduct("18k Saudi Gold", 7000);
            s1.AddProduct("Creolla Earrings", 5000);


            var s2 = new ShopLocation("Jewelle Giftshop", "SM Supercenter, MacArthur", new Point(100, 220));
            s2.AddProduct("Fashion Necklace", 500);
            s2.AddProduct("Silver Bracelet", 850);
            s2.AddProduct("Venziana Chain", 950);
            s2.AddProduct("South Sea Pearl", 1050);
            s2.AddProduct("Tamborin Necklace", 10500);
            s2.AddProduct("Scrap Gold", 1200);



            var s3 = new ShopLocation("Richgold Pawnshop", "Jade Garden, Marulas", new Point(130, 480));
            s3.AddProduct("18k Wedding Ring", 8500);
            s3.AddProduct("Diamond Studs", 12000);
            s3.AddProduct("Scrap Gold", 1200);
            s3.AddProduct("Alphabet Pendant (A-Z)", 1200);
            s3.AddProduct("Birthstone Ring", 950);



            // Gen T. De Leon (Top Right, Horizontal-ish)
            var s4 = new ShopLocation("Wen'z Silver", "94 Gen. T. de Leon", new Point(500, 80));
            s4.AddProduct("925 Silver Chain", 1500);
            s4.AddProduct("Silver Pendant", 600);
            s4.AddProduct("Stainless Steel Bangle", 600);
            s4.AddProduct("Personalized Name Necklace", 1200);
            s4.AddProduct("Silver Wedding Ring Pair", 2500);
            s4.AddProduct("Birthstone Studs", 600);


            var s5 = new ShopLocation("E.J.M. Pawnshop", "Gen. T. de Leon (East)", new Point(750, 110));
            s5.AddProduct("Smartphone", 4000);
            s5.AddProduct("Gold Pendant", 3200);
            s3.AddProduct("Scrap Gold", 1000);
            s5.AddProduct("14k Gold Bracelet (Rematado)", 4500);
            s5.AddProduct("Baby Earrings (Gold)", 1500);


            // Connector Area (Bottom Right)
            var s6 = new ShopLocation("Cebuana Lhuillier", "One Mall, Valenzuela", new Point(700, 350));
            s6.AddProduct("24k Gold Bar", 15000);
            s6.AddProduct("Heirloom Brooch", 4500);
            s3.AddProduct("Scrap Gold", 1100);
            s3.AddProduct("Heart Pendant Necklace", 1150);
            s3.AddProduct("Beaded Necklace", 1980);




            // Bottom Center Intersection
            var s7 = new ShopLocation("Bulldoggers", "Landmark Intersection", new Point(420, 550));
            s7.AddProduct("Beaded Bracelet", 150);
            s7.AddProduct("Medalion Pendant", 1000);
            s7.AddProduct("Customed Pendant", 1500);
            s7.AddProduct("Wedding Band", 10500);
            s7.AddProduct("Spiritual Ring", 1800);






            _allShops.AddRange(new[] { s1, s2, s3, s4, s5, s6, s7 });

            // 2. PATHS (Mimicking Actual Streets)

            // A. MacArthur Highway (Continuous Line on Left)
            _allRoads.Add(new RoadPath(s1, s2));
            _allRoads.Add(new RoadPath(s2, s3));

            // B. Gen T. de Leon Road (Line on Top Right)
            _allRoads.Add(new RoadPath(s4, s5));
            _allRoads.Add(new RoadPath(s5, s6)); // Road curving down towards One Mall

            // C. Karuhatan Road (Connector: MacArthur -> Gen T)
            // Starts near Palawan/Jewelle, goes to Wen'z
            _allRoads.Add(new RoadPath(s1, s4));

            // D. Maysan/Connector Road (Bottom)
            // Connects Richgold (MacArthur) to Bulldoggers -> Cebuana
            _allRoads.Add(new RoadPath(s3, s7));
            _allRoads.Add(new RoadPath(s7, s6));

            // E. Inner Shortcut
            // Jewelle -> Bulldoggers (cutting through)
            _allRoads.Add(new RoadPath(s2, s7));
        }

        private void HandleMapClick(object sender, MouseEventArgs e)
        {
            if (e.X > this.Width - 350) return;
            _userPos = new ShopLocation("YOU", "Pinned Location", e.Location);
            _lblLocationDetail.Text = $"Pinned: {e.X}, {e.Y}"; // Updates the UI Text
            _myStatusLabel.Text = "Location pinned.";
            _myStatusLabel.ForeColor = Color.Orange;
            _chosenPath = null;
            _myResultList.Items.Clear();
            this.Invalidate();
        }

        private void RunSearch(object sender, EventArgs e)
        {
            if (_userPos == null) { MessageBox.Show("Please click on the map to set your location first."); return; }
            string query = _mySearchBox.Text.ToLower().Trim();
            if (string.IsNullOrEmpty(query)) return;

            var results = new List<TripResult>();
            var entryShop = _allShops.OrderBy(s => GetDist(_userPos.Coords, s.Coords)).First();

            foreach (var shop in _allShops)
            {
                var item = shop.Stock.FirstOrDefault(i => i.Title.ToLower().Contains(query));
                if (item != null)
                {
                    var path = CalculateDijkstra(entryShop, shop);
                    if (path != null)
                    {
                        double walkDist = GetDist(_userPos.Coords, entryShop.Coords) * 2.2;
                        path.FullDistance += walkDist;
                        path.Route.Insert(0, _userPos);
                        path.ItemFound = item.Title;
                        path.ItemCost = item.Cost;
                        path.FinalShop = shop;
                        results.Add(path);
                    }
                }
            }

            if (results.Count == 0) { MessageBox.Show("No items found."); return; }

            decimal minP = results.Min(r => r.ItemCost);
            double minD = results.Min(r => r.FullDistance);
            foreach (var r in results)
            {
                if (r.ItemCost == minP) r.IsBestPrice = true;
                if (Math.Abs(r.FullDistance - minD) < 1.0) r.IsNearest = true;
            }

            if (_mySortBox.SelectedIndex == 0) results = results.OrderBy(r => r.FullDistance).ToList();
            else results = results.OrderBy(r => r.ItemCost).ThenBy(r => r.FullDistance).ToList();

            _myResultList.Items.Clear();
            foreach (var r in results) _myResultList.Items.Add(r);
            _myStatusLabel.Text = $"Found {results.Count} options.";
            _myStatusLabel.ForeColor = Color.LightGreen;

            if (results.Count > 0) _chosenPath = results[0];
            this.Invalidate();
        }

        private TripResult CalculateDijkstra(ShopLocation start, ShopLocation end)
        {
            var dists = new Dictionary<ShopLocation, double>();
            var prevs = new Dictionary<ShopLocation, ShopLocation>();
            var q = new List<ShopLocation>();

            foreach (var s in _allShops) { dists[s] = double.MaxValue; q.Add(s); }
            dists[start] = 0;

            while (q.Count > 0)
            {
                q.Sort((a, b) => dists[a].CompareTo(dists[b]));
                var u = q[0];
                q.RemoveAt(0);

                if (u == end)
                {
                    var res = new TripResult { FinalShop = end, FullDistance = dists[end] };
                    var curr = end;
                    while (curr != null && prevs.ContainsKey(curr))
                    {
                        res.Route.Insert(0, curr);
                        curr = prevs[curr];
                    }
                    res.Route.Insert(0, start);
                    return res;
                }

                if (dists[u] == double.MaxValue) break;

                foreach (var road in _allRoads.Where(r => r.StartNode == u || r.EndNode == u))
                {
                    var v = (road.StartNode == u) ? road.EndNode : road.StartNode;
                    double alt = dists[u] + (road.Length * road.TrafficWeight);
                    if (alt < dists[v])
                    {
                        dists[v] = alt;
                        prevs[v] = u;
                    }
                }
            }
            return null;
        }

        private double GetDist(Point a, Point b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

        // --- 5. DRAWING ---
        private void RenderMap(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;

            // 1. Draw Background Roads (Gray Lines)
            foreach (var r in _allRoads)
            {
                Color c = r.TrafficWeight > 1.5 ? Color.FromArgb(231, 76, 60) : Color.FromArgb(200, 200, 210);
                using (Pen p = new Pen(c, 5))
                {
                    // Round caps make the joints look smoother
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLine(p, r.StartNode.Coords, r.EndNode.Coords);
                }
            }

            // 2. Draw the Best Path (Solid Line from User -> End)
            if (_chosenPath != null && _chosenPath.Route.Count > 1)
            {
                // Choose Color: Green for Cheapest, Blue for Nearest
                Color pathColor = _chosenPath.IsBestPrice ? Color.FromArgb(46, 204, 113) : Color.FromArgb(52, 152, 219);

                using (Pen pPath = new Pen(pathColor, 6))
                {
                    pPath.StartCap = LineCap.Round;
                    pPath.EndCap = LineCap.Round;

                    // Loop through the WHOLE route, starting from 0 (User)
                    for (int i = 0; i < _chosenPath.Route.Count - 1; i++)
                    {
                        Point p1 = _chosenPath.Route[i].Coords;
                        Point p2 = _chosenPath.Route[i + 1].Coords;
                        g.DrawLine(pPath, p1, p2);
                    }
                }
            }

            // 3. Draw Shops
            foreach (var s in _allShops)
            {
                bool isTarget = (_chosenPath != null && s == _chosenPath.FinalShop);

                // Draw Circle
                g.FillEllipse(isTarget ? Brushes.Gold : Brushes.White, s.Coords.X - 10, s.Coords.Y - 10, 20, 20);
                using (Pen borderPen = new Pen(isTarget ? Color.Orange : Color.Black, 2))
                    g.DrawEllipse(borderPen, s.Coords.X - 10, s.Coords.Y - 10, 20, 20);

                // Draw Name
                g.DrawString(s.ShopName, this.Font, Brushes.Black, s.Coords.X - 20, s.Coords.Y + 15);
            }

            // 4. Draw User Pin
            if (_userPos != null)
            {
                // Draw a "Pin" style marker
                g.FillEllipse(Brushes.Red, _userPos.Coords.X - 8, _userPos.Coords.Y - 8, 16, 16);
                g.DrawEllipse(Pens.White, _userPos.Coords.X - 8, _userPos.Coords.Y - 8, 16, 16);
                g.DrawString("YOU", new Font("Segoe UI", 8, FontStyle.Bold), Brushes.Red, _userPos.Coords.X - 15, _userPos.Coords.Y - 25);
            }
        }

        private void DrawListItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var item = (TripResult)_myResultList.Items[e.Index];
            e.DrawBackground();

            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(50, 55, 65)), e.Bounds);

            e.Graphics.DrawString(item.FinalShop.ShopName, new Font("Segoe UI", 11, FontStyle.Bold), Brushes.Gold, e.Bounds.X + 10, e.Bounds.Y + 5);
            e.Graphics.DrawString(item.FinalShop.Address, new Font("Segoe UI", 8, FontStyle.Italic), Brushes.LightGray, e.Bounds.X + 10, e.Bounds.Y + 25);
            e.Graphics.DrawString($"{item.ItemFound}: ₱{item.ItemCost:N0}", new Font("Segoe UI", 10), Brushes.White, e.Bounds.X + 10, e.Bounds.Y + 42);
            e.Graphics.DrawString($"{item.FullDistance:F0}m (approx)", new Font("Segoe UI", 8), Brushes.Gray, e.Bounds.Width - 85, e.Bounds.Y + 10);

            if (item.IsBestPrice)
            {
                e.Graphics.FillRectangle(Brushes.SeaGreen, e.Bounds.X + 10, e.Bounds.Y + 62, 60, 18);
                e.Graphics.DrawString("BEST DEAL", new Font("Arial", 7, FontStyle.Bold), Brushes.White, e.Bounds.X + 13, e.Bounds.Y + 64);
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();   
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}