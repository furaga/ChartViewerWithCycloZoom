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
    // 楕円

    struct Ellipse
    {
        public double X;        // 中心X座標
        public double Y;        // 中心Y座標
        public double Theta;    // 回転角
        public double A;        // 長径
        public double B;        // 短径
        public double E;        // 離心率

        public Ellipse(double X = 0, double Y = 0, double Theta = 0, double A = 0, double B = 0, double E = 0)
        {
            this.X = X;
            this.Y = Y;
            this.Theta = Theta;
            this.A = A;
            this.B = B;
            this.E = E;
        }
    }

    class CycloZoom
    {
        const int MAX_CAPACITY = 30;
        const int MIN_SQDIR = 25;

        public Point[] stroke = new Point[MAX_CAPACITY];
        int prevIndex = MAX_CAPACITY - 1;
        int curIndex = 0;
        int strokeLength = 0;

        public CycloZoom()
        {

        }

        public bool CheckZoom(Point point, out Ellipse ellipse, out double angle)
        {
            stroke[curIndex] = point;
            if (strokeLength < MAX_CAPACITY) strokeLength++;

            angle = 0;
            ellipse = new Ellipse();

            var curDir = new Point(point.X - stroke[prevIndex].X, point.Y - stroke[prevIndex].Y);

            int cnt = 0;
            int index = 0;
            for (int i = 2; i < strokeLength; i++)
            {
                var index1 = (curIndex + MAX_CAPACITY - i) % MAX_CAPACITY;
                var index2 = (curIndex + MAX_CAPACITY - i + 1) % MAX_CAPACITY;
                var x = stroke[index2].X - stroke[index1].X;
                var y = stroke[index2].Y - stroke[index1].Y;

                if (x * x + y * y < MIN_SQDIR) break;

                var dot = x * curDir.X + y * curDir.Y;

                if ((cnt % 2 == 0 ? dot : -dot) < 0)
                {
                    cnt++;
                    if (cnt >= 2)
                    {
                        index = index1;
                        break;
                    }
                }
            }

            bool ans = false;

            if (cnt >= 2)
            {
                ellipse = GetEllipse(index, curIndex);
                // ellise.Eが小さいほどまん丸に近い
                if (ellipse.E < 0.9)
                {
                    // ズーム
                    double x1 = stroke[prevIndex].X - ellipse.X;
                    double y1 = stroke[prevIndex].Y - ellipse.Y;
                    double x2 = point.X - ellipse.X;
                    double y2 = point.Y - ellipse.Y;

                    double dot = x1 * x2 + y1 * y2;
                    double d = Math.Sqrt((x1 * x1 + y1 * y1) * (x2 * x2 + y2 * y2));

                    angle = Math.Acos(dot / d) * (x1 * y2 - x2 * y1 > 0 ? 1 : -1);
                    ans = true;
                }
            }

            curIndex = (curIndex + 1) % MAX_CAPACITY;
            prevIndex = (curIndex + MAX_CAPACITY - 1) % MAX_CAPACITY;

            return ans;
        }
        const int N = 5;

        Ellipse GetEllipse(int start, int end)
        {
            double[] m = new double[N * N];
            double[] l = new double[N * N];
            double[] u = new double[N * N];
            double[] v = new double[N];
            double[] y = new double[N];
            double[] a = new double[N];

            for (int i = 0; i < m.Length; i++) m[i] = l[i] = u[i] = 0;
            for (int i = 0; i < v.Length; i++) v[i] = y[i] = a[i] = 0;

            for (int i = start; i != end; i = (i + 1) % MAX_CAPACITY)
            {
                double px = stroke[i].X;
                double py = stroke[i].Y;
                double px2 = px * px;
                double py2 = py * py;
                double px3 = px2 * px;
                double py3 = py2 * py;
                double py4 = py2 * py2;

                m[0] += px2 * py2;  m[1] += px * py3;   m[2] += px2 * py;   m[3] += px * py2;   m[4] += px * py;
                m[5] += px * py3;   m[6] += py4;        m[7] += px * py2;   m[8] += py3;        m[9] += py2;
                m[10] += px2 * py;  m[11] += px * py2;  m[12] += px2;       m[13] += px * py;   m[14] += px;
                m[15] += px * py2;  m[16] += py3;       m[17] += px * py;   m[18] += py2;       m[19] += py;
                m[20] += px * py;   m[21] += py2;       m[22] += px;        m[23] += py;        m[24] += 1;

                v[0] -= px3 * py;
                v[1] -= px2 * py2;
                v[2] -= px3;
                v[3] -= px2 * py;
                v[4] -= px2;
            }

            // LU分解
            LU(m, l, u);

            // Ly = v の y を求める
            for (int i = 0; i < 5; i++)
            {
                double s = 0.0;
                for (int j = 0; j < i; j++) s += l[i * N + j] * y[j];
                y[i] = (v[i] - s) / l[i * N + i];
            }
            // Ua = y の a を求める
            for (int i = 4; i >= 0; i--)
            {
                double s = 0.0;
                for (int j = 4; j > i; j--) s += u[i * N + j] * a[j];
                a[i] = (y[i] - s) / u[i * N + i];
            }

            double x0 = (a[0] * a[3] - 2 * a[1] * a[2]) / (4 * a[1] - a[0] * a[0]);
            double y0 = (a[0] * a[2] - 2 * a[3]) / (4 * a[1] - a[0] * a[0]);
            double theta = Math.Atan(a[0] / (1.0 - a[1])) / 2.0;
            double sin = Math.Sin(theta), cos = Math.Cos(theta);
            double r1 = Math.Sqrt(
                (x0 * cos + y0 * sin) * (x0 * cos + y0 * sin) -
                a[4] * cos * cos -
                ((x0 * sin - y0 * cos) * (x0 * sin - y0 * cos) - a[4] * sin * sin) * (sin * sin - a[1] * cos * cos) / (cos * cos - a[1] * sin * sin)
            );
            double r2 = Math.Sqrt(
                (x0 * sin - y0 * cos) * (x0 * sin - y0 * cos) -
                a[4] * sin * sin -
                ((x0 * cos + y0 * sin) * (x0 * cos + y0 * sin) - a[4] * cos * cos) * (cos * cos - a[1] * sin * sin) / (sin * sin - a[1] * cos * cos)
            );

            double maxR = Math.Max(r1, r2);
            double minR = Math.Min(r1, r2);
            double e = Math.Sqrt(Math.Abs(maxR * maxR - minR * minR) / (maxR * maxR));
            var ellipse = new Ellipse(x0, y0, theta, maxR, minR, e);
            return ellipse;
        }

        void LU(double[] A, double[] L, double[] U)
        {
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    L[i * N + j] = 0.0;
                    U[i * N + j] = 0.0;
                    if (i == j) L[i * N + j] = 1.0;
                }
            }

            double sum;
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    if (i > j)
                    {
                        sum = 0.0;
                        for (int k = 0; k < j; k++)
                        {
                            sum += L[i * N + k] * U[k * N + j];
                        }
                        L[i * N + j] = (A[i * N + j] - sum) / U[j * N + j];
                    }
                    else
                    {
                        sum = 0.0;
                        for (int k = 0; k < i; k++)
                        {
                            sum += L[i * N + k] * U[k * N + j];
                        }
                        U[i * N + j] = A[i * N + j] - sum;
                    }
                }
            }
        }
    }
}
