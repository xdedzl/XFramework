using MySql.Data.MySqlClient;
using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MySql数据库操作类。
/// 多线程使用MySql数据库时请加锁。
/// 参考文献：http://www.runoob.com/MySql/MySql-tutorial.html 
/// </summary>
class MySqlOpr
{
    #region 库操作
    /// <summary>
    /// 获取MySql数据库连接对象。
    /// </summary>
    /// <param name="dbFile">数据库文件路径和文件名</param>
    /// <param name="openImmediately">是否立即调用Open方法打开数据库，默认不调用</param>
    /// <returns></returns>
    public static MySqlConnection GetMySqlConnection(string datasource, string port, string database, string user, string pwd, bool openImmediately = false)
    {
        string uri = string.Format("datasource={0};port={1};database={2};user={3};pwd={4};Allow User Variables = True;", datasource, port, database, user, pwd);
        MySqlConnection dbConn;
        try
        {
            dbConn = new MySqlConnection(uri);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e.Message);
            return null;
        }

        if (openImmediately)
        {
            try
            {
                dbConn.Open();
            }
            catch (System.Exception e)
            {
                LogError("数据库连接失败" + e.Message);
            }

        }

        return dbConn;
    }

    /// <summary>
    /// 执行没有查询结果的SQL语句。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="sql">SQL语句</param>
    /// <returns>sql命令所影响行数，若不影响则返回-1</returns>
    public int ExecuteNonQuery(MySqlConnection dbConn, string sql)
    {
        MySqlCommand cmd = new MySqlCommand(sql, dbConn);
        int ret = cmd.ExecuteNonQuery();

        return ret;
    }

    /// <summary>
    /// 执行有查询结果的SQL语句。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="sql">SQL语句</param>
    /// <returns></returns>
    public MySqlDataReader ExecuteReader(MySqlConnection dbConn, string sql)
    {
        MySqlDataReader reader = null;

        MySqlCommand cmd = new MySqlCommand(sql, dbConn);
        reader = cmd.ExecuteReader();

        return reader;
    }

    #endregion

    #region 表操作

    /// <summary>
    /// 获取数据库中所有数据表的表名。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <returns></returns>
    public static string[] GetAllTablesName(MySqlConnection dbConn)
    {
        string key = "table_name";
        string sql = "select * from information_schema.tables";

        MySqlCommand cmd = new MySqlCommand(sql, dbConn);
        MySqlDataReader reader = cmd.ExecuteReader();

        List<string> tableNameList = new List<string>();
        while (reader.Read())
        {
            tableNameList.Add(reader[key].ToString());
        }

        string[] tableNames = tableNameList.ToArray();

        return tableNames;
    }

    /// <summary>
    /// 检查数据表是否存在，如果存在返回true，否则返回false。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名称</param>
    /// <returns></returns>
    public static bool CheckTableExists(MySqlConnection dbConn, string tableName)
    {
        MySqlCommand cmd = dbConn.CreateCommand();
        cmd.CommandText = string.Format("select COUNT(*) from information_schema.tables WHERE table_name = '{0}';", tableName);
        int count = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.Dispose();
        if (count == 0)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    /// <summary>
    /// 创建数据表。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名称</param>
    /// <param name="columnDefs">数据表字段定义数组，每个字段作为一个独立的字符串</param>
    /// <returns></returns>
    public int CreateTable(MySqlConnection dbConn, string tableName, string[] columnDefs)
    {
        StringBuilder sb = new StringBuilder();

        char comma = ',';
        foreach (var def in columnDefs)
        {
            sb.Append(def).Append(comma);
        }
        sb.Remove(sb.Length - 1, 1);

        return CreateTable(dbConn, tableName, sb.ToString());
    }

    /// <summary>
    /// 创建数据表。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名称</param>
    /// <param name="columnDefs">数据表字段定义，每个定义之间使用半角逗号分隔</param>
    /// <returns></returns>
    public int CreateTable(MySqlConnection dbConn, string tableName, string columnDefs)
    {
        if (CheckTableExists(dbConn, tableName))
        {
            Debug.LogError(string.Format("数据表 {0} 已存在，不能重复创建。", tableName));
            return -2;
        }

        string sql = string.Format("CREATE TABLE {0} ({1});", tableName, columnDefs);

        MySqlCommand cmd = new MySqlCommand(sql, dbConn);
        int ret = cmd.ExecuteNonQuery();
        return ret;
    }

    /// <summary>
    /// 删除数据表。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名</param>
    /// <returns></returns>
    public int DeleteTable(MySqlConnection dbConn, string tableName)
    {
        if (!CheckTableExists(dbConn, tableName))
        {
            Debug.LogError(string.Format("数据表 {0} 不存在，无法删除。", tableName));
            return -2;
        }

        string sql = string.Format("DROP TABLE {0};", tableName);

        MySqlCommand cmd = new MySqlCommand(sql, dbConn);
        int ret = cmd.ExecuteNonQuery();

        return ret;
    }

    /// <summary>
    /// 重命名数据表。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="oldTableName">旧数据表名称</param>
    /// <param name="newTableName">新数据表名称</param>
    /// <returns></returns>
    public int RenameTable(MySqlConnection dbConn, string oldTableName, string newTableName)
    {
        if (!CheckTableExists(dbConn, oldTableName))
        {
            Debug.LogError(string.Format("数据表 {0} 不存在，无法重命名。", oldTableName));
            return -2;
        }

        if (CheckTableExists(dbConn, newTableName))
        {
            Debug.LogError(string.Format("数据表 {0} 已存在，无法重命名。", newTableName));
            return -3;
        }

        string sql = string.Format("ALTER TABLE {0} RENAME TO '{1}';", oldTableName, newTableName);

        MySqlCommand cmd = new MySqlCommand(sql, dbConn);
        int ret = cmd.ExecuteNonQuery();

        return ret;
    }

    #endregion

    #region 字段操作

    /// <summary>
    /// 获取数据表中所有字段的名称。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名</param>
    /// <returns></returns>
    public string[] GetAllColumnsName(MySqlConnection dbConn, string tableName)
    {
        string sql = string.Format("PRAGMA table_info({0});", tableName);

        MySqlCommand cmd = new MySqlCommand(sql, dbConn);
        MySqlDataReader reader = cmd.ExecuteReader();

        List<string> columns = new List<string>();
        while (reader.Read())
        {
            columns.Add(reader["name"].ToString());
        }

        return columns.ToArray();
    }

    /// <summary>
    /// 检查字段是否存在，如果存在返回true，否则返回false。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名名</param>
    /// <param name="columnName">字段名</param>
    /// <returns></returns>
    public bool CheckColumnExists(MySqlConnection dbConn, string tableName, string columnName)
    {
        /**
         * 也可以通过查询的方式检测字段是否存在：
         * SELECT COUNT(*) FROM MySql_master WHERE name='test_table' AND sql LIKE '% column_name %';
         * 缺点：
         * 刚方法是查询数据表创建语句中是否含有要查找的那个字段的名称，使用了通配符，不能精确查找，可能出现误判。
         */

        string[] columns = GetAllColumnsName(dbConn, tableName);
        foreach (var column in columns)
        {
            if (column == columnName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 向数据表添加新列。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名</param>
    /// <param name="columnDef">数据表字段定义</param>
    /// <returns></returns>
    public int AddColumnToTable(MySqlConnection dbConn, string tableName, string columnDef)
    {
        if (!CheckTableExists(dbConn, tableName))
        {
            Debug.LogError(string.Format("数据表 {0} 不存在，无法重命名。", tableName));
            return -2;
        }

        string sql = string.Format("ALTER TABLE {0} ADD COLUMN {1};", tableName, columnDef);

        MySqlCommand cmd = new MySqlCommand(sql, dbConn);
        int ret = cmd.ExecuteNonQuery();

        return ret;
    }

    /// <summary>
    /// 从数据表删除列。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名</param>
    /// <param name="columnName">要删除的列名</param>
    /// <param name="dontBackup">不保留原表备份</param>
    /// <returns></returns>
    public int DeleteColumnFromTable(MySqlConnection dbConn, string tableName, string columnName, bool dontBackup = false)
    {
        if (!CheckTableExists(dbConn, tableName))
        {
            Debug.LogError(string.Format("数据表 {0} 不存在，无法删除。", tableName));
            return -2;
        }

        if (!CheckColumnExists(dbConn, tableName, columnName))
        {
            Debug.LogError(string.Format("字段 {0} 不存在，无法删除。", columnName));
            return -3;
        }

        // MySql目前不支持直接删除列！！！

        /**
         * 实现思路：
         * 1. 获取该数据表中的所有字段名称存到列表中；
         * 2. 将要删除的那个字段的名称从列表中移除；
         * 3. 将数据表重命名为不与其他表名冲突的临时名称（避免在他人使用该数据表时改名）；
         * 4. 将列表中剩余的字段的数据全部查出并存储到和原来的数据表同名的新数据表中。
         */

        char comma = ',';
        StringBuilder sb = new StringBuilder();
        string[] columns = GetAllColumnsName(dbConn, tableName);

        // 排除要删除的列
        foreach (var column in columns)
        {
            if (column == columnName)
            {
                continue;
            }

            sb.Append(column).Append(comma);
        }
        sb.Remove(sb.Length - 1, 1);

        // 寻找一个可作为备份表名的字符串
        string backupTableName;
        do
        {
            backupTableName = tableName + "_backup_" + DateTime.Now.ToString("yyyyMMddHHmmss");
        } while (CheckTableExists(dbConn, backupTableName));

        // 将要删除列的表名改为备份表名
        RenameTable(dbConn, tableName, backupTableName);

        // 将临时表中的数据复制到与原表同名的新表中
        // 补充：WHERE 1=0 —— 只复制表结构，不复制表数据
        string sql = string.Format("CREATE TABLE {0} AS SELECT {1} FROM {2};", tableName, sb.ToString(), backupTableName);

        MySqlCommand cmd = new MySqlCommand(sql, dbConn);
        int ret = cmd.ExecuteNonQuery();

        // 如果不保留备份，则删除备份表
        if (dontBackup)
        {
            DeleteTable(dbConn, backupTableName);
        }

        return -ret;
    }
    #endregion

    #region 数据操作

    ///// <summary>
    ///// 从数据表中查找数据。
    ///// </summary>
    ///// <param name="dbConn">MySql数据库连接对象</param>
    ///// <param name="tableName">数据表名</param>
    ///// <param name="columnNames">字段名数组，每个字段名作为一个单独的字符串</param>
    ///// <param name="wheres">查找条件</param>
    ///// <param name="limit">查找数量</param>
    ///// <param name="orderBy">排序规则</param>
    ///// <returns></returns>
    //public static MySqlDataReader Select(MySqlConnection dbConn, string tableName, string[] columnNames, string[] wheres, string orderBy, int limit)
    //{
    //    char comma = ',';
    //    string and = " AND ";
    //    StringBuilder sb1 = new StringBuilder();
    //    StringBuilder sb2 = new StringBuilder();

    //    foreach (var column in columnNames)
    //    {
    //        sb1.Append(column).Append(comma);
    //    }
    //    sb1.Remove(sb1.Length - 1, 1);

    //    foreach (var where in wheres)
    //    {
    //        sb2.Append(where).Append(and);
    //    }
    //    sb2.Remove(sb2.Length - and.Length, and.Length);
    //    //sb2.Replace(and, "", sb2.Length - and.Length, and.Length);

    //    return Select(dbConn, tableName, sb1.ToString(), sb2.ToString(), orderBy, limit);
    //}

    /// <summary>
    /// 从数据表中查找数据。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名</param>
    /// <param name="columnNames">字段名，多个字段名之间使用半角逗号分隔</param>
    /// <param name="wheres">查找条件，多个条件之间使用 AND 分隔</param>
    /// <param name="limit">查找数量</param>
    /// <param name="orderBy">排序规则</param>
    /// <returns></returns>
    public static MySqlDataReader Select(MySqlConnection dbConn, string tableName, string columnNames, string where = null, string orderBy = null, int limit = -1)
    {
        //if (!CheckTableExists(dbConn, tableName))
        //{
        //    Debug.LogError(string.Format("数据表 {0} 不存在，无法查找数据。", tableName));
        //    return null;
        //}
        StringBuilder sql = new StringBuilder();
        sql.Append(string.Format("SELECT {0} FROM {1}", columnNames, tableName));
        if (!string.IsNullOrEmpty(where)) sql.Append(" WHERE " + where);
        if (!string.IsNullOrEmpty(orderBy)) sql.Append(" ORDER BY " + orderBy);
        if (limit != -1) sql.Append(" LIMIT " + limit);
        sql.Append(';');

        MySqlDataReader reader;

        MySqlCommand cmd = dbConn.CreateCommand();
        cmd.CommandText = sql.ToString();

        try
        {
            reader = cmd.ExecuteReader();
            cmd.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError("读取数据库报错");
            Debug.LogError(e.Message);
            return null;
        }
        return reader;
    }

    /// <summary>
    /// 向数据表中插入数据。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名</param>
    /// <param name="columnNames">字段名数组，每个字段名作为一个单独的字符串</param>
    /// <param name="columnValues">字段值数组，每个值作为一个单独的字符串</param>
    /// <returns></returns>
    public int Insert(MySqlConnection dbConn, string tableName, string[] columnNames, string[] columnValues)
    {
        if (columnNames.Length != columnValues.Length)
        {
            Debug.LogError("列名数目和值数目不匹配，无法插入数据。");
            return -3;
        }

        char comma = ',';
        StringBuilder sb1 = new StringBuilder();
        StringBuilder sb2 = new StringBuilder();

        foreach (var field in columnNames)
        {
            sb1.Append(field).Append(comma);
        }
        sb1.Remove(sb1.Length - 1, 1);

        foreach (var value in columnValues)
        {
            sb2.Append(value).Append(comma);
        }
        sb2.Remove(sb2.Length - 1, 1);

        return Insert(dbConn, tableName, sb1.ToString(), sb2.ToString());
    }

    /// <summary>
    /// 向数据表中插入数据。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名</param>
    /// <param name="columnNames">字段名，可以是多个字段，多个字段名之间使用半角逗号分隔</param>
    /// <param name="columnValues">字段值，数量要和字段名对应，多个值之间使用半角逗号分隔</param>
    /// <returns></returns>
    public int Insert(MySqlConnection dbConn, string tableName, string columnNames, string columnValues)
    {
        if (!CheckTableExists(dbConn, tableName))
        {
            Debug.LogError(string.Format("数据表 {0} 不存在，无法插入数据。", tableName));
            return -2;
        }

        string sql = string.Format("INSERT INTO {0} ({1}) VALUES ({2});", tableName, columnNames, columnValues);

        MySqlCommand cmd = new MySqlCommand(sql, dbConn);
        int ret = cmd.ExecuteNonQuery();

        return ret;
    }

    /// <summary>
    /// 更新数据表中的数据。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名</param>
    /// <param name="columnNames">字段名数组，每个字段名作为一个单独的字符串</param>
    /// <param name="columnValues">字段值数组，每个字段值作为一个单独的字符串</param>
    /// <param name="where">更新条件</param>
    /// <returns></returns>
    public static int Update(MySqlConnection dbConn, string tableName, string[] columnNames, string[] columnValues, string[] wheres)
    {
        if (columnNames.Length != columnValues.Length)
        {
            Debug.LogError("所选字段数目与提供的值数目不匹配，无法更新数据。");
            return -3;
        }

        char comma = ',';
        char equal = '=';
        string and = " AND ";
        StringBuilder sb1 = new StringBuilder();
        StringBuilder sb2 = new StringBuilder();

        for (int i = 0; i < columnNames.Length; i++)
        {
            sb1.Append(columnNames[i]).Append(equal).Append(columnValues[i]).Append(comma);
        }
        sb1.Remove(sb1.Length - 1, 1);

        foreach (var where in wheres)
        {
            sb2.Append(where).Append(and);
        }
        sb2.Remove(sb2.Length - and.Length, and.Length);

        return Update(dbConn, tableName, sb1.ToString(), sb2.ToString());
    }

    /// <summary>
    /// 更新数据表中的数据。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名</param>
    /// <param name="set">新数据</param>
    /// <param name="where">更新条件</param>
    /// <returns></returns>
    public static int Update(MySqlConnection dbConn, string tableName, string set, string where)
    {
        if (!CheckTableExists(dbConn, tableName))
        {
            Debug.LogError(string.Format("数据表 {0} 不存在，无法更新数据。", tableName));
            return -2;
        }

        string sql = string.Format("UPDATE {0} SET {1} WHERE {2};", tableName, set, where);
        Debug.Log(sql);
        MySqlCommand cmd = new MySqlCommand(sql, dbConn);
        int ret = cmd.ExecuteNonQuery();

        return ret;
    }

    /// <summary>
    /// 从数据表删除数据。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名</param>
    /// <param name="wheres">删除条件</param>
    /// <returns></returns>
    public int Delete(MySqlConnection dbConn, string tableName, string[] wheres)
    {
        string and = " AND ";
        StringBuilder sb = new StringBuilder();

        foreach (var where in wheres)
        {
            sb.Append(where).Append(and);
        }
        sb.Remove(sb.Length - and.Length, and.Length);

        return Delete(dbConn, tableName, sb.ToString());
    }

    /// <summary>
    /// 从数据表中删除数据。
    /// </summary>
    /// <param name="dbConn">MySql数据库连接对象</param>
    /// <param name="tableName">数据表名</param>
    /// <param name="where">删除条件</param>
    /// <returns></returns>
    public int Delete(MySqlConnection dbConn, string tableName, string where)
    {
        if (!CheckTableExists(dbConn, tableName))
        {
            Debug.LogError(string.Format("数据表 {0} 不存在，无法删除数据。", tableName));
            return -2;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("DELETE FROM ").Append(tableName);
        if (!string.IsNullOrEmpty(where)) sb.Append(" WHERE ").Append(where);
        sb.Append(';');

        MySqlCommand cmd = new MySqlCommand(sb.ToString(), dbConn);
        int ret = cmd.ExecuteNonQuery();

        return ret;
    }
    #endregion

    #region 查找操作

    /// <summary>
    /// 从一个表中查找数据
    /// </summary>
    /// <param name="conn">数据库连接</param>
    /// <param name="tableName">表名</param>
    /// <param name="columnName">查找条件对应的条款名</param>
    /// <param name="orderBy">查找条件</param>
    /// <returns></returns>
    public static MySqlDataReader SelectRegion(MySqlConnection conn, string tableName, string columnName, string where)
    {
        string cmdStr = string.Format("select * from {0} where {1} = '{2}';", tableName, columnName, where);
        MySqlCommand cmd = new MySqlCommand(cmdStr, conn);
        try
        {
            MySqlDataReader dataReader = cmd.ExecuteReader();
            //dataReader.Read();
            //if (dataReader.HasRows)
            //{
            //    returnStr = dataReader.GetString("REGION_NAME");
            //}
            //dataReader.Close();
            return dataReader;
        }
        catch (System.Exception e)
        {
            Debug.Log("[DataMgr]SelectRegion fail   " + e.Message);
            return null;
        }
    }

    #endregion

    public static void LogError(string errorType = " ", string errorContent = " ")
    {
        Debug.LogError(string.Format("[MySql] {0} : {1}", errorType, errorContent));
    }
}
