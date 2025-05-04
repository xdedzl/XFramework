using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.Resource
{
    public class DependenciesData
    {
        private readonly string[] m_Empty;

        [SerializeField]
        private SingleDepenciesData[] AllDependenceData;

        public DependenciesData()
        {
            m_Empty = new string[0];
        }

        public DependenciesData(SingleDepenciesData[] allDependenceData)
        {
            AllDependenceData = allDependenceData;

            m_Empty = new string[0];
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
            List<string> result = new List<string>();

            string[] dps = GetDirectDependencies(assetBundleName);

            List<string> tempList = new List<string>();
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
            List<string> strs = new List<string>();

            foreach (var item in AllDependenceData)
            {
                strs.Add(item.Name);
            }

            return strs.ToArray();
        }
    }

    [System.Serializable]
    public class SingleDepenciesData
    {
        /// <summary>
        /// AB包名
        /// </summary>
        public string Name;
        /// <summary>
        /// 直接依赖包
        /// </summary>
        public string[] Dependencies;

        public SingleDepenciesData(string name, string[] dependencies)
        {
            Name = name;
            Dependencies = dependencies;
        }
    }

    public class DependenceUtility
    {
        /// <summary>
        /// 融合依赖关系
        /// 在数组中越靠后优先级越高
        /// </summary>
        /// <param name="datas"></param>
        public static DependenciesData ConbineDependence(DependenciesData[] datas)
        {
            List<SingleDepenciesData> singleDatas = new List<SingleDepenciesData>();

            foreach (var data in datas.Reverse())
            {
                string[] assetBundles = data.GetAllAssetBundles();

                foreach (var abName in assetBundles)
                {
                    if (Contains(abName))
                        continue;
                    singleDatas.Add(new SingleDepenciesData(abName, data.GetDirectDependencies(abName)));
                }
            }

            return new DependenciesData(singleDatas.ToArray());

            bool Contains(string abName)
            {
                foreach (var item in singleDatas)
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
        /// <param name="mainfest"></param>
        /// <returns></returns>
        public static DependenciesData Manifest2Dependence(AssetBundleManifest mainfest)
        {
            string[] abNames = mainfest.GetAllAssetBundles();

            List<SingleDepenciesData> singleDatas = new List<SingleDepenciesData>();

            for (int j = 0; j < abNames.Length; j++)
            {
                var dpNames = mainfest.GetDirectDependencies(abNames[j]);
                if (dpNames.Length <= 0)
                {
                    continue;
                }
                singleDatas.Add(new SingleDepenciesData(abNames[j], dpNames));
            }
            var data = new DependenciesData(singleDatas.ToArray());
            return data;
        }
    }
}