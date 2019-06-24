using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MySql.Data.MySqlClient;
using System.Reflection;
using System;

/// <summary>
/// 数据库管理类
/// 在MySqlOpr类对数据库操作的基础上在上层管理数据库，把读取的数据转化成字典管理
/// </summary>
public class MySqlMgr : Singleton<MySqlMgr>
{
    //public GetJsonData getJsonData = new GetJsonData();
    /// <summary>
    /// 数据库地址
    /// </summary>
    private string m_Datasource;
    /// <summary>
    /// 数据库端口
    /// </summary>
    private string m_Port;
    /// <summary>
    /// 数据库名
    /// </summary>
    private string m_Database;
    /// <summary>
    /// 用户名
    /// </summary>
    private string m_User;
    /// <summary>
    /// 密码
    /// </summary>
    private string m_Pwd;

    /// <summary>
    /// 当前数据库链接
    /// </summary>
    private MySqlConnection m_Conn;

    /// <summary>
    /// 带参构造函数
    /// </summary>
    /// <param name="_datasource">数据库地址</param>
    /// <param name="_port">数据库端口</param>
    /// <param name="_database">数据库名</param>
    /// <param name="_user">用户名</param>
    /// <param name="_pwd">密码</param>
    public MySqlMgr()
    {
        //getJsonData.inputDate.data[0]
        //this.datasource = getJsonData.inputDate.data[0].datasource;
        //this.port = getJsonData.inputDate.data[0].port;
        //this.database = getJsonData.inputDate.data[0].database;
        //this.user = getJsonData.inputDate.data[0].user;
        //this.pwd = getJsonData.inputDate.data[0].pwd;

        m_Datasource = "";
        m_Port = "";
        m_Database = "";
        m_User = "";
        m_Pwd = "";
    }

    public void Update<T, P>(string Keyword, T value, string column, P data, string tableName)
    {
        MySqlConnection conn = MySqlOpr.GetMySqlConnection(m_Datasource, m_Port, m_Database, m_User, m_Pwd, true);  // 连接数据库

        if (conn == null)
        {
            Debug.LogError("据库空链接");
        }

        string where = string.Format("{0} = '{1}'", Keyword, value);
        string set = string.Format("{0} = '{1}'", column, data);

        MySqlOpr.Update(conn, tableName, set, where);
    }

    /// <summary>
    /// 将一个数据表转化成对应的类并存储在字典里
    /// </summary>
    /// <typeparam name="K">字典Key的类型</typeparam>
    /// <typeparam name="T">数据表对应的类</typeparam>
    /// <param name="tableName">数据表名</param>
    /// <param name="dicId">要定义为字典K的字段名</param>
    /// <returns></returns>
    public Dictionary<K, T> TableToEntity<K, T>(string tableName, string dicId, string columnNames, string condition = null) where T : class, new()
    {
        Dictionary<K, T> returnDatas = new Dictionary<K, T>();
        MySqlConnection conn = MySqlOpr.GetMySqlConnection(m_Datasource, m_Port, m_Database, m_User, m_Pwd, true);  // 连接数据库

        if (conn == null)
        {
            Debug.LogError("据库空链接");
            return returnDatas;
        }

        MySqlDataReader reader = MySqlOpr.Select(conn, tableName, columnNames, condition);                                   // 查找表

        if (reader == null)
        {
            Debug.LogError("数据库读取失败");
            return returnDatas;
        }

        Type type = typeof(T);                // 获取T的类型信息
        K dicKey = default(K);                // 字典存储Id
        int id = 0;
        string numName = string.Empty;
        try
        {
            while (reader.Read())
            {
                id = 0;
                FieldInfo[] pArray = type.GetFields();// 得到T的成员变量信息
                T entity = new T();
                foreach (FieldInfo p in pArray)
                {
                    id++;
                    string fieldInfo = p.FieldType.ToString();
                    numName = p.Name;
                    // 通过成员变量的信息选择读写的类型
                    switch (fieldInfo)
                    {
                        case "System.Int32":
                            p.SetValue(entity, reader.GetInt32(p.Name));
                            break;
                        case "System.Single":
                            p.SetValue(entity, reader.GetFloat(p.Name));
                            break;
                        case "System.Double":
                            p.SetValue(entity, reader.GetDouble(p.Name));
                            break;
                        case "System.Boolean":
                            p.SetValue(entity, reader.GetBoolean(p.Name));
                            break;
                        case "System.String":
                            p.SetValue(entity, reader.GetString(p.Name));
                            break;
                        case "UnityEngine.Vector2[]":
                            string typeName = reader.GetString(p.Name);
                            if (!string.IsNullOrEmpty(typeName))// 安全校验
                            {
                                p.SetValue(entity, GetVector2s(typeName));
                            }
                            break;
                        case "UnityEngine.Vector3[]":
                            break;
                        case "UnityEngine.Color":
                            string colorName = reader.GetString(p.Name);
                            if (!string.IsNullOrEmpty(colorName))
                            {
                                p.SetValue(entity, GetColor(colorName));
                            }
                            break;
                        default:
                            break;
                    }
                    if (p.Name == dicId)
                    {
                        dicKey = (K)p.GetValue(entity);
                    }
                }
                returnDatas.Add(dicKey, entity);// 加入字典
            }
        }
        catch (System.Exception e)
        {
            Debug.Log("[表转类读写出错] " + e.Message);
            Debug.Log("第" + id + "个数据有错");
            Debug.Log(numName);
        }
        reader.Close();
        conn.Close();// 关闭数据库连接
        conn.Dispose();

        return returnDatas;
    }

    /// <summary>
    /// 通过分号和逗号将字符串转化成二维向量数组
    /// </summary>
    /// <param name="BOUNDARY_POINT"></param>
    /// <returns></returns>
    public Vector2[] GetVector2s(string BOUNDARY_POINT)
    {
        BOUNDARY_POINT = BOUNDARY_POINT.Replace("\n", string.Empty).Replace("\r", string.Empty).Replace(" ", string.Empty);
        string[] points = BOUNDARY_POINT.Split(';');
        List<Vector2> returnList = new List<Vector2>();
        for (int i = 0; i < points.Length; i++)
        {
            if (!String.IsNullOrEmpty(points[i]))//安全校验
            {
                string[] point = points[i].Split(',');
                returnList.Add(new Vector2(float.Parse(point[0]), float.Parse(point[1])));
            }
        }
        return returnList.ToArray();
    }

    /// <summary>
    /// 将字符信息转为颜色类
    /// </summary>
    /// <param name="ColorInfo"></param>
    /// <returns></returns>
    public Color GetColor(string ColorInfo)
    {
        ColorInfo = ColorInfo.Replace("\n", string.Empty).Replace("\r", string.Empty).Replace(" ", string.Empty);
        string[] paras = ColorInfo.Split(',');
        return new Color32(Byte.Parse(paras[0]), Byte.Parse(paras[1]), Byte.Parse(paras[2]), 120);
    }
}
