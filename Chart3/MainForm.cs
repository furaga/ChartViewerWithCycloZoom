using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Chart3
{
    public partial class MainForm : Form
    {
        // データ
        struct ChartData
        {
            public DateTime Date;
            public double Money;
            public ChartData(DateTime date, double val)
            {
                Date = date;
                Money = val;
            }
        }

        // 相場データ

        // 拡大率
        double scale = 1.0;

        // ズーム中か
        bool zooming = false;

        // グラフ描画用のペン
        Pen defaultPen = new Pen(new SolidBrush(Color.Black));
        Pen grayPen = new Pen(new SolidBrush(Color.Gray));
        Pen lightPen = new Pen(new SolidBrush(Color.LightGray));
        Pen cursorPen = new Pen(new SolidBrush(Color.Red));

        // グラフ描画用のブラシ
        SolidBrush blackBrush = new SolidBrush(Color.Black);
        SolidBrush redBrush = new SolidBrush(Color.Red);
        SolidBrush blueBrush = new SolidBrush(Color.Blue);

        // 時間計測用ストップウォッチ
        Stopwatch stopWatch = null;


        //-------------------------------------------------------------------------------
        // コンストラクタ
        //-------------------------------------------------------------------------------
        public MainForm()
        {
            InitializeComponent();

            // MouseWheel += PictureBoxChart_MouseWheel;

            try
            {
                //
                // 相場データの読み込み
                //

                foreach (var line in File.ReadAllLines("../data.csv"))
                {
                    var t = line.TrimEnd(new[] { ',', ' ' }).Split(new[] { ',' });

                    double value;

                    if (t.Length >= 2 && double.TryParse(t[1], out value))
                    {
                        var date = DateTime.Parse(t[0]);
                        sortedDataList.Add(new ChartData(date, value));
                    }
                }

                // グラフ画像を生成
                pictureBoxChart.Image = new Bitmap(pictureBoxChart.Width, pictureBoxChart.Height, PixelFormat.Format32bppArgb);

                // 描画
                DrawChart();
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        //-------------------------------------------------------------------------------
        //
        // イベントハンドラ
        //
        //-------------------------------------------------------------------------------

        /*
        // マウスホイール
        void PictureBoxChart_MouseWheel(object sender, MouseEventArgs e)
        {
            Zoom(e.Delta / 100 * 1.3f);
            DrawChart();
        }
        */

        void Zoom(double dScale)
        {
            var prevScale = scale;

            scale += dScale;

            if (scale < 1)
            {
                scale = 1;
            }

            // カーソル位置を中心として拡大するように調整
            var imgWidth = pictureBoxChart.Width - offset.X;
            var scaleRatio = scale / prevScale;
            var imgCursorX = cursor.X - offset.X;
            var left = -graphOffsetUnit * prevScale * imgWidth;
            graphOffsetUnit = ((imgCursorX - left) * scaleRatio - imgCursorX) / (scale * imgWidth);
        }

        CycloZoom cycroZoom = new CycloZoom();

        // 画面がリサイズされたら
        void OnResize(object sender, EventArgs e)
        {
            pictureBoxChart.Image = new Bitmap(pictureBoxChart.Width, pictureBoxChart.Height, PixelFormat.Format32bppArgb);
            DrawChart();
        }

        // タイマー呼び出し
        private void timer_Tick(object sender, EventArgs e)
        {
            Ellipse ellipse;
            double angle;
            zooming = cycroZoom.CheckZoom(Cursor.Position, out ellipse, out angle);

            if (zooming)
            {
                double bias = angle > 0 ? 0.14 : 0.25;
                var dScale = scale * angle * bias;
                Zoom(dScale);
            }


            // 左移動
            var pt = splitContainer3.Panel1.PointToScreen(buttonLeft.Location);
            var r = new Rectangle(pt, buttonLeft.Size);
            if (r.Contains(Cursor.Position))
            {
                graphOffsetUnit -= 0.01 / scale;
            }
        
            // 右移動
            pt = splitContainer3.Panel2.PointToScreen(buttonRight.Location);
            r = new Rectangle(pt, buttonRight.Size);
            if (r.Contains(Cursor.Position))
            {
                graphOffsetUnit += 0.01 / scale;
            }

            if (stopWatch != null)
            {
                labelTime.Text = string.Format("経過時間 : {0:0} 秒", stopWatch.Elapsed.TotalSeconds);
            }

            DrawChart();
        }


        //-------------------------------------------------------------------------------
        //
        // グラフ描画
        //
        //-------------------------------------------------------------------------------

        // グラフ位置のオフセット。左下原点
        readonly Point offset = new Point(50, 30);

        // 表示する金額の最大・最小値
        const int MAX_MONEY = 150;
        const int MIN_MONEY = 70;

        // グラフ中に描画するカーソル位置
        Point cursor;

        // グラフがどれだけ横にスライドしているかを表す。正規化されている
        double graphOffsetUnit = 0;

        List<ChartData> sortedDataList = new List<ChartData>();
        Font font = new Font("MS UI Gothic", 12);

        //-------------------------------------------------------------------------------
        // 座標計算
        //-------------------------------------------------------------------------------

        // sortedDataListにおけるインデックスからグラフにおけるX座標を計算
        float GetX(int index)
        {
            double MAX_X = sortedDataList.Count;
            double MIN_X = 0;
            double graphWidth = pictureBoxChart.Width - offset.X;
            double scaleX = graphWidth / (MAX_X - MIN_X) * scale;
            double graphX = index * scaleX + offset.X;
            double w = scale * graphWidth;
            double graphOffset = graphOffsetUnit * w;
            return (float)(graphX - graphOffset);
        }

        // 金額からグラフにおけるY座標を計算
        float GetY(double money)
        {
            double imgHeight = pictureBoxChart.Height - offset.Y;
            double scaleY = imgHeight / (MAX_MONEY - MIN_MONEY);
            return (float)(imgHeight - (money - MIN_MONEY) * scaleY);
        }

        // 座標を取得
        PointF GetPoint(int index, double money) { return new PointF(GetX(index), GetY(money)); }

        //-------------------------------------------------------------------------------
        // インデックス値計算
        //-------------------------------------------------------------------------------

        // x座標から、最も近い点のインデックスを取得
        int GetIndex(int x)
        {
            double MAX_X = sortedDataList.Count;
            double MIN_X = 0;
            double graphWidth = pictureBoxChart.Width - offset.X;
            double scaleX = graphWidth / (MAX_X - MIN_X) * scale;
            double w = scale * graphWidth;
            double graphOffset = graphOffsetUnit * w;
            double index = (x + graphOffset - offset.X) / scaleX;
            return (int)Math.Round(index);
        }

        //-------------------------------------------------------------------------------
        // 描画
        //-------------------------------------------------------------------------------

        // チャートを描画
        void DrawChart()
        {
            if (sortedDataList == null) return;

            var sw = Stopwatch.StartNew();

            // 各描画点の座標を取得
            var points = sortedDataList.Select((pair, index) => GetPoint(index, pair.Money)).Where(pt => pt.X >= offset.X).ToArray();
            if (points.Length <= 0) return;

            try
            {
                using (var g = Graphics.FromImage(pictureBoxChart.Image))
                {
                    // 画面をクリア
                    g.Clear(Color.White);

                    // 横線と縦軸のメモリ
                    DrawHorizontalLines(g);

                    // 縦線と横軸のメモリ
                    DrawVirbatileLines(g);

                    // 折れ線
                    g.DrawLines(defaultPen, points);

                    // カーソル位置の更新
                    cursor = pictureBoxChart.PointToClient(Cursor.Position);
                    cursor.X = cursor.X >= offset.X ? cursor.X : offset.X;

                    // カーソルのX座標に近い点のインデックスを取得
                    var index = GetIndex(cursor.X);
                    if (index < 0 || sortedDataList.Count <= index) return;

                    var date = sortedDataList[index].Date;
                    var money = sortedDataList[index].Money;

                    // 日付・金額を表示
                    labelValue.Text = string.Format("{0}/{1}/{2}  {3}円", date.Year, date.Month, date.Day, money);

                    // 表示される日付・金額に対応するグラフ位置を直行する２本の赤線で表示
                    var pt = GetPoint(index, money); 
                    if (!zooming)
                    {
                        g.DrawLine(cursorPen, new Point(offset.X, (int)pt.Y), new Point(pictureBoxChart.Width, (int)pt.Y));
                        g.DrawLine(cursorPen, new Point((int)pt.X, 0), new Point((int)pt.X, pictureBoxChart.Height - offset.Y));
                    }

                    // 各点
                    foreach (var p in points)
                    {
                        var brush = p.Y < pt.Y ? redBrush : p.Y > pt.Y ? blueBrush : blackBrush;
                        g.FillEllipse(brush, new Rectangle((int)p.X - 2, (int)p.Y - 2, 4, 4));
                    }

                    if (!zooming)
                    {
                        // 赤線の交点は大きめに
                        g.FillEllipse(blackBrush, new Rectangle((int)pt.X - 4, (int)pt.Y - 4, 8, 8));
                    }
                }
            }
            finally
            {
                // 再描画
                pictureBoxChart.Refresh();
                labelValue.Refresh();
                labelTime.Refresh();
            }
        }

        // 横線と縦軸のメモリの描画
        void DrawHorizontalLines(Graphics g)
        {
            for (int money = (int)MIN_MONEY; money < (int)MAX_MONEY; money++)
            {
                int y = (int)GetY(money);
                if (money % 10 != 0)
                {
                    // 金額が10の倍数以外は薄めの線
                    g.DrawLine(lightPen, new Point(offset.X, y), new Point(pictureBoxChart.Width, y));
                }
                else
                {
                    // 金額が10の倍数なら濃い目の線
                    g.DrawLine(grayPen, new Point(offset.X, y), new Point(pictureBoxChart.Width, y));

                    // 金額をグラフの横に表示
                    string text = money.ToString();
                    var textSize = g.MeasureString(text, font);
                    float textX = offset.X - textSize.Width;
                    float textY = y - textSize.Height / 2;
                    g.DrawString(text, font, blackBrush, new PointF(textX, textY));
                }
            }

            // 一番下側の線（濃い目の線で描画）
            g.DrawLine(defaultPen, new Point(offset.X, (int)GetY(MIN_MONEY)), new Point(pictureBoxChart.Width, (int)GetY(MIN_MONEY)));
        }

        // 縦線と横軸のメモリ
        void DrawVirbatileLines(Graphics g)
        {
            var bottom = pictureBoxChart.Height - offset.Y;
            
            // 各年最初のデータのリスト
            List<int> borders = new List<int>();

            int prev = 0;
            foreach (var data in sortedDataList.Select((pair, Index) => new { Year = pair.Date.Year, Index }))
            {
                if (prev != data.Year)
                {
                    borders.Add(data.Index);
                    prev = data.Year;
                }
            }

            // 一番左側の線（濃い目の線で描画）
            g.DrawLine(defaultPen, new Point(offset.X, 0), new Point(offset.X, bottom));
            for (int i = 0; i < borders.Count - 1; i++)
            {
                int left = (int)GetX(borders[i]);
                int right = (int)GetX(borders[i + 1]);

                // 境界線
                g.DrawLine(lightPen, new Point(right, 0), new Point(right, bottom));

                // メモリ（年）
                var text = sortedDataList[borders[i]].Date.Year.ToString();
                var textWidth = g.MeasureString(text, font).Width;
                var textX = (left + right - textWidth) / 2;
                if (textX >= offset.X) g.DrawString(text, font, blackBrush, new PointF(textX, bottom));
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                if (stopWatch == null)
                {
                    stopWatch = Stopwatch.StartNew();
                    scale = 1;
                    graphOffsetUnit = 0;
                    label2.Text = "スペースキーで終了";
                }
                else
                {
                    stopWatch = null;
                    label2.Text = "スペースキーで開始";
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }
    }
}
