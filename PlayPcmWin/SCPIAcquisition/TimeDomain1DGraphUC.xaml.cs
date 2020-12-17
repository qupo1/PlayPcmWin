using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WWMath;

namespace SCPIAcquisition {
    public partial class TimeDomain1DGraphUC : UserControl {
        public int PlotSegmentNum { get; set; }

        private int PLOT_SEGMENT_NUM_DEFAULT = 1000;

        public TimeDomain1DGraphUC() {
            InitializeComponent();

            StartDateTime = System.DateTime.Now;
            PlotSegmentNum = PLOT_SEGMENT_NUM_DEFAULT;
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

        public DateTime StartDateTime {
            get;
            set;
        }

        /// <summary>
        /// グラフの再描画。描画スレッドから呼ぶ必要あり。
        /// </summary>
        public void Redraw() {
            RedrawGraph();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            Redraw();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e) {
            RedrawGraph();
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
            var r = new Rectangle();
            r.Stroke = brush;
            r.Width = w;
            r.Height = h;
            canvas.Children.Add(r);
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
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

        private void DrawText(string text, double fontSize, Brush brush, PivotPosType pp, double pivotX, double pivotY) {
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

        private const double SPACING_X = 20;
        private const double SPACING_Y = 20;
        private const double TEXT_MARGIN = 6;
        private const double DOT_RADIUS = 2.5;

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
        };

        WWVectorD2 PlotValueToGraphPos(WWVectorD2 v, GraphDimension gd) {
            // 0～1の範囲の値。
            double ratioX = (v.X - gd.minValuesXY.X) / (gd.maxValuesXY.X - gd.minValuesXY.X);
            double ratioY = (v.Y - gd.minValuesXY.Y) / (gd.maxValuesXY.Y - gd.minValuesXY.Y);

            double plotX = SPACING_X + (gd.graphWH.X - SPACING_X * 2) * ratioX;
            double plotY = SPACING_Y + (gd.graphWH.Y - SPACING_Y * 2) * (1.0 - ratioY);

            return new WWVectorD2(plotX, plotY);
        }

        private static string FormatNumber(double v, int dispDigits) {
            string unit = "";
            bool bMinus = false;
            if (v < 0) {
                bMinus = true;
                v = -v;
            }
            if (10e15 < v) {
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

        private void RedrawGraph() {
            var fgColor = SystemColors.ControlTextBrush;

            canvas.Children.Clear();

            var graphDimension = new GraphDimension();

            // キャンバスサイズを調べる。
            double W = canvas.ActualWidth;
            double H = canvas.ActualHeight;
            graphDimension.graphWH = new WWVectorD2(W, H);

            // 枠線。
            DrawRectangle(fgColor, SPACING_X, SPACING_Y, W - SPACING_X * 2, H - SPACING_Y * 2);

            // 総数が0
            if (mPlotData.Count == 0) {
                textBlockStartTime.Text = "";
                textBlockCurTime.Text = "";
                textBlockStartTime.Text = string.Format("Start: {0}", System.DateTime.Now.ToString());
                StartDateTime = System.DateTime.Now;
                return;
            }

            textBlockCurTime.Text = string.Format("Last: {0}", System.DateTime.Now.ToString());

            // 最大値、最小値を調べgraphDimensionにセット。
            double xMin = double.MaxValue;
            double yMin = double.MaxValue;
            double xMax = double.MinValue;
            double yMax = double.MinValue;
            foreach (var v in mPlotData) {
                if (v.X < xMin) {
                    xMin = v.X;
                }
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

            graphDimension.minValuesXY = new WWVectorD2(xMin, yMin);
            graphDimension.maxValuesXY = new WWVectorD2(xMax, yMax);

            // 最大値、最小値の文字表示。

            DrawText(string.Format("{0}", FormatNumber(xMin, 4)), 10, fgColor, PivotPosType.Top, SPACING_X, TEXT_MARGIN + H - SPACING_Y);
            DrawText(string.Format("{0}", FormatNumber(xMax, 4)), 10, fgColor, PivotPosType.Top, W - SPACING_X, TEXT_MARGIN + H - SPACING_Y);
            DrawText(string.Format("{0}", FormatNumber(yMin, 4)), 10, fgColor, PivotPosType.Right, SPACING_X - TEXT_MARGIN, H - SPACING_Y);
            DrawText(string.Format("{0}", FormatNumber(yMax, 4)), 10, fgColor, PivotPosType.Right, SPACING_X - TEXT_MARGIN, SPACING_Y);

            // 折れ線描画。

            int step = 1;
            if (PlotSegmentNum < mPlotData.Count) {
                // プロット点が多すぎるとき間引く。
                step = mPlotData.Count / PlotSegmentNum;
            }

            var plPoints = new PointCollection();
            for (int i = 0; i < mPlotData.Count; i += step) {
                var pXY = mPlotData[i];

                var gXY = PlotValueToGraphPos(pXY, graphDimension);
                if (!gXY.IsValid()) {
                    continue;
                }

                plPoints.Add(new Point(gXY.X, gXY.Y));
            }

            var polyline = new Polyline();
            polyline.Stroke = fgColor;
            polyline.Points = plPoints;
            canvas.Children.Add(polyline);

            /*
            // 点をプロット。
            foreach (var pXY in mPlotData) {
                var gXY = PlotValueToGraphPos(pXY, graphDimension);
                if (!gXY.IsValid()) {
                    continue;
                }

                DrawDot(fgColor, DOT_RADIUS, gXY);
            }
            */

        }
    }
}
