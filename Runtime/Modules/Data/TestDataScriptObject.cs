using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Data 
{
    [Serializable]
    public struct TestData : IDataHasKey<int>
    {
        public int id;
        public string content;

        public override string ToString()
        {
            return $"TestData: id={id}, content={content}";
        }

        public int PrimaryKey => id;
    }

    [DataResourcePath("Assets/ABRes/Data/TestDatas.asset")]
    [TargetDataType(typeof(TestData))]
    [CreateAssetMenu(fileName = "TestDatas", menuName = "DataScriptableObject/DataScriptableObject")]
    public class TestDataScriptableObject:DataScriptableObject<TestData>
    {
    }
}