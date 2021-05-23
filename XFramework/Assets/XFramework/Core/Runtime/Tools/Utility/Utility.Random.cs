using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URandom = UnityEngine.Random;

namespace XFramework
{
    public static partial class Utility
    {

        public static class Random
        {
            public static Vector3 RandomVector3(float x, float y, float z)
            {
                return new Vector3(URandom.Range(-x, x), URandom.Range(-y, y), URandom.Range(-z, z));
            }

            public static Quaternion RandomQuaternion()
            {
                return Quaternion.Euler(URandom.Range(-90, 90), URandom.Range(-90, 90), URandom.Range(-90, 90));
            }

            public static Color RandomColor()
            {
                return new Color(URandom.Range(0, 1), URandom.Range(0, 1), URandom.Range(0, 1), 1);
            }
        }
    }
}