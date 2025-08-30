using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using Timer = System.Windows.Forms.Timer;

namespace CitySimPrototype
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // 擴充版模擬城市建設原型 (WinForms 單檔示範)
    // 功能新增：建築升級、隨機事件/自然災害、儲存/讀取、簡易日誌、右鍵互動
    // 使用方法：Visual Studio -> 建立 WinForms (.NET Framework) -> 替換 Program.cs 內容為本檔並執行

    public enum TileType { Empty, Residential, Commercial, Industrial, PowerPlant, WaterPlant, Road, Park }

    public class Tile
    {
        public TileType Type { get; set; } = TileType.Empty;
        public int Level { get; set; } = 1; // 升級等級
    }

    [Serializable]
    public class CityState
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public Tile[,] Map { get; set; }

        // 資源
        public int Money { get; set; }
        public int Power { get; set; }
        public int Water { get; set; }
        public int Materials { get; set; }
        public int Population { get; set; }
        public int Jobs { get; set; }
        public int Pollution { get; set; }

        // 其他
        public int TickCount { get; set; }

        // 必要的無參構造
        public CityState() { }

        public CityState(int w, int h)
        {
            Width = w; Height = h;
            Map = new Tile[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    Map[x, y] = new Tile();

            // 初始資源
            Money = 8000;
            Power = 0;
            Water = 0;
            Materials = 200;
            Population = 100;
            Jobs = 60;
            Pollution = 0;
            TickCount = 0;
        }



        public void Tick(int secondsElapsed, double taxRate, Action<string> log)
        {
            TickCount++;
            // 計算產出/消耗
            int powerProduced = 0;
            int waterProduced = 0;
            int materialsProduced = 0;
            int commercialJobs = 0;
            int industrialJobs = 0;
            int residentialCapacity = 0;
            int pollutionGenerated = 0;

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    var t = Map[x, y];
                    switch (t.Type)
                    {
                        case TileType.Residential:
                            residentialCapacity += 10 * t.Level;
                            break;
                        case TileType.Commercial:
                            commercialJobs += 12 * t.Level;
                            break;
                        case TileType.Industrial:
                            industrialJobs += 8 * t.Level;
                            materialsProduced += 6 * t.Level;
                            pollutionGenerated += 4 * t.Level;
                            break;
                        case TileType.PowerPlant:
                            powerProduced += 60 * t.Level;
                            pollutionGenerated += 6 * t.Level;
                            break;
                        case TileType.WaterPlant:
                            waterProduced += 60 * t.Level;
                            break;
                        case TileType.Park:
                            pollutionGenerated -= 5 * t.Level;
                            break;
                    }
                }

            

            int powerNeeded = Population * 1;
            int waterNeeded = Population * 1;

            // 供需
            int powerSurplus = powerProduced - powerNeeded;
            int waterSurplus = waterProduced - waterNeeded;

            // 紀錄供給到狀態
            Power = Math.Max(0, powerProduced);
            Water = Math.Max(0, waterProduced);

            double shortageFactor = 0.0;
            if (powerProduced < powerNeeded) shortageFactor += (powerNeeded - powerProduced) / (double)Math.Max(1, powerNeeded);
            if (waterProduced < waterNeeded) shortageFactor += (waterNeeded - waterProduced) / (double)Math.Max(1, waterNeeded);

            // 人口變化（加入更穩健的條件）
            int growthBase = (int)(Population *  (1.0 - shortageFactor)); // 基礎增長

            int vacancy = residentialCapacity - Population;
            if (vacancy < 0) growthBase -= (int)(Math.Abs(vacancy) * 0.5);
            else growthBase += (int)(vacancy * 0.5);
            int jobGap = Math.Max(0, Population - Jobs);
            if (jobGap > 0) growthBase -= (int)(jobGap * 0.03);

            // 污染與滿意度影響
            double pollutionPenalty = Math.Max(0, (Pollution + pollutionGenerated) / 1000.0); // 污染過高會衰退
            growthBase = (int)(growthBase * (1.2 - pollutionPenalty-taxRate));

            // 淨人口變化
            Population = Math.Max(0, Population + growthBase);

            Jobs = Math.Min(Population,commercialJobs + industrialJobs);
            // 產物進帳
            Materials += materialsProduced;

            // 稅收與維護
            int taxes = (int)(Population * taxRate + commercialJobs * 1);
            Money += taxes;
            int maintenance = (int)(Width * Height * 0.12 + Population * 0.6);
            Money -= maintenance;

            Pollution = Math.Max(0, Pollution + pollutionGenerated-10);

            // 紀錄日誌（每 10 tick 輸出一次摘要）
            if (TickCount % 10 == 0)
            {
                log?.Invoke($"Tick {TickCount}: 人口={Population}, 金錢={Money}, 就業={Jobs}, 污染={Pollution}");
            }
        }
    }

    public class MainForm : Form
    {
        CityState city;
        const int GRID_W = 12, GRID_H = 12;
        const int TILE_PIX = 40;

        Panel mapPanel;
        Timer simTimer;
        Button btnResidential, btnCommercial, btnIndustrial, btnPower, btnWater, btnRoad, btnPark;
        Label lblMoney, lblPower, lblWater, lblMaterials, lblPopulation, lblJobs, lblPollution, lblTick;
        NumericUpDown nudTaxRate;
        TileType selected = TileType.Residential;
        ListBox lstLog;

        Random rng = new Random();
        double eventChancePerTick = 0.05; // 每秒 5% 機率出事件

        public MainForm()
        {
            this.Text = "CitySim Prototype - Expanded";
            this.ClientSize = new Size(1200, 700);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            city = new CityState(GRID_W, GRID_H);

            InitializeUI();

            simTimer = new Timer();
            simTimer.Interval = 1000; // 每秒 tick
            simTimer.Tick += SimTimer_Tick;
            simTimer.Start();
        }

        void InitializeUI()
        {
            mapPanel = new Panel();
            typeof(Panel).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic,
            null, mapPanel, new object[] { true });

            DoubleBuffered = true;
            mapPanel.Location = new Point(10, 10);
            mapPanel.Size = new Size(GRID_W * TILE_PIX + 1, GRID_H * TILE_PIX + 1);
            mapPanel.Paint += MapPanel_Paint;
            mapPanel.MouseClick += MapPanel_MouseClick;
            mapPanel.MouseDown += MapPanel_MouseDown;
            this.Controls.Add(mapPanel);

            int bx = mapPanel.Right + 20;
            int by = 10;

            Label l = new Label() { Text = "建築選單：", Location = new Point(bx, by), AutoSize = true };
            this.Controls.Add(l);
            by += 24;

            btnResidential = new Button() { Text = "住宅", Location = new Point(bx, by), Width = 120 };
            btnResidential.Click += (s, e) => selected = TileType.Residential; this.Controls.Add(btnResidential); by += 32;
            btnCommercial = new Button() { Text = "商業", Location = new Point(bx, by), Width = 120 };
            btnCommercial.Click += (s, e) => selected = TileType.Commercial; this.Controls.Add(btnCommercial); by += 32;
            btnIndustrial = new Button() { Text = "工業", Location = new Point(bx, by), Width = 120 };
            btnIndustrial.Click += (s, e) => selected = TileType.Industrial; this.Controls.Add(btnIndustrial); by += 32;
            btnPower = new Button() { Text = "發電廠", Location = new Point(bx, by), Width = 120 };
            btnPower.Click += (s, e) => selected = TileType.PowerPlant; this.Controls.Add(btnPower); by += 32;
            btnWater = new Button() { Text = "淨水廠", Location = new Point(bx, by), Width = 120 };
            btnWater.Click += (s, e) => selected = TileType.WaterPlant; this.Controls.Add(btnWater); by += 32;
            btnRoad = new Button() { Text = "道路", Location = new Point(bx, by), Width = 120 };
            btnRoad.Click += (s, e) => selected = TileType.Road; this.Controls.Add(btnRoad); by += 32;
            btnPark = new Button() { Text = "公園", Location = new Point(bx, by), Width = 120 };
            btnPark.Click += (s, e) => selected = TileType.Park; this.Controls.Add(btnPark); by += 32;

            by += 8;
            Label resLabel = new Label() { Text = "資源面板：", Location = new Point(bx, by), AutoSize = true };
            this.Controls.Add(resLabel); by += 24;

            lblMoney = AddStatusLabel("金錢:", bx, ref by);
            lblPower = AddStatusLabel("電力:", bx, ref by);
            lblWater = AddStatusLabel("水:", bx, ref by);
            lblMaterials = AddStatusLabel("建材:", bx, ref by);
            lblPopulation = AddStatusLabel("人口:", bx, ref by);
            lblJobs = AddStatusLabel("就業:", bx, ref by);
            lblPollution = AddStatusLabel("污染:", bx, ref by);
            lblTick = AddStatusLabel("Tick:", bx, ref by);

            by += 8;
            Label taxLabel = new Label() { Text = "稅率：", Location = new Point(bx, by), AutoSize = true };
            this.Controls.Add(taxLabel);
            nudTaxRate = new NumericUpDown() { Location = new Point(bx + 50, by - 3), Width = 60, DecimalPlaces = 2, Increment = 0.01M, Minimum = 0.0M, Maximum = 1.0M, Value = 0.10M };
            this.Controls.Add(nudTaxRate);
            by += 32;

            Button btnPause = new Button() { Text = "暫停/繼續", Location = new Point(bx, by), Width = 120 };
            btnPause.Click += (s, e) => simTimer.Enabled = !simTimer.Enabled; this.Controls.Add(btnPause); by += 32;

            Button btnClear = new Button() { Text = "清空地圖", Location = new Point(bx, by), Width = 120 };
            btnClear.Click += (s, e) => { city = new CityState(GRID_W, GRID_H); Log("地圖已清空"); InvalidateAll(); }; this.Controls.Add(btnClear); by += 32;

            Button btnSave = new Button() { Text = "儲存遊戲", Location = new Point(bx, by), Width = 120 };
            btnSave.Click += (s, e) => SaveGame(); this.Controls.Add(btnSave); by += 32;

            Button btnLoad = new Button() { Text = "讀取遊戲", Location = new Point(bx, by), Width = 120 };
            btnLoad.Click += (s, e) => LoadGame(); this.Controls.Add(btnLoad); by += 32;

            Button btnUpgrade = new Button() { Text = "升級 (右鍵亦可)", Location = new Point(bx, by), Width = 120 };
            btnUpgrade.Click += (s, e) => { MessageBox.Show("請右鍵地圖上的建築進行升級。", "說明", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            this.Controls.Add(btnUpgrade); by += 40;

            Label logLabel = new Label() { Text = "日誌：", Location = new Point(bx, by), AutoSize = true };
            this.Controls.Add(logLabel); by += 20;

            lstLog = new ListBox() { Location = new Point(bx, by), Size = new Size(360, 280) };
            this.Controls.Add(lstLog);

            UpdateUI();
        }

        private Label AddStatusLabel(string name, int bx, ref int by)
        {
            Label l = new Label() { Text = name, Location = new Point(bx, by), AutoSize = true };
            Label v = new Label() { Text = "", Location = new Point(bx + 60, by), AutoSize = true };
            this.Controls.Add(l); this.Controls.Add(v);
            by += 22;
            return v;
        }

        private void MapPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int gx = e.X / TILE_PIX;
                int gy = e.Y / TILE_PIX;
                if (gx < 0 || gy < 0 || gx >= GRID_W || gy >= GRID_H) return;
                var tile = city.Map[gx, gy];
                if (tile.Type == TileType.Empty)
                {
                    MessageBox.Show("空地無法升級。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int upCost = GetUpgradeCost(tile);
                int upMat = GetUpgradeMaterialsCost(tile);
                var dr = MessageBox.Show($"升級 {tile.Type} 等級 {tile.Level} -> {tile.Level + 1}金錢 { upCost}, 建材 { upMat}。確定？", "升級建築", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.Yes)
                {
                    if (city.Money < upCost) { MessageBox.Show("金錢不足。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    if (city.Materials < upMat) { MessageBox.Show("建材不足。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    city.Money -= upCost; city.Materials -= upMat; tile.Level++;
                    Log($"已升級格子 ({gx},{gy}) 為 {tile.Type} Lv{tile.Level}");
                    InvalidateAll();
                }
            }
        }

        private void MapPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int gx = e.X / TILE_PIX;
                int gy = e.Y / TILE_PIX;
                if (gx < 0 || gy < 0 || gx >= GRID_W || gy >= GRID_H) return;

                int cost = GetCostFor(selected);
                int matCost = GetMaterialsCostFor(selected);
                if (city.Money < cost)
                {
                    MessageBox.Show("金錢不足，無法建造。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (city.Materials < matCost)
                {
                    MessageBox.Show("建材不足，無法建造。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                city.Money -= cost;
                city.Materials -= matCost;
                city.Map[gx, gy].Type = selected;
                city.Map[gx, gy].Level = 1;
                Log($"在 ({gx},{gy}) 建造 {selected} 花費 {cost} 金錢, {matCost} 建材");
                InvalidateAll();
            }
        }

        private int GetMaterialsCostFor(TileType t)
        {
            switch (t)
            {
                case TileType.Residential: return 12;
                case TileType.Commercial: return 25;
                case TileType.Industrial: return 35;
                case TileType.PowerPlant: return 80;
                case TileType.WaterPlant: return 70;
                case TileType.Road: return 3;
                case TileType.Park: return 8;
                default: return 0;
            }
        }

        private int GetCostFor(TileType t)
        {
            switch (t)
            {
                case TileType.Residential: return 300;
                case TileType.Commercial: return 600;
                case TileType.Industrial: return 900;
                case TileType.PowerPlant: return 1800;
                case TileType.WaterPlant: return 1600;
                case TileType.Road: return 80;
                case TileType.Park: return 300;
                default: return 0;
            }
        }

        private int GetUpgradeCost(Tile tile)
        {
            // 升級成本以等級與類型調整
            int baseCost = 0;
            switch (tile.Type)
            {
                case TileType.Residential: baseCost = 200; break;
                case TileType.Commercial: baseCost = 400; break;
                case TileType.Industrial: baseCost = 600; break;
                case TileType.PowerPlant: baseCost = 1200; break;
                case TileType.WaterPlant: baseCost = 1000; break;
                case TileType.Park: baseCost = 150; break;
                default: baseCost = 100; break;
            }
            return baseCost * tile.Level;
        }

        private int GetUpgradeMaterialsCost(Tile tile)
        {
            return GetMaterialsCostFor(tile.Type) * tile.Level;
        }

        private void SimTimer_Tick(object sender, EventArgs e)
        {
            double taxRate = (double)nudTaxRate.Value;
            city.Tick(1, taxRate, Log);
            // 事件觸發
            if (rng.NextDouble() < eventChancePerTick)
            {
                TriggerRandomEvent();
            }
            UpdateUI();
            mapPanel.Invalidate();
        }

        private void TriggerRandomEvent()
        {
            // 事件：地震（毀建多格）、經濟繁榮或衰退
            double r = rng.NextDouble();
            if (r < 0.5)
            {
                // 地震
                int cx = rng.Next(GRID_W);
                int cy = rng.Next(GRID_H);
                int radius = rng.Next(1, 3);
                int destroyed = 0;
                for (int x = Math.Max(0, cx - radius); x <= Math.Min(GRID_W - 1, cx + radius); x++)
                    for (int y = Math.Max(0, cy - radius); y <= Math.Min(GRID_H - 1, cy + radius); y++)
                    {
                        if (city.Map[x, y].Type != TileType.Empty)
                        {
                            city.Map[x, y].Type = TileType.Empty;
                            city.Map[x, y].Level = 1;
                            destroyed++;
                        }
                    }
                city.Pollution = Math.Max(0, city.Pollution - destroyed); // 假設建築被毀污染反而下降
                MessageBox.Show($"地震！中心 ({cx},{cy}) 半徑 {radius}，毀壞 {destroyed} 棟建築。");
                Log($"地震！中心 ({cx},{cy}) 半徑 {radius}，毀壞 {destroyed} 棟建築。");
                city.Money -= 500 * destroyed; // 重建費用
            }
            else
            {
                // 經濟事件
                bool boom = rng.NextDouble() < 0.5;
                if (boom)
                {
                    int gain = 2000 + rng.Next(0, 2000);
                    city.Money += gain;
                    MessageBox.Show($"經濟繁榮：獲得額外收入 {gain}。");
                    Log($"經濟繁榮：獲得額外收入 {gain}。");
                }
                else
                {
                    int loss = 1000 + rng.Next(0, 1500);
                    city.Money = Math.Max(0, city.Money - loss);
                    MessageBox.Show($"經濟衰退：損失 {loss} 金錢。");
                    Log($"經濟衰退：損失 {loss} 金錢。");
                }
            }
            InvalidateAll();
        }

        private void UpdateUI()
        {
            lblMoney.Text = city.Money.ToString();
            lblPower.Text = city.Power.ToString();
            lblWater.Text = city.Water.ToString();
            lblMaterials.Text = city.Materials.ToString();
            lblPopulation.Text = city.Population.ToString();
            lblJobs.Text = city.Jobs.ToString();
            lblPollution.Text = city.Pollution.ToString();
            lblTick.Text = city.TickCount.ToString();
        }

        private void InvalidateAll()
        {
            UpdateUI();
            mapPanel.Invalidate();
        }

        private void MapPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            for (int x = 0; x < GRID_W; x++)
                for (int y = 0; y < GRID_H; y++)
                {
                    Rectangle r = new Rectangle(x * TILE_PIX, y * TILE_PIX, TILE_PIX, TILE_PIX);
                    var t = city.Map[x, y];
                    Color fill = Color.LightGray;
                    switch (t.Type)
                    {
                        case TileType.Empty: fill = Color.Beige; break;
                        case TileType.Residential: fill = Color.LightGreen; break;
                        case TileType.Commercial: fill = Color.LightBlue; break;
                        case TileType.Industrial: fill = Color.Orange; break;
                        case TileType.PowerPlant: fill = Color.YellowGreen; break;
                        case TileType.WaterPlant: fill = Color.CornflowerBlue; break;
                        case TileType.Road: fill = Color.SaddleBrown; break;
                        case TileType.Park: fill = Color.ForestGreen; break;
                    }
                    using (Brush b = new SolidBrush(fill)) g.FillRectangle(b, r);
                    g.DrawRectangle(Pens.Black, r);
                    if (t.Type != TileType.Empty)
                    {
                        g.DrawString($"L{t.Level}", this.Font, Brushes.Black, r.Location);
                    }
                }

            // 選取提示與說明文字
            g.DrawString($"選擇: " + selected.ToString() + " (左鍵建造 / 右鍵升級)", this.Font, Brushes.Black, new PointF(mapPanel.Right + 22, 25));
        }

        private void SaveGame()
        {
            try
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "CitySave (*.xml)|*.xml";
                if (sfd.ShowDialog() != DialogResult.OK) return;

                // Correctly create an XmlSerializer for the SerializableCityState type
                var ser = new XmlSerializer(typeof(SerializableCityState));

                using (var fs = File.Create(sfd.FileName))
                {
                    // Create the serializable data object
                    var data = new SerializableCityState(city);

                    // Serialize the data object to the file stream
                    ser.Serialize(fs, data);
                }

                Log("儲存成功。" + Path.GetFileName(sfd.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show("儲存失敗: " + ex.Message);
            }
        }

        private void LoadGame()
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "CitySave (*.xml)|*.xml";
                if (ofd.ShowDialog() != DialogResult.OK) return;

                // Correctly create an XmlSerializer for the SerializableCityState type
                var ser = new XmlSerializer(typeof(SerializableCityState));

                using (var fs = File.OpenRead(ofd.FileName))
                {
                    // Deserialize the data from the file stream
                    var data = (SerializableCityState)ser.Deserialize(fs);

                    // Convert the deserialized data back to the game's CityState object
                    city = data.ToCityState();
                }

                Log("讀檔成功。" + Path.GetFileName(ofd.FileName));
                InvalidateAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show("讀取失敗: " + ex.Message);
            }
        }

        private void Log(string text)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            lstLog.Items.Insert(0, $"[{time}] {text}");
            // 限制日誌長度
            if (lstLog.Items.Count > 200) lstLog.Items.RemoveAt(lstLog.Items.Count - 1);
        }
    }

    // 為了序列化二維陣列，使用中介類
    [Serializable]
    public class SerializableCityState
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<SerializableTile> Tiles { get; set; }

        public int Money { get; set; }
        public int Power { get; set; }
        public int Water { get; set; }
        public int Materials { get; set; }
        public int Population { get; set; }
        public int Jobs { get; set; }
        public int Pollution { get; set; }
        public int TickCount { get; set; }

        public SerializableCityState() { }

        public SerializableCityState(CityState s)
        {
            Width = s.Width; Height = s.Height;
            Tiles = new List<SerializableTile>();
            for (int x = 0; x < s.Width; x++)
                for (int y = 0; y < s.Height; y++)
                    Tiles.Add(new SerializableTile { X = x, Y = y, Type = s.Map[x, y].Type, Level = s.Map[x, y].Level });
            Money = s.Money; Power = s.Power; Water = s.Water; Materials = s.Materials; Population = s.Population; Jobs = s.Jobs; Pollution = s.Pollution; TickCount = s.TickCount;
        }

        public CityState ToCityState()
        {
            CityState s = new CityState(Width, Height);
            s.Money = Money; s.Power = Power; s.Water = Water; s.Materials = Materials; s.Population = Population; s.Jobs = Jobs; s.Pollution = Pollution; s.TickCount = TickCount;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    s.Map[x, y] = new Tile();
            foreach (var t in Tiles)
            {
                s.Map[t.X, t.Y].Type = t.Type;
                s.Map[t.X, t.Y].Level = t.Level;
            }
            return s;
        }
    }

    [Serializable]
    public class SerializableTile
    {
        public int X { get; set; }
        public int Y { get; set; }
        public TileType Type { get; set; }
        public int Level { get; set; }
    }
}
