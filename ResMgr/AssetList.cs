using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// 生成的资源索引表
/// </summary>
public class AssetList
{
    public static string kAssetListFileName = "asset_list.json";
    public string platform = "";
    // 从资源路径获取包名
    public Dictionary<string, string> files = new Dictionary<string, string>();
}

/// <summary>
/// 生成的Lua脚本代码列表
/// </summary>
public class LuaList
{
    public static string kLuaListFileName = "lua_list.json";
    public Dictionary<string, string> files = new Dictionary<string, string>();
}

