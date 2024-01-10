using MySqlConnector;
using System.Text.RegularExpressions;

public static partial class DBManager
{
    public readonly static MySqlConnection mysql = new();


    /// <summary>
    /// 连接数据库
    /// </summary>
    public static bool Connect(string database, string server, int port, string user, string password)
    {
        mysql.ConnectionString = $"database={database};server={server};port={port};user={user};password={password};";
        try
        {
            mysql.Open();
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }
    
    /// <summary>
    /// 检查传入的字符串是否安全（防止SQL注入）
    /// </summary>
    public static bool IsSafeString(string str)
    {
        if (string.IsNullOrEmpty(str)) return false;
        return !SafeRegex().IsMatch(str);
    }

    /// <summary>
    /// 检查账号是否存在
    /// </summary>
    public static bool IsAccountExist(string name)
    {
        if (!IsSafeString(name)) return false;
        try
        {
            MySqlCommand cmd = new($"SELECT * FROM account WHERE name='{name}'", mysql);
            MySqlDataReader rdr = cmd.ExecuteReader();
            bool exist = rdr.HasRows;
            rdr.Close();
            return exist;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    /// <summary>
    /// 注册账号的方法
    /// </summary>
    /// <returns>是否注册成功</returns>
    public static bool Register(string name, string password)
    {
        if (!IsSafeString(name) || !IsSafeString(password)) return false;
        try
        {
            if (IsAccountExist(name))
            {
                Console.WriteLine("已有相同的账号名");
                return false;
            }
            MySqlCommand cmd = new($"INSERT INTO account (name, password) VALUES ('{name}', '{password}')", mysql);
            MySqlDataReader rdr = cmd.ExecuteReader();
            Console.WriteLine("注册成功");
            rdr.Close();
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    /// <summary>
    /// 登录账号的方法
    /// </summary>
    /// <returns>是否登录成功</returns>
    public static bool Login(string name, string password)
    {
        if (!IsSafeString(name) || !IsSafeString(password)) return false;
        try
        {
            MySqlCommand cmd = new($"SELECT * FROM account WHERE name='{name}' AND password='{password}'", mysql);
            MySqlDataReader rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                Console.WriteLine("登录成功");
                rdr.Close();
                return true;
            }
            else
            {
                Console.WriteLine("登录失败");
                rdr.Close();
                return false;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    /// <summary>
    /// 生成的正则表达式
    /// </summary>
    [GeneratedRegex(@"(.*\=.*\-\-.*)|(.*(\+|\-).*)|(.*\w+(%|\$|#|&)\w+.*)|(.*\|\|.*)|(.*\s+(and|or)\s+.*)|(.*\b(select|update|union|and|or|delete|insert|trancate|char|into|substr|ascii|declare|exec|count|master|into|drop|execute)\b.*)
")]
    private static partial Regex SafeRegex();
}