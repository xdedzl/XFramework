using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace XFramework.Mathematics
{
    /// <summary>
    /// 和2d数学相关的算法
    /// </summary>
    public static class Math2d
    {

        #region 向量

        /// <summary>
        /// 点绕点旋转
        /// </summary>
        /// <param name="_orgPos">中心点</param>
        /// <param name="_tarPos">需要旋转的点</param>
        /// <param name="_angle">角度逆时针</param>
        /// <returns></returns>
        public static Vector2 GetTargetVector(Vector2 _orgPos, Vector2 _tarPos, float _angle)
        {
            return GetTargetVector(_tarPos - _orgPos, _angle) + _orgPos;
        }

        /// <summary>
        /// 获取平面向量向左旋转theta后的目标向量
        /// </summary>
        /// <param name="startVector"></param>
        /// <param name="theta"></param>
        /// <returns></returns>
        public static Vector2 GetTargetVector(Vector2 startVector, float theta = 90)
        {
            float x = startVector.x * Mathf.Cos(theta * Mathf.Deg2Rad) - startVector.y * Mathf.Sin(theta * Mathf.Deg2Rad);
            float y = startVector.x * Mathf.Sin(theta * Mathf.Deg2Rad) + startVector.y * Mathf.Cos(theta * Mathf.Deg2Rad);
            return new Vector2(x, y);
        }

        /// <summary>
        /// 获取一个向量的垂直向量（顺时针）
        /// </summary>
        /// <param name="_dir">方向</param>
        /// <returns>垂直方向</returns>
        public static Vector2 GetHorizontalDir(Vector2 _dir)
        {
            return new Vector2(_dir.y, -_dir.x).normalized;
        }

        /// <summary>
        /// 获取两点组成的向量的水平垂直向量（忽略Y轴，顺时针）
        /// </summary>
        /// <param name="_start">起始点</param>
        /// <param name="_end">终止点</param>
        /// <returns>垂直向量</returns>
        public static Vector3 GetHorizontalDir(Vector3 _start, Vector3 _end)
        {
            Vector3 _dirValue = (_end - _start);
            return GetHorizontalDir(_dirValue);
        }

        /// <summary>
        /// 获取一个向量的水平垂直向量（忽略y轴，顺时针）
        /// </summary>
        /// <param name="_dirValue"></param>
        /// <returns></returns>
        public static Vector3 GetHorizontalDir(Vector3 _dirValue)
        {
            Vector3 returnVec = new Vector3(_dirValue.z, 0, -_dirValue.x);
            return returnVec.normalized;
        }

        #endregion

        #region 线段相关

        /// <summary>
        /// 判断两线段的是否相交
        /// </summary>
        /// <param name="intersection">交点</param>
        /// <param name="point_0"></param>
        /// <param name="point_1"></param>
        /// <param name="point_2"></param>
        /// <param name="point_3"></param>
        /// <returns></returns>
        public static bool SegmentIntersection(out Vector2 intersection, Vector2 p0, Vector2 p1, Vector2 q0, Vector2 q1)
        {
            // 判断相交性
            Vector3 vec_0 = q1 - q0;   // 线段q的方向
            Vector3 vec_1 = q0 - p0;   // 线段q到p两个端点连线的方向
            Vector3 vec_2 = q0 - p1;
            if (Vector3.Dot(Vector3.Cross(vec_0, vec_1), Vector3.Cross(vec_0, vec_2)) > 0)
            {
                intersection = Vector2.zero;
                return false;
            }

            intersection = Vector2.zero;
            return false;
        }

        #endregion

        #region 二维数组

        /// <summary>
        /// 高斯核的静态存储
        /// </summary>
        private static Dictionary<int, float[,]> gaussianCoreDic = new Dictionary<int, float[,]>();

        /// <summary>
        /// 双线性差值
        /// </summary>
        /// <param name="array_In"></param>
        /// <param name="newWidth"></param>
        /// <param name="newHeight"></param>
        /// <returns></returns>
        public static async Task<float[,]> ZoomBilinearInterpAsync(float[,] array_In, int newWidth, int newHeight)
        {
            int originalHeight = array_In.GetLength(0);
            int originalWidth = array_In.GetLength(1);

            float scaleX = ((float)newHeight) / ((float)originalHeight);
            float scaleY = ((float)newWidth) / ((float)originalWidth);

            float[,] array_Out = new float[newHeight, newWidth];
            float u = 0, v = 0, x = 0, y = 0;
            int m = 0, n = 0;
            int i, j;
            await Task.Run(async () =>
            {
                for (i = 0; i < newHeight; ++i)
                {
                    await Task.Run(() =>
                    {
                        for (j = 0; j < newWidth; ++j)
                        {
                            x = i / scaleX;
                            y = j / scaleY;

                            m = Mathf.FloorToInt(x);
                            n = Mathf.FloorToInt(y);

                            u = x - m;
                            v = y - n;

                            if (m < originalHeight - 1 && n < originalWidth - 1)
                            {

                                array_Out[i, j] = (1.0f - v) * (1.0f - u) * array_In[m, n] + (1 - v) * u * array_In[m, n + 1]
                                                       + v * (1.0f - u) * array_In[m + 1, n] + v * u * array_In[m + 1, n + 1];
                            }
                            else
                            {
                                array_Out[i, j] = array_In[m, n];
                            }
                        }

                    });
                }
            }
            );
            Debug.Log("新矩阵计算终了");
            return array_Out;
        }

        /// <summary>
        /// 双线性差值
        /// </summary>
        /// <param name="array_In"></param>
        /// <param name="newWidth"></param>
        /// <param name="newHeight"></param>
        /// <returns></returns>
        public static float[,] ZoomBilinearInterp(float[,] array_In, int newWidth, int newHeight)
        {
            int originalHeight = array_In.GetLength(0);
            int originalWidth = array_In.GetLength(1);

            float scaleX = ((float)newHeight) / ((float)originalHeight);
            float scaleY = ((float)newWidth) / ((float)originalWidth);

            float[,] array_Out = new float[newHeight, newWidth];
            float u = 0, v = 0, x = 0, y = 0;
            int m = 0, n = 0;
            int i, j;
            for (i = 0; i < newHeight; ++i)
            {
                for (j = 0; j < newWidth; ++j)
                {
                    x = i / scaleX;
                    y = j / scaleY;

                    m = Mathf.FloorToInt(x);
                    n = Mathf.FloorToInt(y);

                    u = x - m;
                    v = y - n;

                    if (m < originalHeight - 1 && n < originalWidth - 1)
                    {

                        array_Out[i, j] = (1.0f - v) * (1.0f - u) * array_In[m, n] + (1 - v) * u * array_In[m, n + 1]
                                               + v * (1.0f - u) * array_In[m + 1, n] + v * u * array_In[m + 1, n + 1];
                    }
                    else
                    {
                        array_Out[i, j] = array_In[m, n];
                    }
                }
            }

            return array_Out;
        }

        /// <summary>
        /// 双线性差值
        /// </summary>
        /// <param name="array"></param>
        /// <param name="length_0"></param>
        /// <param name="length_1"></param>
        /// <returns></returns>
        public static async Task<float[,]> BilinearInterpAsync(float[,] array, int length_0, int length_1)
        {
            float[,] _out = new float[length_0, length_1];
            int original_0 = array.GetLength(0);
            int original_1 = array.GetLength(1);

            float ReScale_0 = original_0 / ((float)length_0);  // 倍数的倒数
            float ReScale_1 = original_1 / ((float)length_1);

            float index_0;
            float index_1;
            int inde_0;
            int inde_1;
            float s_leftUp;
            float s_rightUp;
            float s_rightDown;
            float s_leftDown;

            await Task.Run(async () =>
            {
                for (int i = 0; i < length_0; i++)
                {
                    await Task.Run(() =>
                    {
                        for (int j = 0; j < length_1; j++)
                        {
                            index_0 = i * ReScale_0;
                            index_1 = j * ReScale_1;
                            inde_0 = Mathf.FloorToInt(index_0);
                            inde_1 = Mathf.FloorToInt(index_1);
                            s_leftUp = (index_0 - inde_0) * (index_1 - inde_1);
                            s_rightUp = (inde_0 + 1 - index_0) * (index_1 - inde_1);
                            s_rightDown = (inde_0 + 1 - index_0) * (inde_1 + 1 - index_1);
                            s_leftDown = (index_0 - inde_0) * (inde_1 + 1 - index_1);
                            _out[i, j] = array[inde_0, inde_1] * s_rightDown + array[inde_0 + 1, inde_1] * s_leftDown + array[inde_0 + 1, inde_1 + 1] * s_leftUp + array[inde_0, inde_1 + 1] * s_rightUp;
                        }
                    });
                }
            });

            return _out;
        }

        /// <summary>
        /// 双线性差值
        /// </summary>
        /// <param name="array"></param>
        /// <param name="length_0"></param>
        /// <param name="length_1"></param>
        /// <returns></returns>
        public static float[,] BilinearInterp(float[,] array, int length_0, int length_1)
        {
            float[,] _out = new float[length_0, length_1];
            int original_0 = array.GetLength(0);
            int original_1 = array.GetLength(1);

            float ReScale_0 = original_0 / ((float)length_0);  // 倍数的倒数
            float ReScale_1 = original_1 / ((float)length_1);

            float index_0;
            float index_1;
            int inde_0;
            int inde_1;
            float s_leftUp;
            float s_rightUp;
            float s_rightDown;
            float s_leftDown;

            for (int i = 0; i < length_0; i++)
            {
                for (int j = 0; j < length_1; j++)
                {
                    index_0 = i * ReScale_0;
                    index_1 = j * ReScale_1;
                    inde_0 = Mathf.FloorToInt(index_0);
                    inde_1 = Mathf.FloorToInt(index_1);
                    s_leftUp = (index_0 - inde_0) * (index_1 - inde_1);
                    s_rightUp = (inde_0 + 1 - index_0) * (index_1 - inde_1);
                    s_rightDown = (inde_0 + 1 - index_0) * (inde_1 + 1 - index_1);
                    s_leftDown = (index_0 - inde_0) * (inde_1 + 1 - index_1);

                    try
                    {
                        _out[i, j] = array[inde_0, inde_1] * s_rightDown + array[inde_0 + 1, inde_1] * s_leftDown + array[inde_0 + 1, inde_1 + 1] * s_leftUp + array[inde_0, inde_1 + 1] * s_rightUp;
                    }
                    catch (Exception)
                    {

                        throw;
                    }

                }
            }

            return _out;
        }

        /// <summary>
        /// 对二维数组做高斯模糊
        /// </summary>
        /// <param name="array">要处理的数组</param>
        /// <param name="dev"></param>
        /// <param name="r">高斯核扩展半径</param>
        /// <param name="isCircle">改变形状是否是圆</param>
        public async static Task GaussianBlurAsync(float[,] array, float dev, int r = 1, bool isCircle = true)
        {
            // 构造或者从字典中拿取半径为r的高斯核
            int length = r * 2 + 1;
            float[,] gaussianCore;
            if (gaussianCoreDic.ContainsKey(r) && dev == 1.5)
            {
                gaussianCore = gaussianCoreDic[r];
            }
            else
            {
                gaussianCore = new float[length, length];
                float k = 1 / (2 * Mathf.PI * dev * dev);
                for (int i = 0; i < length; i++)
                {
                    for (int j = 0; j < length; j++)
                    {
                        float pow = -((j - r) * (j - r) + (i - r) * (i - r)) / (2 * dev * dev);
                        gaussianCore[i, j] = k * Mathf.Pow(2.71828f, pow);
                    }
                }

                // 使权值和为1
                float sum = 0;
                for (int i = 0; i < length; i++)
                {
                    for (int j = 0; j < length; j++)
                    {
                        sum += gaussianCore[i, j];
                    }
                }
                for (int i = 0; i < length; i++)
                {
                    for (int j = 0; j < length; j++)
                    {
                        gaussianCore[i, j] /= sum;
                    }
                }

                if (dev == 1.5 && !gaussianCoreDic.ContainsKey(r))
                    gaussianCoreDic.Add(r, gaussianCore);
            }

            // 对二维数组进行高斯模糊处理
            int circleR = array.GetLength(0) / 2;

            for (int i = r, length_0 = array.GetLength(0) - r; i < length_0; i++)
            {
                await Task.Run(() =>
                {
                    for (int j = r, length_1 = array.GetLength(1) - r; j < length_1; j++)
                    {
                        if (isCircle && (i - circleR) * (i - circleR) + (j - circleR) * (j - circleR) > (circleR - r) * (circleR - r))
                            continue;

                        // 用高斯核处理一个值
                        float value = 0;
                        for (int u = 0; u < length; u++)
                        {
                            for (int v = 0; v < length; v++)
                            {
                                if ((i + u - r) >= array.GetLength(0) || (i + u - r) < 0 || (j + v - r) >= array.GetLength(1) || (j + v - r) < 0)
                                    Debug.LogError("滴嘟滴嘟的报错");
                                else
                                    value += gaussianCore[u, v] * array[i + u - r, j + v - r];
                            }
                        }
                        array[i, j] = value;
                    }
                });
            }
        }

        /// <summary>
        /// 对二维数组做高斯模糊
        /// </summary>
        /// <param name="array">要处理的数组</param>
        /// <param name="dev"></param>
        /// <param name="r">高斯核扩展半径</param>
        /// <param name="isCircle">改变形状是否是圆</param>
        public static void GaussianBlur(float[,] array, float dev, int r = 1, bool isCircle = true)
        {
            // 构造或者从字典中拿取半径为r的高斯核
            int length = r * 2 + 1;
            float[,] gaussianCore;
            if (gaussianCoreDic.ContainsKey(r) && dev == 1.5)
            {
                gaussianCore = gaussianCoreDic[r];
            }
            else
            {
                gaussianCore = new float[length, length];
                float k = 1 / (2 * Mathf.PI * dev * dev);
                for (int i = 0; i < length; i++)
                {
                    for (int j = 0; j < length; j++)
                    {
                        float pow = -((j - r) * (j - r) + (i - r) * (i - r)) / (2 * dev * dev);
                        gaussianCore[i, j] = k * Mathf.Pow(2.71828f, pow);
                    }
                }

                // 使权值和为1
                float sum = 0;
                for (int i = 0; i < length; i++)
                {
                    for (int j = 0; j < length; j++)
                    {
                        sum += gaussianCore[i, j];
                    }
                }
                for (int i = 0; i < length; i++)
                {
                    for (int j = 0; j < length; j++)
                    {
                        gaussianCore[i, j] /= sum;
                    }
                }

                if (dev == 1.5 && !gaussianCoreDic.ContainsKey(r))
                    gaussianCoreDic.Add(r, gaussianCore);
            }

            // 对二维数组进行高斯模糊处理
            int circleR = array.GetLength(0) / 2;

            for (int i = r, length_0 = array.GetLength(0) - r; i < length_0; i++)
            {
                for (int j = r, length_1 = array.GetLength(1) - r; j < length_1; j++)
                {
                    // 限制圆
                    if (isCircle && (i - circleR) * (i - circleR) + (j - circleR) * (j - circleR) > (circleR - r) * (circleR - r))
                        continue;

                    // 对边缘的处理
                    int u = 0, v = 0, tempLength0 = length, tempLength1 = length;
                    //if (i < r)
                    //{
                    //    u += r - i;
                    //}
                    //else if (i >= array.GetLength(0) - r)
                    //{
                    //    tempLength0 -= i + r + 1 - array.GetLength(0) + 1;
                    //}

                    //if (j < r)
                    //{
                    //    v += r - j;
                    //}
                    //else if (j >= array.GetLength(1) - r)
                    //{
                    //    tempLength1 -= j + r + 1 - array.GetLength(1);
                    //}

                    // 用高斯核处理一个值
                    float value = 0;
                    for (u = 0; u < tempLength0; u++)
                    {
                        for (v = 0; v < tempLength1; v++)
                        {
                            if ((i + u - r) >= array.GetLength(0) || (i + u - r) < 0 || (j + v - r) >= array.GetLength(1) || (j + v - r) < 0)
                                Debug.LogError("滴嘟滴嘟的报错" + i + " as" + j);
                            else
                                value += gaussianCore[u, v] * array[i + u - r, j + v - r];
                        }
                    }
                    array[i, j] = value;
                }
            }
        }

        #endregion

        #region 抛物线相关公式

        /// <summary>
        /// 求抛物线上某点的高度
        /// </summary>
        /// <param name="angle">角度</param>
        /// <param name="v">速度</param>
        /// <param name="x">x轴的量</param>
        /// <returns></returns>
        public static float ParabolaGetHeight(float angle, float v, float x)
        {
            float vy = v * Mathf.Sin(Mathf.Deg2Rad * angle);
            float vxz = v * Mathf.Cos(Mathf.Deg2Rad * angle);

            float y = -0.5f * 9.8f * (x * x / (vxz * vxz)) + vy * x / vxz;
            return y;
        }

        /// <summary>
        /// 根据俯仰角和速度求最远距离
        /// </summary>
        public static float ParabolaGetMaxDis(float v, float angle)
        {
            return 2 * v * v * Mathf.Sin(angle * Mathf.Deg2Rad) * Mathf.Cos(angle * Mathf.Deg2Rad) / (9.8f);
        }

        /// <summary>
        /// 通过起始点和速度反推俯仰角
        /// </summary>
        public static float ParabolaGetAngle(Vector3 start, Vector3 end, float v)
        {
            float tempRad = Mathf.Asin(9.8f * (start - end).magnitude / (v * v));
            return tempRad * Mathf.Rad2Deg / 2;
        }

        /// <summary>
        /// 求给定高度的距离的较大值
        /// </summary>
        /// <param name="v">速度</param>
        /// <param name="angle">角度</param>
        /// <param name="height">竖直方向上的高度</param>
        /// <returns></returns>
        public static float ParabolaGetDis(float v, float angle, float height)
        {
            float vy = v * Mathf.Sin(Mathf.Deg2Rad * angle);

            if (vy * vy + 2 * -9.8f * height < 0) return 0;
            float res1, res2;
            res1 = (-vy + Mathf.Sqrt(vy * vy + 2 * -9.8f * height)) / -9.8f;
            res2 = (-vy - Mathf.Sqrt(vy * vy + 2 * -9.8f * height)) / -9.8f;

            res1 = res1 > res2 ? res1 : res2;       // t 取较大值
            res1 = res1 * v * Mathf.Cos(Mathf.Deg2Rad * angle);     // x = vxz * t

            return res1;
        }

        #endregion

        #region 曲线相关

        /// <summary>
        /// y为0的伯努利方程
        /// </summary>
        /// <param name="a"></param>
        /// <param name="radian">弧度</param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static Vector2 GetBernoulli(int a, float radian)
        {
            float p = Mathf.Sqrt(a * a * Mathf.Cos(2 * radian));
            float x = p * Mathf.Cos(radian);
            float y = p * Mathf.Sin(radian);
            if ((0.75) * Mathf.PI < radian && radian < (1.25) * Mathf.PI)
            {
                return new Vector2(x, -y);
            }
            else
            {
                return new Vector2(x, y);
            }
        }

        #endregion

        #region 几何图形

        /// <summary>
        /// 获取椭圆上的点
        /// </summary>
        /// <param name="_r">短轴</param>
        /// <param name="_R">长轴</param>
        /// <param name="_origin">中点</param>
        /// <param name="seg">间隔</param>
        /// <returns></returns>
        public static Vector2[] GetEllipsePoints(float _r, float _R, Vector2 _origin, int seg)
        {
            float angle;
            Vector2[] points = new Vector2[seg];
            int j = 0;
            for (float i = 0; i < 360; j++, i += 360 / seg)
            {
                angle = (i / 180) * Mathf.PI;
                points[j] = _origin + new Vector2(_r * Mathf.Cos(angle), _R * Mathf.Sin(angle));
            }
            return points;
        }

        /// <summary>
        /// 获取半径为r的圆的点坐标集合
        /// </summary>
        /// <param name="origin"> 圆心 </param>
        /// <param name="radius"> 半径 </param>
        /// <returns></returns>
        public static Vector3[] GetCirclePoints(Vector3 origin, float radius, int seg = 120)
        {
            float angle;
            Vector3[] points = new Vector3[seg];
            for (int i = 0, j = 0; i < 360; j++, i += 360 / seg)
            {
                angle = Mathf.Deg2Rad * i;
                points[j] = origin + new Vector3(radius * Mathf.Cos(angle), 0, radius * Mathf.Sin(angle));
            }
            //points[360] = origin;     // 圆心点
            return points;
        }

        /// <summary>
        /// 获取扇形区域的点集合（包括圆心点）
        /// </summary>
        /// <param name="origin">圆心点</param>
        /// <param name="egdePoint">扇形弧边右边缘点</param>
        /// <param name="angleDiffer">x，z轴张角</param>
        /// <returns></returns>
        public static Vector3[] GetSectorPoints(Vector3 origin, Vector3 egdePoint, float angleDiffer)
        {
            float angle;
            Vector3 dir = egdePoint - origin;                               //取两点的向量
            float radius = dir.magnitude;                                   //获取扇形的半径

            //取数组长度 如60度的弧边取61个点 0~60 再加上一个圆心点
            Vector3[] points = new Vector3[(int)(angleDiffer / 3) + 2];
            points[0] = origin;                                             //取圆心点
            int startEuler = (int)Vector2.Angle(Vector2.right, new Vector2(dir.x, dir.z));
            for (int i = startEuler, j = 1; i <= angleDiffer + startEuler; j++, i += 3)
            {
                angle = Mathf.Deg2Rad * i;
                float differ = Mathf.Abs(Mathf.Cos(angle - (float)(0.5 * angleDiffer * Mathf.Deg2Rad)) * egdePoint.y - egdePoint.y);//高度差的绝对值
                points[j] = origin + new Vector3(radius * Mathf.Cos(angle), egdePoint.y + differ, radius * Mathf.Sin(angle));       //给底面点赋值
            }
            return points;
        }

        /// <summary>
        /// 获取两点组成的矩形边框
        /// </summary>
        /// <param name="start">起始点</param>
        /// <param name="end">终止点</param>
        /// <param name="width">宽度</param>
        /// <returns></returns>
        public static Vector2[] GetRect(Vector2 start, Vector2 end, float width)
        {
            Vector2[] rect = new Vector2[4];
            Vector2 dir = Math2d.GetHorizontalDir(end - start);
            rect[0] = start + dir * width;
            rect[1] = start - dir * width;
            rect[2] = end + dir * width;
            rect[3] = end - dir * width;

            return rect;
        }

        /// <summary>
        /// 获取Rect边框上中离pos最近的点
        /// </summary>
        public static Vector3 GetClosestBorderPoint(Vector3 pos, Rect rect)
        {
            float left = rect.x;
            float right = rect.x + rect.width;
            float bottom = rect.y;
            float top = rect.y + rect.height;

            // 初始化为矩形边界内的点
            float x = Mathf.Clamp(pos.x, left, right);
            float y = Mathf.Clamp(pos.y, bottom, top);

            // 如果点在矩形内部，找到最近的边
            if (x == pos.x && y == pos.y)
            {
                // 计算到各边的距离
                float distToLeft = pos.x - left;
                float distToRight = right - pos.x;
                float distToBottom = pos.y - bottom;
                float distToTop = top - pos.y;

                // 找到最近的边
                float minDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);

                if (minDist == distToLeft) x = left;
                else if (minDist == distToRight) x = right;
                else if (minDist == distToBottom) y = bottom;
                else y = top;
            }

            return new Vector3(x, y, 0);
        }

        #endregion

        #region 多边形相关

        /// <summary>
        /// 以index点和前后两个点构造一个三角形,判断点集内的其余点是否全部在这个三角形外部
        /// </summary>
        public static bool IsFragementIndex(List<Vector2> verts, int index, bool containEdge = true)
        {
            int len = verts.Count;
            List<Vector2> triangleVert = new List<Vector2>();
            int next = (index == len - 1) ? 0 : index + 1;
            int prev = (index == 0) ? len - 1 : index - 1;
            triangleVert.Add(verts[prev]);
            triangleVert.Add(verts[index]);
            triangleVert.Add(verts[next]);
            for (int i = 0; i < len; i++)
            {
                if (i != index && i != prev && i != next)
                {
                    if (IsPointInsidePolygon(verts[i], triangleVert.ToArray(), containEdge))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 以index点和前后两个点构造一个三角形,判断点集内的其余点是否全部在这个三角形外部(忽略y轴)
        /// </summary>
        public static bool IsFragementIndex(List<Vector3> verts, int index, bool containEdge = true)
        {
            int len = verts.Count;
            List<Vector3> triangleVert = new List<Vector3>();
            int next = (index == len - 1) ? 0 : index + 1;
            int prev = (index == 0) ? len - 1 : index - 1;
            triangleVert.Add(verts[prev]);
            triangleVert.Add(verts[index]);
            triangleVert.Add(verts[next]);
            for (int i = 0; i < len; i++)
            {
                if (i != index && i != prev && i != next)
                {
                    if (Math2d.IsPointInsidePolygon(verts[i], triangleVert.ToArray(), containEdge))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 将一个多边形转化为多个三角形
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static List<Vector2[]> PolygonToTriangles(List<Vector2> points)
        {
            if (points.Count < 3)
            {
                return null;
            }
            List<Vector2[]> triangles = new List<Vector2[]>();
            int index = points.Count - 1;
            int next;
            int prev;

            while (points.Count > 3)
            {
                List<Vector2> polygon = new List<Vector2>(points);
                polygon.RemoveAt(index);

                //是否是凹点
                if (!Math2d.IsPointInsidePolygon(points[index], polygon.ToArray(), false))
                {
                    // 是否是可划分顶点:新的多边形没有顶点在分割的三角形内
                    if (IsFragementIndex(points, index, false))
                    {
                        //可划分，剖分三角形
                        next = (index == points.Count - 1) ? 0 : index + 1;
                        prev = (index == 0) ? points.Count - 1 : index - 1;

                        triangles.Add(new Vector2[]
                        {
                        points[index],
                        points[prev],
                        points[next]
                        });

                        points.RemoveAt(index);

                        index = (index + points.Count - 1) % points.Count;       // 防止出现index超出值域
                        continue;
                    }
                }
                index = (index + 1) % points.Count;
            }
            triangles.Add(new Vector2[] { points[1], points[0], points[2] });

            return triangles;
        }

        /// <summary>
        /// 判断点是否在多边形区域内
        /// </summary>
        /// <param name="p">待判断的点，格式：{ x: X坐标, y: Y坐标 }</param>
        /// <param name="poly">多边形顶点，数组成员的格式同</param>
        /// <returns>true:在多边形内，凹点   false：在多边形外，凸点</returns>
        public static bool IsPointInsidePolygon(Vector2 p, Vector2[] poly, bool containEdge = true)
        {
            float px = p.x;
            float py = p.y;
            double sum = 0;

            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i, i++)
            {
                float sx = poly[i].x;
                float sy = poly[i].y;
                float tx = poly[j].x;
                float ty = poly[j].y;

                // 点与多边形顶点重合或在多边形的边上(这个判断有些情况不需要)
                if ((sx - px) * (px - tx) >= 0 && (sy - py) * (py - ty) >= 0 && (px - sx) * (ty - sy) == (py - sy) * (tx - sx))
                {
                    return containEdge;
                }

                // 点与相邻顶点连线的夹角
                var angle = Mathf.Atan2(sy - py, sx - px) - Math.Atan2(ty - py, tx - px);

                // 确保夹角不超出取值范围（-π 到 π）
                if (angle >= Mathf.PI)
                {
                    angle = angle - Mathf.PI * 2;
                }
                else if (angle <= -Mathf.PI)
                {
                    angle = angle + Mathf.PI * 2;
                }

                sum += angle;
            }

            // 计算回转数并判断点和多边形的几何关系
            return Mathf.RoundToInt((float)(sum / Math.PI)) != 0;
        }

        /// <summary>
        /// 判断点是否在多边形区域内(忽略y轴)
        /// </summary>
        /// <param name="p">待判断的点，格式：{ x: X坐标, y: Y坐标 }</param>
        /// <param name="poly">多边形顶点，数组成员的格式同</param>
        /// <returns>true:在多边形内，凹点   false：在多边形外，凸点</returns>
        public static bool IsPointInsidePolygon(Vector3 p, Vector3[] poly, bool containEdge = true)
        {
            float px = p.x;
            float py = p.z;
            double sum = 0;

            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i, i++)
            {
                float sx = poly[i].x;
                float sy = poly[i].z;
                float tx = poly[j].x;
                float ty = poly[j].z;

                // 点与多边形顶点重合或在多边形的边上(这个判断有些情况不需要)
                if ((sx - px) * (px - tx) >= 0 && (sy - py) * (py - ty) >= 0 && (px - sx) * (ty - sy) == (py - sy) * (tx - sx))
                {
                    return containEdge;
                }

                // 点与相邻顶点连线的夹角
                var angle = Mathf.Atan2(sy - py, sx - px) - Math.Atan2(ty - py, tx - px);

                // 确保夹角不超出取值范围（-π 到 π）
                if (angle >= Mathf.PI)
                {
                    angle -= Mathf.PI * 2;
                }
                else if (angle <= -Mathf.PI)
                {
                    angle += Mathf.PI * 2;
                }

                sum += angle;
            }

            // 计算回转数并判断点和多边形的几何关系
            return Mathf.RoundToInt((float)(sum / Math.PI)) == 0 ? false : true;
        }

        /// <summary>
        /// 判断点是否在多边形区域内
        /// </summary>
        /// <param name="point"> 待判断的点 </param>
        /// <param name="mPoints"> 多边形 </param>
        /// <returns></returns>
        public static bool IsPointInsidePolygon(Vector3 point, List<Vector3> mPoints)
        {
            int nCross = 0;

            for (int i = 0; i < mPoints.Count; i++)
            {
                Vector3 p1 = mPoints[i];
                Vector3 p2 = mPoints[(i + 1) % mPoints.Count];

                // 取多边形任意一个边,做点point的水平延长线,求解与当前边的交点个数
                // p1p2是水平线段,要么没有交点,要么有无限个交点, 计为无交点
                if (p1.z == p2.z)
                {
                    continue;
                }

                // point 在p1p2 底部 --> 无交点   (等于底部时, 算有交点)
                if (point.z < Mathf.Min(p1.z, p2.z))
                {
                    continue;
                }

                // point 在p1p2 顶部 或等于顶部 --> 无交点
                if (point.z >= Mathf.Max(p1.z, p2.z))
                {
                    continue;
                }

                // 求解 point点水平线与当前p1p2边的交点的 X 坐标
                // 直线两点式方程：(y-y2)/(y1-y2) = (x-x2)/(x1-x2)
                float x = (point.z - p1.z) * (p2.x - p1.x) / (p2.z - p1.z) + p1.x;

                if (x > point.x)        // 当 x = point.x 时,说明 point 在 p1p2 线段上, 不成立
                {
                    nCross++;           // 只统计单边交点
                }
            }

            // 单边交点为奇数，点在多边形之内
            return (nCross % 2 == 1);
        }

        /// <summary>
        /// 判断点是否在多边形区域内
        /// </summary>
        /// <param name="point"> 待判断的点 </param>
        /// <param name="mPoints"> 多边形 </param>
        /// <returns></returns>
        public static bool IsPointInsidePolygon(Vector3 point, Vector3[] mPoints, out List<Vector3> crossPoints)
        {
            int nCross = 0;
            crossPoints = new List<Vector3>();

            for (int i = 0; i < mPoints.Length; i++)
            {
                Vector3 p1 = mPoints[i];
                Vector3 p2 = mPoints[(i + 1) % mPoints.Length];

                // 取多边形任意一个边,做点point的水平延长线,求解与当前边的交点个数
                // p1p2是水平线段,要么没有交点,要么有无限个交点, 计为无交点
                if (p1.z == p2.z)
                {
                    // point点与p1p2在同一高度,加入p1点和p2点
                    if (p1.z == point.z)
                    {
                        crossPoints?.Add(p1);
                        crossPoints?.Add(p2);
                    }
                    continue;
                }
                // point 在p1p2 底部 --> 无交点   (等于底部时, 算有交点)
                if (point.z < Mathf.Min(p1.z, p2.z))
                {
                    continue;
                }

                // point 在p1p2 顶部 或等于顶部 --> 无交点
                if (point.z >= Mathf.Max(p1.z, p2.z))
                {
                    continue;
                }

                // 求解 point点水平线与当前p1p2边的交点的 X 坐标
                // 直线两点式方程：(y-y2)/(y1-y2) = (x-x2)/(x1-x2)
                float x = (point.z - p1.z) * (p2.x - p1.x) / (p2.z - p1.z) + p1.x;

                // 加上交点
                crossPoints?.Add(new Vector3(x, 0, point.z));

                if (x > point.x)        // 当 x = point.x 时,说明 point 在 p1p2 线段上, 不成立
                {
                    nCross++;           // 只统计单边交点
                }
            }

            // 单边交点为奇数，点在多边形之内
            return (nCross % 2 == 1);
        }

        public static bool IsPointInsidePolygon(Vector2 point, Vector2[] mPoints, out List<Vector2> crossPoints)
        {
            int nCross = 0;
            crossPoints = new List<Vector2>();

            for (int i = 0; i < 3; i++)
            {
                Vector2 p1 = mPoints[i];
                Vector2 p2 = mPoints[(i + 1) % 3];

                // 取多边形任意一个边,做点point的水平延长线,求解与当前边的交点个数
                // p1p2是水平线段,要么没有交点,要么有无限个交点, 计为无交点
                if (p1.y == p2.y)
                {
                    // point点与p1p2在同一高度,加入p1点和p2点
                    if (p1.y == point.y)
                    {
                        crossPoints?.Add(p1);
                        crossPoints?.Add(p2);
                    }
                    continue;
                }

                // 加上水平线与线段的端点交点
                if (point.y == p1.y)
                {
                    crossPoints?.Add(p1);
                }
                else if (point.y == p2.y)
                {
                    crossPoints?.Add(p2);
                }


                // point 在p1p2 底部 --> 无交点   (等于底部时, 算有交点)
                if (point.y < Mathf.Min(p1.y, p2.y))
                {
                    continue;
                }

                // point 在p1p2 顶部 或等于顶部 --> 无交点
                if (point.y >= Mathf.Max(p1.y, p2.y))
                {
                    continue;
                }

                // 求解 point点水平线与当前p1p2边的交点的 X 坐标
                // 直线两点式方程：(y-y2)/(y1-y2) = (x-x2)/(x1-x2)
                float x = (point.y - p1.y) * (p2.x - p1.x) / (p2.y - p1.y) + p1.x;

                // 加上交点
                crossPoints?.Add(new Vector2(x, point.y));

                if (x > point.x)        // 当 x = point.x 时,说明 point 在 p1p2 线段上, 不成立
                {
                    nCross++;           // 只统计单边交点
                }
            }

            // 单边交点为奇数，点在多边形之内
            return (nCross % 2 == 1);
        }

        /// <summary>
        /// 计算多边形面积(忽略y轴)
        /// </summary>
        /// <param name="points"></param>
        /// <returns>平方米</returns>
        public static float ComputePolygonArea(List<Vector3> points)
        {
            float iArea = 0;

            for (int iCycle = 0, iCount = points.Count; iCycle < iCount; iCycle++)
            {
                iArea += (points[iCycle].x * points[(iCycle + 1) % iCount].z - points[(iCycle + 1) % iCount].x * points[iCycle].z);
            }

            return (float)Math.Abs(0.5 * iArea);
        }

        #endregion
    }
}