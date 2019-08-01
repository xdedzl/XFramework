using System.Collections;
using System.Collections.Generic;
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
                if(assetBundleName == item.Name)
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
            while(dps.Length != 0)
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
}