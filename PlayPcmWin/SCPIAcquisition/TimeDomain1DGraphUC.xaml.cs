using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WWMath;

namespace SCPIAcquisition {
    public partial class TimeDomain1DGraphUC : UserControl {
        private bool mInitialized = false;

        private int PLOT_SEGMENT_NUM_DEFAULT = 1000;
        private int PLOT_POINT_MAX = 75;
        private double PLOT_RATIO_THRESHOLD = 5;

        private int GRID_Y = 4;

        private const double TICK_SIZE = 3;

        private const double SPACING_X = 20;
        private const double SPACING_Y = 20;
        private const double TEXT_MARGIN = 6;
        private const double DOT_RADIUS = 1.5;


        public TimeDomain1DGraphUC() {
            InitializeComponent();

            ShowGrid = true;
            ShowStartEndTime = true;
            StartDateTime = System.DateTime.Now;
            PlotSegmentNum = PLOT_SEGMENT_NUM_DEFAULT;
        }

        private void textBlockStartTime_Loaded(object sender, RoutedEventArgs e) {
            mInitialized = true;

            LocalizeUI();

            Redraw();
        }

        private void LocalizeUI() {
            groupBoxGraphSettings.Header = Properties.Resources.Settings;
            checkBoxGrid.Content = Properties.Resources.ShowGrid;
            checkBoxTime.Content = Properties.Resources.ShowStartEndTime;
        }

        public string GraphTitle {
            get { return textBlockTitle.Text; }
            set { textBlockTitle.Text = value; }
        }

        public string XAxisText {
            get { return textBlockXAxis.Text; }
            set { textBlockXAxis.Text = value; }
        }

        public string YAxisText {
            get { return textBlockYAxis.Text; }
            set { textBlockYAxis.Text = value; }
        }

        /// <summary>
        /// 折れ線グラフの頂点最大数。プロットデータ数がこれを超えた場合描画する頂点を間引く。
        /// </summary>
        public int PlotSegmentNum { get; set; }

        /// <summary>
        /// 枠線の表示。
        /// </summary>
        public bool ShowGrid { get; set; }

        /// <summary>
        /// 開始終了時刻表示。
        /// </summary>
        public bool ShowStartEndTime { get; set; }

        /// <summary>
        /// 計測開始時刻。
        /// </summary>
        public DateTime StartDateTime { get; set; }

        List<WWVectorD2> mPlotData = new List<WWVectorD2>();

        /// <summary>
        /// プロット値をすべて削除する。グラフの再描画は行わない。
        /// </summary>
        public void Clear() {
            mPlotData.Clear();
        }

        /// <summary>
        /// プロット値を追加。グラフの再描画は行わない。
        /// </summary>
        public void Add(WWVectorD2 v) {
            mPlotData.Add(v);
        }

        /// <summary>
        /// プロットデータ取得。
        /// </summary>
        public List<WWVectorD2> PlotData() {
            return mPlotData;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            Redraw();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e) {
            Redraw();
        }

        private void DrawLine(Brush brush, double x1, double y1, double x2, double y2) {
            var l = new Line();
            l.X1 = x1;
            l.X2 = x2;
            l.Y1 = y1;
            l.Y2 = y2;
            l.Stroke = brush;
            canvas.Children.Add(l);
        }

        private void DrawLine(Brush brush, WWVectorD2 xy1, WWVectorD2 xy2) {
            DrawLine(brush, xy1.X, xy1.Y, xy2.X, xy2.Y);
        }

        private void DrawRectangle(Brush brush, double x, double y, double w, double h) {
            DrawLine(brush, new WWVectorD2(x, y), new WWVectorD2(x+w, y));
            DrawLine(brush, new WWVectorD2(x+w, y), new WWVectorD2(x + w, y+h));
            DrawLine(brush, new WWVectorD2(x + w, y+h), new WWVectorD2(x, y + h));
            DrawLine(brush, new WWVectorD2(x, y + h), new WWVectorD2(x, y));
        }

        private void DrawDot(Brush brush, double radius, WWVectorD2 xy) {
            var e = new Ellipse();
            e.Width = radius * 2;
            e.Height = radius * 2;
            e.Fill = brush;
            canvas.Children.Add(e);
            Canvas.SetLeft(e, xy.X - radius);
            Canvas.SetTop(e, xy.Y - radius);
        }

        enum PivotPosType {
            LeftTop,
            Left,
            Top,
            Right,
            Bottom
        };

        private void DrawText(Brush brush, string text, double fontSize, PivotPosType pp, double pivotX, double pivotY) {
            var tb = new TextBlock();
            tb.Text = text;
            tb.FontSize = FontSize;
            tb.Foreground = brush;
            tb.Measure(new Size(1000, 1000));
            var tbWH = tb.DesiredSize;

            double x = pivotX;
            double y = pivotY;
            switch (pp) {
            case PivotPosType.LeftTop:
                // テキストの左上座標が指定された場合。
                break;
            case PivotPosType.Left:
                // テキストの左の座標が指定された。
                y -= tbWH.Height / 2;
                break;
            case PivotPosType.Top:
                // テキストの上の座標が指定された。
                x -= tbWH.Width / 2;
                break;
            case PivotPosType.Right:
                // テキストの右の座標が指定された。
                x -= tbWH.Width;
                y -= tbWH.Height / 2;
                break;
            case PivotPosType.Bottom:
                // テキストの下の座標が指定された。
                x -= tbWH.Width / 2;
                y -= tbWH.Height;
                break;
            }

            canvas.Children.Add(tb);
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
        }

        static double RoundToSignificantDigits(double d, int digits) {
            if (d == 0)
                return 0;

            double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(d))) + 1);
            return scale * Math.Round(d / scale, digits);
        }

        class GraphDimension {
            public WWVectorD2 graphWH;
            public WWVectorD2 minValuesXY;
            public WWVectorD2 maxValuesXY;

            public double gridStepX;
        };

        WWVectorD2 PlotValueToGraphPos(WWVectorD2 v, GraphDimension gd) {
            // 0～1の範囲の値。
            double ratioX = (v.X - gd.minValuesXY.X) / (gd.maxValuesXY.X - gd.minValuesXY.X);
            double ratioY = (v.Y - gd.minValuesXY.Y) / (gd.maxValuesXY.Y - gd.minValuesXY.Y);

            double plotX = SPACING_X + (gd.graphWH.X - SPACING_X * 2) * ratioX;
            double plotY = SPACING_Y + (gd.graphWH.Y - SPACING_Y * 2) * (1.0 - ratioY);

            return new WWVectorD2(plotX, plotY);
        }

        private static string FormatTime(double v) {
            string unit = "sec";
            bool bMinus = false;
            if (v < 0) {
                bMinus = true;
                v = -v;
            }

            if (v == 0) {
                return "0";
            }

            if (v < 1) {
                unit = "ms";
                v *= 1000;
            } else if (v < 60) {
                unit = Properties.Resources.Graph_Second;
            } else if (v < 3600) {
                unit = Properties.Resources.Graph_Minute;
                v /= 60;
            } else if (v < 3600 * 24) {
                unit = Properties.Resources.Graph_Hour;
                v /= 3600;
            } else if (v < 3600 * 24 * 7) {
                unit = Properties.Resources.Graph_Day;
                v /= 3600 * 24;
            } else {
                unit = Properties.Resources.Graph_Week;
                v /= 3600 * 24 * 7;
            }

            if (v < 10) {
                return string.Format("{0}{1:0.000} {2}", bMinus ? "-" : "", v, unit);
            }
            if (v < 100) {
                return string.Format("{0}{1:0.00} {2}", bMinus ? "-" : "", v, unit);
            }
            return string.Format("{0}{1:0.0} {2}", bMinus ? "-" : "", v, unit);
        }

        private static string FormatNumber(double v, int dispDigits) {
            string unit = "";
            bool bMinus = false;
            if (v < 0) {
                bMinus = true;
                v = -v;
            }

            if (v < 10e-15) {
                return "0";
            } else if (10e15 < v) {
                return string.Format("{0} ∞ ", bMinus ? "-" : "");
            } else if (v < 0.001 * 0.001 * 0.001) {
                unit = "p";
                v *= 1000.0 * 1000 * 1000 * 1000;
            } else if (v < 0.001 * 0.001) {
                unit = "n";
                v *= 1000.0 * 1000 * 1000;
            } else if (v < 0.001) {
                unit = "μ";
                v *= 1000.0 * 1000;
            } else if (v < 1) {
                unit = "m";
                v *= 1000.0;
            } else if (1000.0 * 1000 * 1000 <= v) {
                unit = "G";
                v /= 1000.0 * 1000 * 1000;
            } else if (1000.0 * 1000 <= v) {
                unit = "M";
                v /= 1000.0 * 1000;
            } else if (1000.0 <= v) {
                unit = "k";
                v /= 1000.0;
            }

            switch (dispDigits) {
            case 4:
                if (v < 10) {
                    return string.Format("{0}{1:0.000} {2}", bMinus ? "-" : "", v, unit);
                }
                if (v < 100) {
                    return string.Format("{0}{1:0.00} {2}", bMinus ? "-" : "", v, unit);
                }
                return string.Format("{0}{1:0.0} {2}", bMinus ? "-" : "", v, unit);
            case 5:
                if (v < 10) {
                    return string.Format("{0}{1:0.0000} {2}", bMinus ? "-" : "", v, unit);
                }
                if (v < 100) {
                    return string.Format("{0}{1:0.000} {2}", bMinus ? "-" : "", v, unit);
                }
                return string.Format("{0}{1:0.00} {2}", bMinus ? "-" : "", v, unit);
            case 6:
                if (v < 10) {
                    return string.Format("{0}{1:0.00000} {2}", bMinus ? "-" : "", v, unit);
                }
                if (v < 100) {
                    return string.Format("{0}{1:0.0000} {2}", bMinus ? "-" : "", v, unit);
                }
                return string.Format("{0}{1:0.000} {2}", bMinus ? "-" : "", v, unit);
            case 7:
                if (v < 10) {
                    return string.Format("{0}{1:0.000000} {2}", bMinus ? "-" : "", v, unit);
                }
                if (v < 100) {
                    return string.Format("{0}{1:0.00000} {2}", bMinus ? "-" : "", v, unit);
                }
                return string.Format("{0}{1:0.0000} {2}", bMinus ? "-" : "", v, unit);
            case 8:
                if (v < 10) {
                    return string.Format("{0}{1:0.0000000} {2}", bMinus ? "-" : "", v, unit);
                }
                if (v < 100) {
                    return string.Format("{0}{1:0.000000} {2}", bMinus ? "-" : "", v, unit);
                }
                return string.Format("{0}{1:0.00000} {2}", bMinus ? "-" : "", v, unit);
            default:
                throw new ArgumentException();
            }
        }

        private class FirstDigitAndExponent {
            public int firstDigit;
            public int exponent;
            public FirstDigitAndExponent(int aFirstDigit, int aExponent) {
                firstDigit = aFirstDigit;
                exponent = aExponent;
            }
        };
        private static FirstDigitAndExponent CalcFirstDigit(double v) {
            v = Math.Abs(v);

            if (v < 10e-15) {
                return new FirstDigitAndExponent(1, -15);
            }
            if (10e15 < v) {
                return new FirstDigitAndExponent(1, 15);
            }

            int exponent = 0;

            while (10.0 <= v) {
                v /= 10.0;
                ++exponent;
            }
            while (v < 1.0) {
                v *= 10.0;
                --exponent;
            }

            return new FirstDigitAndExponent((int)v, exponent);
        }

        private static void CalcGraphGridStep(GraphDimension gd) {

            // x軸のステップ。
            double maxX = gd.maxValuesXY.X;
            if (maxX < 6) {
                // 6秒以下：1秒毎。
                gd.gridStepX = 1;
            } else if (maxX < 15) {
                // 15秒以下：2秒毎。
                gd.gridStepX = 2;
            } else if (maxX < 30) {
                // 30秒以下：5秒毎。
                gd.gridStepX = 5;
            } else if (maxX < 60) {
                // 1分以下：10秒毎。
                gd.gridStepX = 10;
            } else if (maxX < 60*3) {
                // 3分以下：30秒毎。
                gd.gridStepX = 30;
            } else if (maxX < 60*6) {
                // 6分以下：1分毎。
                gd.gridStepX = 60;
            } else if (maxX < 60*15) {
                // 15分以下：3分毎。
                gd.gridStepX = 60 * 3;
            } else if (maxX < 60*30) {
                // 30分以下：5分毎。
                gd.gridStepX = 60 * 5;
            } else if (maxX < 3600) {
                // 1時間以下：10分毎。
                gd.gridStepX = 60 * 10;
            } else if (maxX < 3600*3) {
                // 3時間以下：30分毎。
                gd.gridStepX = 60 * 30;
            } else if (maxX < 3600 * 6) {
                // 6時間以下：1時間毎。
                gd.gridStepX = 3600;
            } else if (maxX < 3600 * 12) {
                // 12時間以下：2時間毎。
                gd.gridStepX = 3600 * 2;
            } else if (maxX < 3600 * 24) {
                // 1日以下：4時間毎。
                gd.gridStepX = 3600 * 4;
            } else if (maxX < 3600 * 24 * 3) {
                // 3日以下：12時間毎。
                gd.gridStepX = 3600 * 12;
            } else if (maxX < 3600 * 24 * 7) {
                // 1週間以下：1日毎。
                gd.gridStepX = 3600 * 24;
            } else {
                // 1週間以上：1週間毎。
                gd.gridStepX = 3600 * 24 * 7;
            }
        }

        /// <summary>
        /// グラフの再描画。描画スレッドから呼ぶ必要あり。
        /// </summary>
        public void Redraw() {
            var fgColor = SystemColors.ControlTextBrush;
            var gridColor = SystemColors.ControlDarkBrush;

            canvas.Children.Clear();

            var gd = new GraphDimension();

            // キャンバスサイズを調べる。
            double W = canvas.ActualWidth;
            double H = canvas.ActualHeight;
            gd.graphWH = new WWVectorD2(W, H);

            // 枠線。
            DrawRectangle(gridColor, SPACING_X, SPACING_Y, W - SPACING_X * 2, H - SPACING_Y * 2);

            // 総数が0
            if (mPlotData.Count == 0) {
                textBlockStartTime.Text = "";
                textBlockCurTime.Text = "";
                textBlockStartTime.Text = string.Format("{0}: {1}",Properties.Resources.Start, System.DateTime.Now.ToString());
                StartDateTime = System.DateTime.Now;
                return;
            }

            textBlockCurTime.Text = string.Format("{0}: {1}",Properties.Resources.Last, System.DateTime.Now.ToString());

            // 最大値、最小値を調べgraphDimensionにセット。
            double xMin = 0;
            double yMin = double.MaxValue;
            double xMax = double.MinValue;
            double yMax = double.MinValue;
            foreach (var v in mPlotData) {
                if (v.Y < yMin) {
                    yMin = v.Y;
                }
                if (xMax < v.X) {
                    xMax = v.X;
                }
                if (yMax < v.Y) {
                    yMax = v.Y;
                }
            }

            // 表示の都合上、最大値と最小値を異なる値にする。
            if (xMin == xMax) {
                xMax = xMin + 1;
            }
            if (yMin == yMax) {
                yMax = yMin + 1;
            }

            if (double.Epsilon < yMin && PLOT_RATIO_THRESHOLD < yMax / yMin) {
                // yMinが正の値で、yMinとyMaxの比が3倍以上の場合yMinを0にする。
                yMin = 0;
            } else if (yMax < -double.Epsilon && PLOT_RATIO_THRESHOLD < yMin / yMax) {
                // yMaxが負の値で、yMinとyMaxの比が3倍以上の場合yMaxを0にする。
                yMax = 0;
            }


            gd.minValuesXY = new WWVectorD2(xMin, yMin);
            gd.maxValuesXY = new WWVectorD2(xMax, yMax);

            // グラフのステップの計算。
            CalcGraphGridStep(gd);
            
            // グリッド縦線を切りの良い位置に引く。
            for (int i=0; i<xMax/gd.gridStepX; ++i) {
                DrawText(fgColor, string.Format("{0}", FormatTime(xMin + gd.gridStepX * i)), 10, PivotPosType.Top,
                                   SPACING_X + (W - SPACING_X * 2) * i * gd.gridStepX / (xMax-xMin), TEXT_MARGIN + H - SPACING_Y);
                if (ShowGrid) {
                    DrawLine(gridColor,
                        new WWVectorD2(SPACING_X + (W - SPACING_X * 2) * i * gd.gridStepX / (xMax - xMin), SPACING_Y),
                        new WWVectorD2(SPACING_X + (W - SPACING_X * 2) * i * gd.gridStepX / (xMax - xMin), H - SPACING_Y + TICK_SIZE));
                } else {
                    // グリッド線を表示しない場合、Tickを表示する。
                    DrawLine(gridColor,
                        new WWVectorD2(SPACING_X + (W - SPACING_X * 2) * i * gd.gridStepX / (xMax - xMin), H - SPACING_Y),
                        new WWVectorD2(SPACING_X + (W - SPACING_X * 2) * i * gd.gridStepX / (xMax - xMin), H - SPACING_Y + TICK_SIZE));
                }
            }

            // グリッド横線。
            for (int i = 0; i <= GRID_Y; ++i) {
                DrawText(fgColor, string.Format("{0}", FormatNumber(yMin + (yMax - yMin)*i/GRID_Y, 6)), 10, PivotPosType.Right, 
                                   SPACING_X - TEXT_MARGIN, H - SPACING_Y - (H - SPACING_Y * 2) * i / GRID_Y);

                if (ShowGrid) {
                    DrawLine(gridColor,
                        new WWVectorD2(SPACING_X -TICK_SIZE, H - SPACING_Y - (H - SPACING_Y * 2) * i / GRID_Y),
                        new WWVectorD2(W - SPACING_X, H - SPACING_Y - (H - SPACING_Y * 2) * i / GRID_Y));
                } else {
                    // グリッド線を表示しない場合、Tickを表示する。
                    DrawLine(gridColor,
                        new WWVectorD2(SPACING_X-TICK_SIZE, H - SPACING_Y - (H - SPACING_Y * 2) * i / GRID_Y),
                        new WWVectorD2(SPACING_X, H - SPACING_Y - (H - SPACING_Y * 2) * i / GRID_Y));
                }
            }

            // 折れ線描画。

            int step = 1;
            if (PlotSegmentNum < mPlotData.Count) {
                // プロット点が多すぎるとき間引く。
                step = mPlotData.Count / PlotSegmentNum;
            }

            var plPoints = new PointCollection();
            for (int i = 0; i < mPlotData.Count; i += step) {
                var pXY = mPlotData[i];

                var gXY = PlotValueToGraphPos(pXY, gd);
                if (!gXY.IsValid()) {
                    continue;
                }

                plPoints.Add(new Point(gXY.X, gXY.Y));
            }

            var polyline = new Polyline();
            polyline.Stroke = fgColor;
            polyline.Points = plPoints;
            canvas.Children.Add(polyline);

            if (mPlotData.Count <= PLOT_POINT_MAX) {
                // 点をプロット。
                foreach (var pXY in mPlotData) {
                    var gXY = PlotValueToGraphPos(pXY, gd);
                    if (!gXY.IsValid()) {
                        continue;
                    }

                    DrawDot(fgColor, DOT_RADIUS, gXY);
                }
            }
        }

        private void checkBoxGrid_Checked(object sender, RoutedEventArgs e) {
            ShowGrid = true;

            if (!mInitialized) {
                return;
            }

            Redraw();
        }

        private void checkBoxGrid_Unchecked(object sender, RoutedEventArgs e) {
            ShowGrid = false;

            if (!mInitialized) {
                return;
            }

            Redraw();
        }

        private void checkBoxTime_Checked(object sender, RoutedEventArgs e) {
            ShowStartEndTime = true;

            if (!mInitialized) {
                return;
            }

            textBlockStartTime.Visibility = System.Windows.Visibility.Visible;
            textBlockCurTime.Visibility = System.Windows.Visibility.Visible;
        }

        private void checkBoxTime_Unchecked(object sender, RoutedEventArgs e) {
            ShowStartEndTime = false;

            if (!mInitialized) {
                return;
            }

            textBlockStartTime.Visibility = System.Windows.Visibility.Collapsed;
            textBlockCurTime.Visibility = System.Windows.Visibility.Collapsed;
        }

    }
}
