using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// 生成的资源索引表
/// </summary>
public class AssetList
{
    /// <summary>
    /// 资源索引文件名
    /// </summary>
    public static string kAssetListFileName = "asset_list.json";
    /// <summary>
    /// 平台名称，资源打包目标平台
    /// </summary>
    public string platform = "";
    /// <summary>
    ///  从资源路径获取包名
    /// </summary>
    public Dictionary<string, string> files = new Dictionary<string, string>();
}

/// <summary>
/// 生成的Lua脚本代码列表
/// </summary>
public class LuaList
{
    /// <summary>
    /// lua脚本文件名列表
    /// </summary>
    public static string kLuaListFileName = "lua_list.json";
    /// <summary>
    /// 
    /// </summary>
    public Dictionary<string, string> files = new Dictionary<string, string>();
}

