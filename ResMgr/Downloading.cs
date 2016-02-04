/**
 * Copyright (c) 2016,广州擎天柱网络科技有限公司
 * All rights reserved.
 *
 * 文件名称：Downloading.cs
 * 简    述：更新所需资源到持久化目录Application.persistentDataPath,必须配合
 * BundleBuilder打包好的美术UI资源、Package中生成导出的lualist等才能正常正常运行
 * 实现了更新资源的进度条显示，依赖于指定的Loading.prefab
 * 创建标识：
 * 修改标识：Lorry 2016/2/2 整理代码模块，除去对于Util的依赖，让ResourceManager依赖自己
 */
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 用于处理资源自动更新，同时处理lua脚本更新，依赖于Assets/Prefabs/ui/Loading.prefab
/// </summary>
public class Downloading : MonoBehaviour {
    /// <summary>
    ///  用于显示加载界面的资源,这个资源名不能随便修改，一旦修改，需要清空持久文件夹
    ///  Application.persistentDataPath才能正常更新，因为。
    /// </summary>
    public static string LOAD_UI = "Assets/Prefabs/ui/CommonLoading.prefab".ToLower();
    private static string kManifestFileName = "asset_bundle";
    private static string kAssetPath = kManifestFileName + "/"; //生成的资源目录和Manifest文件必定同名
    private static string kLuaPath = "lua/";
    private static string kVersionFileName = "version";
    private static string kDebugFileName = "debug";

    private string _version = "v0.0.0.0";
    private GameObject _load = null;
    private Text _loadingTips = null;
    private Image _process = null;
    private bool _finish = false;
    /// <summary>
    /// 判断更新流程是否完成
    /// </summary>
    public bool IsFinish() {
        return _finish;
    }
    /// <summary>
    /// 通过Coroutine开始整个界面的加载
    /// </summary>
    public IEnumerator StartDownload () {
        yield return StartCoroutine(LoadUi());
        Assert.IsNotNull<GameObject>(_load, "loading界面加载出错");
        _loadingTips = _load.transform.Find("Text").gameObject.GetComponent<Text>();
        _process = _load.transform.Find("ProgC_Loading").gameObject.GetComponent<Image>();
        Assert.IsNotNull<Text>(_loadingTips, "loading界面数据出错");
        yield return StartCoroutine(UpdateResources());
    }

    private IEnumerator LoadUi() {
        //这里面如果有一步失败就不用玩了，不用判断，直接加Assert了
        //本地有下载界面就用本地的
        Debug.LogWarning("LoadUi:" + UtilGetAssetPath(kManifestFileName));
        if (System.IO.File.Exists(UtilGetAssetPath(kManifestFileName))) {
            LoadUiFromLocalAssets();
        } else {
            yield return StartCoroutine(LoadUiFormPackage());
        }
        Resources.UnloadUnusedAssets();
    }

    private void LoadUiFromLocalAssets() {
        string manifestPath = UtilGetAssetPath(kManifestFileName);
        Debug.Log("LoadUiFromLocalAssets:" + manifestPath);
        AssetBundle bundle = UtilCreateBundleFromFile(manifestPath);//AssetBundle.CreateFromFile(manifestPath);
        Assert.IsNotNull<AssetBundle>(bundle, "加载本地manifest失败！");
        AssetBundleManifest manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
        Assert.IsNotNull<AssetBundleManifest>(manifest, "从bundle加载manifest失败");
        bundle.Unload(false);

        string assetListPath = UtilGetAssetPath(AssetList.kAssetListFileName);
        string content = System.IO.File.ReadAllText(assetListPath);
        AssetList assetlist = UtilJson2Object<AssetList>(content);
        if(assetlist.files.ContainsKey(LOAD_UI) == false)
        {
            _load = Resources.Load("CommonLoading") as GameObject;
            return;
        }
        string bundleName = assetlist.files[LOAD_UI];

        string[] depends = manifest.GetAllDependencies(bundleName);
        List<AssetBundle> dependBundles = new List<AssetBundle>();
        foreach (string abName in depends) {
            string path = UtilGetAssetPath(abName);
            bundle = UtilCreateBundleFromFile(path);
            dependBundles.Add(bundle);
        }
        string bundlePath = UtilGetAssetPath(bundleName);
        bundle = UtilCreateBundleFromFile(bundlePath);
        GameObject asset = bundle.LoadAsset<GameObject>(LOAD_UI);
        _load = GameObject.Instantiate(asset) as GameObject;
        bundle.Unload(false);
        foreach (AssetBundle ab in dependBundles) {
            ab.Unload(false);
        }
    }

    private IEnumerator LoadUiFormPackage() {
        string manifestUrl = GetPackageAssetUrl(kManifestFileName);
        Debug.LogWarning("LoadUiFormPackage:" + manifestUrl);

        WWW downloader = new WWW(manifestUrl);
        yield return downloader;
        Assert.IsTrue(string.IsNullOrEmpty(downloader.error), downloader.error);
        AssetBundleManifest manifest = downloader.assetBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
        Assert.IsNotNull<AssetBundleManifest>(manifest);
        downloader.assetBundle.Unload(false);
        downloader.Dispose();

        string assetListUrl = GetPackageAssetUrl(AssetList.kAssetListFileName);
        downloader = new WWW(assetListUrl);
        yield return downloader;
        Assert.IsTrue(string.IsNullOrEmpty(downloader.error), downloader.error);
        AssetList assetlist = UtilJson2Object<AssetList>(downloader.text);
        downloader.Dispose();
        string bundleName = assetlist.files[LOAD_UI];


        string[] depends = manifest.GetAllDependencies(bundleName);
        List<AssetBundle> dependBundles = new List<AssetBundle>();
        foreach (string abName in depends) {
            string url = GetPackageAssetUrl(abName);
            downloader = new WWW(url);
            yield return downloader;
            Assert.IsTrue(string.IsNullOrEmpty(downloader.error), downloader.error);
            dependBundles.Add(downloader.assetBundle);
        }
        string uiAssetUrl = GetPackageAssetUrl(bundleName);
        downloader = new WWW(uiAssetUrl);
        yield return downloader;
        Assert.IsTrue(string.IsNullOrEmpty(downloader.error), downloader.error);
        GameObject asset = downloader.assetBundle.LoadAsset<GameObject>(LOAD_UI);
        _load = GameObject.Instantiate(asset) as GameObject;
        downloader.assetBundle.Unload(false);
        downloader.Dispose();
        foreach (AssetBundle ab in dependBundles) {
            ab.Unload(false);
        }
    }

    //editor模式下，认为package assets = remote assets
    private string GetPackageAssetUrl(string relaPath) {
        return combinePath(GetPackageRootUrl(), kAssetPath + relaPath);
    }

    private string GetPakcageLuaUrl(string relaPath) {
        return combinePath(GetPackageRootUrl(), kLuaPath + relaPath);
    }

    private string GetPackageRootUrl() {
        switch (Application.platform) {
            case RuntimePlatform.Android:
                return string.Format("{0}/", Application.streamingAssetsPath);
            case RuntimePlatform.IPhonePlayer:
                return string.Format("file://{0}/", Application.streamingAssetsPath);
            case RuntimePlatform.WindowsPlayer:
                return string.Format("file:///{0}/", Application.streamingAssetsPath);
            case RuntimePlatform.WindowsEditor:
                return GetRemoteRootUrl();
            default:
                string msg = "不支持的平台:" + Application.platform.ToString();
                SetErrorTipsMsg(msg);
                throw new System.Exception(msg);
        }
    }

    ///////////////////////////////////////////////////初始化资源目录
    private string m_webUrl = "http://192.168.16.96/q6/";
    /// <summary>
    /// 此变量指定了在编辑器状态下，更新资源工程所在的位置，打包成为独立版本时用的是m_webUrl
    /// </summary>
    private string m_editorResPath = "file:///C:/q6/trunk/debug/win64";

    /// <summary>
    /// 初始化正规版本的下载路径，如："http://192.168.16.96/q6/"
    /// 路径
    /// </summary>
    public void InitWebUrl(string webUrl)
    {
        m_webUrl = webUrl;
    }
    /// <summary>
    /// 初始化编辑器版本指定的路径，这里是指定的全路径，路径下有asset_bundle目录
    /// assetbundle目录包括assets目录，asset_bundle.manifest文件,asset_list.json
    /// </summary>
    /// <param name="path"></param>
    public void InitEditorResPath( string path)
    {
        m_editorResPath = path;
    }
    ///////////////////////////////////////////////////初始化

    private string GetRemoteRootUrl() {
#if RELEASE
        string releaesTag = "release";
#else
        string releaesTag = "debug";
#endif
        switch (Application.platform) {
            case RuntimePlatform.Android:
                return combinePath(m_webUrl, releaesTag + "/android");
            case RuntimePlatform.IPhonePlayer:
                return combinePath(m_webUrl, releaesTag + "/ios");
            case RuntimePlatform.WindowsPlayer:
                return combinePath(m_webUrl, releaesTag + "/win64");
            case RuntimePlatform.WindowsEditor:
                return m_editorResPath;
                //return "file:///" + System.IO.Path.Combine(m_editorResPath, releaesTag + "/win64");
            default:
                string msg = "不支持的平台:" + Application.platform.ToString();
                SetErrorTipsMsg(msg);
                throw new System.Exception(msg);
        }
    }

    private string GetRemoteAssetUrl(string relaPath) {
        return combinePath(GetRemoteRootUrl(), kAssetPath + relaPath);
    }

    private string GetRemoteLuaPath(string relaPath) {
        return combinePath(GetRemoteRootUrl(), kLuaPath + relaPath);
    }

    /// <summary>
    /// 向指定路径写入文件，主要用于下载服务器资源和lua逻辑代码
    /// </summary>
    private void WriteAllByte(string path, byte[] data) {
        string directory = System.IO.Path.GetDirectoryName(path);
        if (System.IO.Directory.Exists(directory) == false)
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        System.IO.File.WriteAllBytes(path, data);
    }

    private enum VersionState
    {
        kNormal,
        kAppOutOffTime,
        kResourcesOutOfTime,
    };

    private VersionState CompareVersion(string current, string last) {
        string[] currentArray = current.Substring(1).Split('.');
        string[] lastArray = last.Substring(1).Split('.');
        for (int i = 0; i < 3; i++) {
            if (currentArray[i] != lastArray[i]) {
                return VersionState.kAppOutOffTime;
            }
        }
        if (currentArray[3] != lastArray[3])
            return VersionState.kResourcesOutOfTime;
        return VersionState.kNormal;
    }

    private void SetVersion(string v) {
        _version = v;
    }

    private void SetTipsMsg(string msg) {
        Debug.Log(msg);
        _loadingTips.text = msg;
    }

    private void SetProcess(float percent) {
        _process.fillAmount = percent;
    }

    private void SetErrorTipsMsg(string msg) {
        Debug.LogError(msg);
        _loadingTips.text = string.Format("<color=red>{0}</color>", msg);
    }

    private IEnumerator UpdateResources() {
        //无论怎么样，本地无资源的话先解出包里的资源
        string localAssetInfoFilePath = UtilGetAssetPath(AssetList.kAssetListFileName);
        if (!System.IO.File.Exists(localAssetInfoFilePath) && !Application.isEditor) {
            Debug.Log(localAssetInfoFilePath + " 不存在，从包里解压资源");
            yield return StartCoroutine(ExtraPackageResources());
        }

#if RELEASE
#else
        string debugFilePath = combinePath(Application.persistentDataPath, kDebugFileName);
        if (System.IO.File.Exists(debugFilePath) && !Application.isEditor) {
            SetTipsMsg("调试版，不更新资源");
            yield return new WaitForSeconds(1);
            _finish = true;
            yield break;
        }
#endif
        //==本地版本号
		string localVersion = _version;//Application.version;
        string localVersionFile = UtilGetAssetPath(kVersionFileName);
        if (System.IO.File.Exists(localVersionFile) && !Application.isEditor) {
            string tmpVersion = System.IO.File.ReadAllText(localVersionFile);
            VersionState state = CompareVersion(localVersion, tmpVersion);
            if (state != VersionState.kAppOutOffTime) {
                localVersion = tmpVersion;
            }
        }
        //Util.SetVersion(localVersion);
        string remoteVersion = localVersion;
        string remoteVersionFileUrl = combinePath(GetRemoteRootUrl(), kVersionFileName);
        if (Application.isEditor == false) 
        {//不在编辑器中运行，才开始下载服务器版本文件
            SetTipsMsg(string.Format("下载版本文件{0}", remoteVersionFileUrl));
            WWW downloader = new WWW(remoteVersionFileUrl);
            yield return downloader;
            if (!string.IsNullOrEmpty(downloader.error)) {
                SetErrorTipsMsg(string.Format("下载版本文件{0}失败{1}", remoteVersionFileUrl, downloader.error));
                yield break;
            }
            remoteVersion = downloader.text;
            downloader.Dispose();
			if (remoteVersion == localVersion) {
				_finish = true;
				yield break;
			}
            VersionState state = CompareVersion(localVersion, remoteVersion);
            if (state == VersionState.kAppOutOffTime) {
#if RELEASE
                SetErrorTipsMsg(string.Format("版本({0})过旧,请更新App", localVersion));
#else
                SetErrorTipsMsg(string.Format("版本({0})过旧,开发版本，将不更新资源进入游戏", localVersion));
                yield return new WaitForSeconds(2);
                _finish = true;
#endif
                yield break;
            }
        }

        SetTipsMsg("开始更新远程资源");
        yield return StartCoroutine(UpdateRemoteSources());
        _finish = true;
        //Util.SetVersion(remoteVersion);
        System.IO.File.WriteAllText(localVersionFile, remoteVersion);
    }

    private IEnumerator UpdateRemoteSources() {
        string localManifestPath = UtilGetAssetPath(kManifestFileName);
        AssetBundleManifest localManifest = null;
        if (System.IO.File.Exists(localManifestPath)) {
            AssetBundle manifestBundle = UtilCreateBundleFromFile(localManifestPath);
            localManifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            manifestBundle.Unload(false);
        }
        string remoteManifestUrl = GetRemoteAssetUrl(kManifestFileName);
        WWW manifestDownloader = new WWW(remoteManifestUrl);
        SetTipsMsg(string.Format("下载{0}", remoteManifestUrl));
        yield return manifestDownloader;
        if (!string.IsNullOrEmpty(manifestDownloader.error)) {
            SetErrorTipsMsg(string.Format("下载{0}失败{1}", remoteManifestUrl, manifestDownloader.error));
        }
        AssetBundleManifest remoteManifest = manifestDownloader.assetBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
        string[] assetbundles = remoteManifest.GetAllAssetBundles();
        for (int i = 0; i < assetbundles.Length; ++i) {
            string abName = assetbundles[i];
            Hash128 remoteHash = remoteManifest.GetAssetBundleHash(abName);
            if (localManifest != null) {
                Hash128 localHash = localManifest.GetAssetBundleHash(abName);
                if (localHash.Equals(remoteHash)) {
                    continue;
                }
            }
            string url = GetRemoteAssetUrl(abName);
            SetTipsMsg(string.Format("下载{0}", abName));
            SetProcess(i * 1.0f / assetbundles.Length);
            WWW downloader = new WWW(url);
            yield return downloader;
            if (!string.IsNullOrEmpty(downloader.error)) {
                SetErrorTipsMsg(string.Format("下载{0}失败{1}", url, downloader.error));
            }
            string localPath = UtilGetAssetPath(abName);
            WriteAllByte(localPath, downloader.bytes);
            downloader.Dispose();
        }
        WriteAllByte(localManifestPath, manifestDownloader.bytes);
        manifestDownloader.assetBundle.Unload(true);
        manifestDownloader.Dispose();

        string remoteAssetListFilePath = GetRemoteAssetUrl(AssetList.kAssetListFileName);
        WWW assetListDownloader = new WWW(remoteAssetListFilePath);
        SetTipsMsg(string.Format("下载{0}", remoteAssetListFilePath));
        yield return assetListDownloader;
        if (!string.IsNullOrEmpty(assetListDownloader.error)) {
            SetErrorTipsMsg(string.Format("下载{0}失败 {1}", remoteAssetListFilePath, assetListDownloader.error));
            yield break;
        }
        string localAssetListPath = UtilGetAssetPath(AssetList.kAssetListFileName);
        WriteAllByte(localAssetListPath, assetListDownloader.bytes);
        assetListDownloader.Dispose();

        if (Application.isEditor)
            yield break;

        string remoteLuaListUrl = GetRemoteLuaPath(LuaList.kLuaListFileName);
        WWW luaListDownloader = new WWW(remoteLuaListUrl);
        yield return luaListDownloader;
        if (!string.IsNullOrEmpty(luaListDownloader.error)) {
            SetErrorTipsMsg(string.Format("下载{0}失败{1}", remoteLuaListUrl, luaListDownloader.error));
        }
        LuaList remoteLuaList = UtilJson2Object<LuaList>(luaListDownloader.text);

        string localLuaList = combinePath(luaPath, LuaList.kLuaListFileName);
        LuaList luaList = null;
        if (System.IO.File.Exists(localLuaList)) {
            string content = System.IO.File.ReadAllText(localLuaList);
            luaList = UtilJson2Object<LuaList>(content);
        }
        int cnt = 0;
        foreach (KeyValuePair<string, string> keyValue in remoteLuaList.files) {
            cnt += 1;
            SetProcess(cnt * 1.0f / remoteLuaList.files.Count);
            string file = keyValue.Key;
            if (luaList != null && luaList.files.ContainsKey(file) && luaList.files[file] == keyValue.Value) {
                continue;
            }
            string url = GetRemoteLuaPath(file);
            SetTipsMsg(string.Format("下载{0}", file));
            WWW downloader = new WWW(url);
            yield return downloader;
            if (!string.IsNullOrEmpty(downloader.error)) {
                SetErrorTipsMsg(string.Format("下载{0}失败{1}", url, downloader.error));
            }

            string localPath = combinePath(luaPath, file);
            WriteAllByte(localPath, downloader.bytes);
            downloader.Dispose();
        }
        WriteAllByte(localLuaList, luaListDownloader.bytes);
    }

    private IEnumerator ExtraPackageResources() {
        //资源
        string manifestPath = GetPackageAssetUrl(kManifestFileName);
        WWW downlader = new WWW(manifestPath);
        yield return downlader;
        if (!string.IsNullOrEmpty(downlader.error)) {
            string msg = string.Format("解压Manifest文件失败({0}):{1}", downlader.error, manifestPath);
            SetTipsMsg(msg);
            yield break;
        }
        AssetBundleManifest menifest = downlader.assetBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
        string[] assetbundles = menifest.GetAllAssetBundles();
        for (int i = 0; i < assetbundles.Length;++i) {
            string abName = assetbundles[i];
            string abPath = GetPackageAssetUrl(abName);
            string localPath = UtilGetAssetPath(abName);
            SetTipsMsg(string.Format("解压:{0}", abName));
            SetProcess(i * 1.0f / assetbundles.Length);
            WWW abDownloader = new WWW(abPath);
            yield return abDownloader;
            if (!string.IsNullOrEmpty(abDownloader.error)) {
                SetErrorTipsMsg(string.Format("解压 {0} 失败 : {1}", abPath, abDownloader.error));
            }
            WriteAllByte(localPath, abDownloader.bytes);
            abDownloader.Dispose();
        }
        string manifestLocalPath = UtilGetAssetPath(kManifestFileName);
        WriteAllByte(manifestLocalPath, downlader.bytes);
        downlader.assetBundle.Unload(true);
        downlader.Dispose();
        if (Application.isEditor)
            yield break;
        //脚本
        string luaListPath = GetPakcageLuaUrl(LuaList.kLuaListFileName);
        downlader = new WWW(luaListPath);
        yield return downlader;
        if (!string.IsNullOrEmpty(downlader.error)) {
            Debug.LogError(string.Format("解压LuaFlist文件失败({0}):{1}", downlader.error, luaListPath));
            yield break;
        }
        LuaList luaList = UtilJson2Object<LuaList>(downlader.text);
        foreach (KeyValuePair<string, string> keyValue in luaList.files) {
            string url = GetPakcageLuaUrl(keyValue.Key);
            WWW luaDownloader = new WWW(url);
            yield return luaDownloader;
            string localPath = combinePath(luaPath, keyValue.Key);
            WriteAllByte(localPath, luaDownloader.bytes);
            luaDownloader.Dispose();
        }
        WriteAllByte(combinePath(luaPath, LuaList.kLuaListFileName), downlader.bytes);
        downlader.Dispose();

        //因为是根据assetfile来做判断，这个必须放到最后，错了还能重来
        string assetInfoFilePath = GetPackageAssetUrl(AssetList.kAssetListFileName);
        WWW wwwAssetInfoFile = new WWW(assetInfoFilePath);
        yield return wwwAssetInfoFile;
        if (!string.IsNullOrEmpty(wwwAssetInfoFile.error)) {
            SetErrorTipsMsg(string.Format("解压 {0} 失败 : {1}", assetInfoFilePath, wwwAssetInfoFile.error));
        }
        string localAssetInfoFilePath = UtilGetAssetPath(AssetList.kAssetListFileName);
        WriteAllByte(localAssetInfoFilePath, wwwAssetInfoFile.bytes);
        wwwAssetInfoFile.Dispose();
    }

    ///////////////////////////////////////////////////以下为获得对应资源的方便函数
    /// <summary>
    /// 获得对应文件在持久化目录的真正完整路径
    /// </summary>
    public static string UtilGetAssetPath(string relaPath)
    {
        return System.IO.Path.Combine(Application.persistentDataPath + "/resources/", relaPath).Replace("\\", "/");
    }

    /// <summary>
    /// 因为AssetBundle从文件创建AssetBundle接口还有问题，所以这里先提供方便函数
    /// </summary>
    public static AssetBundle UtilCreateBundleFromFile(string path)
    {
        byte[] data = System.IO.File.ReadAllBytes(path);
        return AssetBundle.CreateFromMemoryImmediate(data);
    }

    /// <summary>
    /// 存放运行lua路径的
    /// </summary>
    public static string luaPath
    {
        get{
            if (Application.isEditor)
                return Application.dataPath + "/Lua/";
            else
                return Application.persistentDataPath + "/lua/"; 
        }
    }
    /// <summary>
    /// 将json格式的文件，转换为可使用的类型对象
    /// </summary>
    public static T UtilJson2Object<T>(string content)
    {
        return LitJson.JsonMapper.ToObject<T>(content);
    }

    private string combinePath(string p1, string p2)
    {
        return System.IO.Path.Combine(p1, p2).Replace("\\", "/");
    }



    // Update is called once per frame
    void Update() {

    }
}
