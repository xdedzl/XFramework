using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Tool
{
    public class CSVReader
    {
        private int index;
        private string[] keys;
        private string[] lines;
        private Dictionary<string, string> readerDic;

        public CSVReader(string text)
        {
            readerDic = new Dictionary<string, string>();
            lines = text.Split('\n');
            string[] keys = lines[0].Split(',');
            for (int i = 0; i < keys.Length; i++)
            {
                readerDic[keys[i]] = "";
            }
            index = 1;
            Debug.Log("A");
        }

        public bool ReadLine()
        {
            if (index >= lines.Length)
                return false;
            string[] line = lines[index].Split(',');
            int num = 0;
            foreach (var key in readerDic.Keys)
            {
                readerDic[key] = line[num++];
            }

            return true;
        }

        public int GetInt32(string name)
        {
            return int.Parse(readerDic[name]);
        }

        public float GetFloat(string name)
        {
            return float.Parse(readerDic[name]);
        }

        public double GetDouble(string name)
        {
            return double.Parse(readerDic[name]);
        }

        public string GetString(string name)
        {
            return readerDic[name];
        }
    }
}