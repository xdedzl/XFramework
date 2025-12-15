using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.Resource
{
    public class DependenciesData
    {
        private readonly string[] m_Empty;

        [SerializeField]
        private SingleDependenciesData[] AllDependenceData;

        public DependenciesData()
        {
            m_Empty = Array.Empty<string>();
        }

        public DependenciesData(SingleDependenciesData[] allDependenceData)
        {
            AllDependenceData = allDependenceData;

            m_Empty = Array.Empty<string>();
        }

        public string[] GetDirectDependencies(string assetBundleName)
        {
            foreach (var item in AllDependenceData)
            {
                if (assetBundleName == item.Name)
                {
                    return item.Dependencies;
                }
            }
            return m_Empty;
        }

        public string[] GetAllDependencies(string assetBundleName)
        {
            var result = new List<string>();

            string[] dps = GetDirectDependencies(assetBundleName);

            var tempList = new List<string>();
            while (dps.Length != 0)
            {
                foreach (var item in dps)
                {
                    if (!result.Contains(item))
                    {
                        result.Add(item);
                    }
                }

                foreach (var item in dps)
                {
                    tempList.AddRange(GetDirectDependencies(item));
                }
                dps = tempList.ToArray();
                tempList.Clear();
            }

            return result.ToArray();
        }

        public string[] GetAllAssetBundles()
        {
            var paths = new List<string>();

            foreach (var item in AllDependenceData)
            {
                paths.Add(item.Name);
            }

            return paths.ToArray();
        }
    }

    [Serializable]
    public class SingleDependenciesData
    {
        /// <summary>
        /// AB包名
        /// </summary>
        public string Name;
        /// <summary>
        /// 直接依赖包
        /// </summary>
        public string[] Dependencies;

        public SingleDependenciesData(string name, string[] dependencies)
        {
            Name = name;
            Dependencies = dependencies;
        }
    }

    public static class DependencyUtility
    {
        /// <summary>
        /// 融合依赖关系
        /// 在数组中越靠后优先级越高
        /// </summary>
        /// <param name="dates"></param>
        public static DependenciesData CombineDependence(DependenciesData[] dates)
        {
            var singleDates = new List<SingleDependenciesData>();

            foreach (var data in dates.Reverse())
            {
                string[] assetBundles = data.GetAllAssetBundles();

                foreach (var abName in assetBundles)
                {
                    if (Contains(abName))
                        continue;
                    singleDates.Add(new SingleDependenciesData(abName, data.GetDirectDependencies(abName)));
                }
            }

            return new DependenciesData(singleDates.ToArray());

            bool Contains(string abName)
            {
                foreach (var item in singleDates)
                {
                    if (abName == item.Name)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 将unity依赖转为自己的
        /// </summary>
        /// <param name="manifest"></param>
        /// <returns></returns>
        public static DependenciesData Manifest2Dependence(AssetBundleManifest manifest)
        {
            string[] abNames = manifest.GetAllAssetBundles();

            var singleDates = new List<SingleDependenciesData>();

            for (int j = 0; j < abNames.Length; j++)
            {
                var dpNames = manifest.GetDirectDependencies(abNames[j]);
                if (dpNames.Length <= 0)
                {
                    continue;
                }
                singleDates.Add(new SingleDependenciesData(abNames[j], dpNames));
            }
            var data = new DependenciesData(singleDates.ToArray());
            return data;
        }
    }
}