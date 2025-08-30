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

    // �X�R�����������س]�쫬 (WinForms ���ɥܽd)
    // �\��s�W�G�ؿv�ɯšB�H���ƥ�/�۵M�a�`�B�x�s/Ū���B²����x�B�k�䤬��
    // �ϥΤ�k�GVisual Studio -> �إ� WinForms (.NET Framework) -> ���� Program.cs ���e�����ɨð���

    public enum TileType { Empty, Residential, Commercial, Industrial, PowerPlant, WaterPlant, Road, Park }

    public class Tile
    {
        public TileType Type { get; set; } = TileType.Empty;
        public int Level { get; set; } = 1; // �ɯŵ���
    }

    [Serializable]
    public class CityState
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public Tile[,] Map { get; set; }

        // �귽
        public int Money { get; set; }
        public int Power { get; set; }
        public int Water { get; set; }
        public int Materials { get; set; }
        public int Population { get; set; }
        public int Jobs { get; set; }
        public int Pollution { get; set; }

        // ��L
        public int TickCount { get; set; }

        // ���n���L�Ѻc�y
        public CityState() { }

        public CityState(int w, int h)
        {
            Width = w; Height = h;
            Map = new Tile[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    Map[x, y] = new Tile();

            // ��l�귽
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
            // �p�ⲣ�X/����
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

            // �ѻ�
            int powerSurplus = powerProduced - powerNeeded;
            int waterSurplus = waterProduced - waterNeeded;

            // �����ѵ��쪬�A
            Power = Math.Max(0, powerProduced);
            Water = Math.Max(0, waterProduced);

            double shortageFactor = 0.0;
            if (powerProduced < powerNeeded) shortageFactor += (powerNeeded - powerProduced) / (double)Math.Max(1, powerNeeded);
            if (waterProduced < waterNeeded) shortageFactor += (waterNeeded - waterProduced) / (double)Math.Max(1, waterNeeded);

            // �H�f�ܤơ]�[�J��í��������^
            int growthBase = (int)(Population *  (1.0 - shortageFactor)); // ��¦�W��

            int vacancy = residentialCapacity - Population;
            if (vacancy < 0) growthBase -= (int)(Math.Abs(vacancy) * 0.5);
            else growthBase += (int)(vacancy * 0.5);
            int jobGap = Math.Max(0, Population - Jobs);
            if (jobGap > 0) growthBase -= (int)(jobGap * 0.03);

            // �ìV�P���N�׼v�T
            double pollutionPenalty = Math.Max(0, (Pollution + pollutionGenerated) / 1000.0); // �ìV�L���|�I�h
            growthBase = (int)(growthBase * (1.2 - pollutionPenalty-taxRate));

            // �b�H�f�ܤ�
            Population = Math.Max(0, Population + growthBase);

            Jobs = Math.Min(Population,commercialJobs + industrialJobs);
            // �����i�b
            Materials += materialsProduced;

            // �|���P���@
            int taxes = (int)(Population * taxRate + commercialJobs * 1);
            Money += taxes;
            int maintenance = (int)(Width * Height * 0.12 + Population * 0.6);
            Money -= maintenance;

            Pollution = Math.Max(0, Pollution + pollutionGenerated-10);

            // ������x�]�C 10 tick ��X�@���K�n�^
            if (TickCount % 10 == 0)
            {
                log?.Invoke($"Tick {TickCount}: �H�f={Population}, ����={Money}, �N�~={Jobs}, �ìV={Pollution}");
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
        double eventChancePerTick = 0.05; // �C�� 5% ���v�X�ƥ�

        public MainForm()
        {
            this.Text = "CitySim Prototype - Expanded";
            this.ClientSize = new Size(1200, 700);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            city = new CityState(GRID_W, GRID_H);

            InitializeUI();

            simTimer = new Timer();
            simTimer.Interval = 1000; // �C�� tick
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

            Label l = new Label() { Text = "�ؿv���G", Location = new Point(bx, by), AutoSize = true };
            this.Controls.Add(l);
            by += 24;

            btnResidential = new Button() { Text = "��v", Location = new Point(bx, by), Width = 120 };
            btnResidential.Click += (s, e) => selected = TileType.Residential; this.Controls.Add(btnResidential); by += 32;
            btnCommercial = new Button() { Text = "�ӷ~", Location = new Point(bx, by), Width = 120 };
            btnCommercial.Click += (s, e) => selected = TileType.Commercial; this.Controls.Add(btnCommercial); by += 32;
            btnIndustrial = new Button() { Text = "�u�~", Location = new Point(bx, by), Width = 120 };
            btnIndustrial.Click += (s, e) => selected = TileType.Industrial; this.Controls.Add(btnIndustrial); by += 32;
            btnPower = new Button() { Text = "�o�q�t", Location = new Point(bx, by), Width = 120 };
            btnPower.Click += (s, e) => selected = TileType.PowerPlant; this.Controls.Add(btnPower); by += 32;
            btnWater = new Button() { Text = "�b���t", Location = new Point(bx, by), Width = 120 };
            btnWater.Click += (s, e) => selected = TileType.WaterPlant; this.Controls.Add(btnWater); by += 32;
            btnRoad = new Button() { Text = "�D��", Location = new Point(bx, by), Width = 120 };
            btnRoad.Click += (s, e) => selected = TileType.Road; this.Controls.Add(btnRoad); by += 32;
            btnPark = new Button() { Text = "����", Location = new Point(bx, by), Width = 120 };
            btnPark.Click += (s, e) => selected = TileType.Park; this.Controls.Add(btnPark); by += 32;

            by += 8;
            Label resLabel = new Label() { Text = "�귽���O�G", Location = new Point(bx, by), AutoSize = true };
            this.Controls.Add(resLabel); by += 24;

            lblMoney = AddStatusLabel("����:", bx, ref by);
            lblPower = AddStatusLabel("�q�O:", bx, ref by);
            lblWater = AddStatusLabel("��:", bx, ref by);
            lblMaterials = AddStatusLabel("�ا�:", bx, ref by);
            lblPopulation = AddStatusLabel("�H�f:", bx, ref by);
            lblJobs = AddStatusLabel("�N�~:", bx, ref by);
            lblPollution = AddStatusLabel("�ìV:", bx, ref by);
            lblTick = AddStatusLabel("Tick:", bx, ref by);

            by += 8;
            Label taxLabel = new Label() { Text = "�|�v�G", Location = new Point(bx, by), AutoSize = true };
            this.Controls.Add(taxLabel);
            nudTaxRate = new NumericUpDown() { Location = new Point(bx + 50, by - 3), Width = 60, DecimalPlaces = 2, Increment = 0.01M, Minimum = 0.0M, Maximum = 1.0M, Value = 0.10M };
            this.Controls.Add(nudTaxRate);
            by += 32;

            Button btnPause = new Button() { Text = "�Ȱ�/�~��", Location = new Point(bx, by), Width = 120 };
            btnPause.Click += (s, e) => simTimer.Enabled = !simTimer.Enabled; this.Controls.Add(btnPause); by += 32;

            Button btnClear = new Button() { Text = "�M�Ŧa��", Location = new Point(bx, by), Width = 120 };
            btnClear.Click += (s, e) => { city = new CityState(GRID_W, GRID_H); Log("�a�Ϥw�M��"); InvalidateAll(); }; this.Controls.Add(btnClear); by += 32;

            Button btnSave = new Button() { Text = "�x�s�C��", Location = new Point(bx, by), Width = 120 };
            btnSave.Click += (s, e) => SaveGame(); this.Controls.Add(btnSave); by += 32;

            Button btnLoad = new Button() { Text = "Ū���C��", Location = new Point(bx, by), Width = 120 };
            btnLoad.Click += (s, e) => LoadGame(); this.Controls.Add(btnLoad); by += 32;

            Button btnUpgrade = new Button() { Text = "�ɯ� (�k���i)", Location = new Point(bx, by), Width = 120 };
            btnUpgrade.Click += (s, e) => { MessageBox.Show("�Хk��a�ϤW���ؿv�i��ɯšC", "����", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            this.Controls.Add(btnUpgrade); by += 40;

            Label logLabel = new Label() { Text = "��x�G", Location = new Point(bx, by), AutoSize = true };
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
                    MessageBox.Show("�Ŧa�L�k�ɯšC", "���~", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int upCost = GetUpgradeCost(tile);
                int upMat = GetUpgradeMaterialsCost(tile);
                var dr = MessageBox.Show($"�ɯ� {tile.Type} ���� {tile.Level} -> {tile.Level + 1}���� { upCost}, �ا� { upMat}�C�T�w�H", "�ɯūؿv", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.Yes)
                {
                    if (city.Money < upCost) { MessageBox.Show("���������C", "���~", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    if (city.Materials < upMat) { MessageBox.Show("�ا������C", "���~", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    city.Money -= upCost; city.Materials -= upMat; tile.Level++;
                    Log($"�w�ɯŮ�l ({gx},{gy}) �� {tile.Type} Lv{tile.Level}");
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
                    MessageBox.Show("���������A�L�k�سy�C", "���~", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (city.Materials < matCost)
                {
                    MessageBox.Show("�ا������A�L�k�سy�C", "���~", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                city.Money -= cost;
                city.Materials -= matCost;
                city.Map[gx, gy].Type = selected;
                city.Map[gx, gy].Level = 1;
                Log($"�b ({gx},{gy}) �سy {selected} ��O {cost} ����, {matCost} �ا�");
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
            // �ɯŦ����H���ŻP�����վ�
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
            // �ƥ�Ĳ�o
            if (rng.NextDouble() < eventChancePerTick)
            {
                TriggerRandomEvent();
            }
            UpdateUI();
            mapPanel.Invalidate();
        }

        private void TriggerRandomEvent()
        {
            // �ƥ�G�a�_�]���ئh��^�B�g���c�a�ΰI�h
            double r = rng.NextDouble();
            if (r < 0.5)
            {
                // �a�_
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
                city.Pollution = Math.Max(0, city.Pollution - destroyed); // ���]�ؿv�Q���ìV�ϦӤU��
                MessageBox.Show($"�a�_�I���� ({cx},{cy}) �b�| {radius}�A���a {destroyed} �ɫؿv�C");
                Log($"�a�_�I���� ({cx},{cy}) �b�| {radius}�A���a {destroyed} �ɫؿv�C");
                city.Money -= 500 * destroyed; // ���ضO��
            }
            else
            {
                // �g�٨ƥ�
                bool boom = rng.NextDouble() < 0.5;
                if (boom)
                {
                    int gain = 2000 + rng.Next(0, 2000);
                    city.Money += gain;
                    MessageBox.Show($"�g���c�a�G��o�B�~���J {gain}�C");
                    Log($"�g���c�a�G��o�B�~���J {gain}�C");
                }
                else
                {
                    int loss = 1000 + rng.Next(0, 1500);
                    city.Money = Math.Max(0, city.Money - loss);
                    MessageBox.Show($"�g�ٰI�h�G�l�� {loss} �����C");
                    Log($"�g�ٰI�h�G�l�� {loss} �����C");
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

            // ������ܻP������r
            g.DrawString($"���: " + selected.ToString() + " (����سy / �k��ɯ�)", this.Font, Brushes.Black, new PointF(mapPanel.Right + 22, 25));
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

                Log("�x�s���\�C" + Path.GetFileName(sfd.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show("�x�s����: " + ex.Message);
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

                Log("Ū�ɦ��\�C" + Path.GetFileName(ofd.FileName));
                InvalidateAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ū������: " + ex.Message);
            }
        }

        private void Log(string text)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            lstLog.Items.Insert(0, $"[{time}] {text}");
            // �����x����
            if (lstLog.Items.Count > 200) lstLog.Items.RemoveAt(lstLog.Items.Count - 1);
        }
    }

    // ���F�ǦC�ƤG���}�C�A�ϥΤ�����
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
