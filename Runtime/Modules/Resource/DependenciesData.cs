using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.Resource
{
    [Serializable]
    public class DependenciesData
    {
        private readonly Dictionary<string, string[]> m_dependenceMap = new();
        
        public DependenciesData(SingleDependenciesData[] allDependenceData)
        {
            foreach (var data in allDependenceData)
            {
                if (!m_dependenceMap.TryAdd(data.name, data.dependencies))
                {
                    Debug.LogError($"DependenciesData has repeated assetBundleName {data.name}");
                }
            }
        }
        
        public bool IsAbExist(string assetBundleName)
        {
            return m_dependenceMap.ContainsKey(assetBundleName);
        }

        public string[] GetDirectDependencies(string assetBundleName)
        {
            if (m_dependenceMap.TryGetValue(assetBundleName, out var dps))
            {
                return dps;
            }
            else
            {
                Debug.LogError($"DependenciesData has no assetBundleName {assetBundleName}");
                return Array.Empty<string>();
            }
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
            return m_dependenceMap.Keys.ToArray();
        }
    }

    [Serializable]
    public class SingleDependenciesData
    {
        /// <summary>
        /// AB包名
        /// </summary>
        public string name;
        /// <summary>
        /// 直接依赖包
        /// </summary>
        public string[] dependencies;

        public SingleDependenciesData(string name, string[] dependencies)
        {
            this.name = name;
            this.dependencies = dependencies;
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
                    if (abName == item.name)
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
        public static SingleDependenciesData[] Manifest2Dependence(AssetBundleManifest manifest)
        {
            string[] abNames = manifest.GetAllAssetBundles();

            var singleDates = new List<SingleDependenciesData>();

            for (int j = 0; j < abNames.Length; j++)
            {
                var dpNames = manifest.GetDirectDependencies(abNames[j]);
                singleDates.Add(new SingleDependenciesData(abNames[j], dpNames));
            }
            return singleDates.ToArray();
        }
    }
}