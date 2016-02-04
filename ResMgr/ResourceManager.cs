using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ResourceMisc;
using UnityEngine.Assertions;
using System.Text.RegularExpressions;

/*
 * Copyright (c) 2015,广州擎天柱网络科技有限公司
 * All rights reserved.
    文件名称：ResourceManager.cs
    简    述：资源加载，原来被各种逻辑代码直接使用，为了提高App运行效率，在使用需要多次加载的资源时，
    请优先使用SimplePool.Instance.Spawn来进行GameObject的创建。TODO:资源加载和内存进行优化
 *  另外在OnLevelWasLoaded中调用了LoadSceneMgr的函数，来完成逻辑，其实ResourceManager不用继承
 *  于MonoBehaviour，倒是LoadSceneMgr应该这么做.
    创建标识：Lorry 2015/10/8
*********************************************************************/
public class ResourceManager : MonoBehaviour
{
    static ResourceManager m_instance;

    public static ResourceManager Instance
    {
        get
        {
            if (m_instance == null)
            {
                GameObject mgr = GameObject.FindWithTag("GameManager");
                m_instance = mgr.AddComponent<ResourceManager>();
            }
            return m_instance;
        }
    }

    public const string kManifestFileName = "asset_bundle";
    #region resource container

    // bundle是否场景包
    List<string> _sceneBundles = new List<string>();

    // 资源路径对包名倒查表;
    Dictionary<string, string> _asset2Bundle = new Dictionary<string, string>();

    // bundle缓存;
    Dictionary<string, BundleWrapper> _bundleCache = new Dictionary<string, BundleWrapper>();

    // asset缓存;
    Dictionary<string, AssetWrapper> _assetWrapperCache = new Dictionary<string, AssetWrapper>();

    // bundle依赖文件;
    AssetBundleManifest _manifest = null;

    #endregion//res container

    private void LoadAssetList() {
        string assetListPath = Downloading.UtilGetAssetPath(AssetList.kAssetListFileName);
        Assert.IsTrue(System.IO.File.Exists(assetListPath), "加载资源列表文件失败！");
        string content = System.IO.File.ReadAllText(assetListPath);
        AssetList assetList = Downloading.UtilJson2Object<AssetList>(content);
        Assert.IsNotNull<AssetList>(assetList, "资源列表格式有误");
        _asset2Bundle = assetList.files;
     }

    /// <summary>
    /// 本函数只能调用一次,在资源更新完成之后调用
    /// </summary>
    public void Initialize() {
        Assert.IsNull<AssetBundleManifest>(_manifest, "manifest文件已经加载过");

        LoadAssetList();
        //读取出整个目录的依赖关系,这个文件名称和目录名称一样;
        string mUrl = Downloading.UtilGetAssetPath(kManifestFileName);
        AssetBundle bundle = Downloading.UtilCreateBundleFromFile(mUrl);
        _manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
        bundle.Unload(false);
    }

    // 判断是否场景包
    private bool IsSceneBundle(string bundleName) {
        return _sceneBundles.Contains(bundleName);
    }

    public AssetWrapper LoadAsset(string assetPath, System.Type assetType) {
        return _LoadAsset(assetPath.ToLower(), assetType, false);
    }

    public AssetWrapper LoadScene(string assetpath) {
        return _LoadAsset(assetpath.ToLower(), null, true);
    }

    // 同步加载
    private AssetWrapper _LoadAsset(string assetPath, System.Type assetType, bool isScene) {

        //去assetbundle中进行加载,首先找到资源对应的ab; Lorry
        if (_asset2Bundle.ContainsKey(assetPath) == false) {
            Debug.LogError(string.Format("找不到资源{0}", assetPath));
            return null;
        }

        //首先看资源AssetWrapper的缓存字典;
        if (_assetWrapperCache.ContainsKey(assetPath)) {
            _assetWrapperCache[assetPath].AddRef();
            return _assetWrapperCache[assetPath];
        }

        string abName = _asset2Bundle[assetPath];
        //需要首先判断加载 Lorry
        if (_bundleCache.ContainsKey(abName)) {
            List<string> refBundles = new List<string> { abName };
            refBundles.AddRange(_manifest.GetAllDependencies(abName));
            for (int i = 0; i < refBundles.Count; ++i) {
                string refName = refBundles[i];
                _bundleCache[refName].AddRefAsset(assetPath);
            }
            return LoadAssetFromBundleWrap(_bundleCache[abName], assetPath, assetType, refBundles.ToArray(), isScene);
        }
        return LoadRes(assetPath, assetType, isScene);
    }

    private AssetWrapper LoadAssetFromBundleWrap(BundleWrapper bundle, string assetName, System.Type assetType, string[] refBundles, bool isScene) {
        Object obj = null;
        if (!isScene) {
            obj = bundle.GetBundle().LoadAsset(assetName, assetType);
            Assert.IsNotNull<Object>(obj, string.Format("从bundle {0} 中加载 {1} 出错!", bundle.GetPath(), assetName));
        }
        AssetWrapper ret = new AssetWrapper(obj, assetName, assetType, bundle.GetPath(), refBundles);
        _assetWrapperCache.Add(assetName, ret);

        return ret;
    }

    //这里面已经是不会有错了，如果有错，说明打包资源已经出问题了，直接Assert
    private AssetWrapper LoadRes(string assetPath, System.Type assetType, bool isScene) {
        //开始加载对应的资源包(.assetbundle)
        string abName = _asset2Bundle[assetPath];
        List<string> allRefBundles = new List<string>() { abName };
        allRefBundles.AddRange(_manifest.GetAllDependencies(abName));
        for (int i = 0; i < allRefBundles.Count; ++i) {
            string refName = allRefBundles[i];
            BundleWrapper bundleWrap = null;
            if (_bundleCache.ContainsKey(refName)) {
                bundleWrap = _bundleCache[refName];
            } else {
                bundleWrap = LoadBundle(refName);
            }
            bundleWrap.AddRefAsset(assetPath);
        }

        return LoadAssetFromBundleWrap(_bundleCache[abName], assetPath, assetType, allRefBundles.ToArray(), isScene);
    }

    /// <summary>
    /// 释放指定资源的引用数
    /// </summary>
    /// <param name="asset"></param>
    public void ReleaseAsset(AssetWrapper asset) {
        asset.Release();
        if (asset.GetRefCount() < 1) {
            DestroyAsset(asset);
        }
    }

    /// <summary>
    /// 在过场景的时候清理所有载入的assert和assertbundle
    /// </summary>
    public void ClearAllAsset() {
        Debug.Log("ResourceManager ClearAllAsset");
        _assetWrapperCache.Clear();
        //自动销毁全部的资源
        foreach (KeyValuePair<string, BundleWrapper> kv in _bundleCache) {
            kv.Value.GetBundle().Unload(true);
        }
        
        _bundleCache.Clear();
        Resources.UnloadUnusedAssets();
    }

    /// <summary>
    /// 内部加载assetbundle的
    /// </summary>
    private BundleWrapper LoadBundle(string abName) {
        string abPath = Downloading.UtilGetAssetPath(abName);
        AssetBundle target_bundle = Downloading.UtilCreateBundleFromFile(abPath);
        Assert.IsNotNull<AssetBundle>(target_bundle, "assetbundle资源不存在:" + abName);
        BundleWrapper bundleWrap = new BundleWrapper(abName, target_bundle);
        _bundleCache.Add(abName, bundleWrap);
        return bundleWrap;
    }

    /// <summary>
    /// 销毁指定的资源
    /// </summary>
    /// <param name="asset"></param>
    private void DestroyAsset(AssetWrapper asset) {
        string assetPath = asset.GetAssetPath();
        string[] refBundles = asset.GetRefBundle();
        string mainBundle = asset.GetBundleName();
        for (int i = 0; i < refBundles.Length; ++i) {
            string abName = refBundles[i];
            Assert.IsTrue(_bundleCache.ContainsKey(abName), string.Format("Bundle:{0}不在缓存中", abName));
            BundleWrapper wrap = _bundleCache[abName];
            wrap.DecRefAsset(assetPath);

            //考虑有什么策略不要太频繁释放
            if (wrap.GetRefAssetCount() == 0) {
                wrap.GetBundle().Unload(false);
                _bundleCache.Remove(abName);
            }
        }
        Resources.UnloadUnusedAssets();
        _assetWrapperCache.Remove(asset.GetAssetPath());
    }

    void Awake() {

    }

    void OnDestroy() {
        _manifest = null;
        ClearAllAsset();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
#endif
        Debug.Log("~ResourceManager was destroy!");
    }
    /// <summary>
    /// 主要是因为ResourceManager这个组件不会被销毁，所以在这里调用LoadSceneMgr的相关函数。
    /// 原来是在
    /// </summary>
    /// <param name="level"></param>
    void OnLevelWasLoaded(int level)
    {
        if (Application.loadedLevelName == "empty")
        {
            LoadSceneMgr.Instance.OnLoadEmptylevel();
        }
        else
        {
            LoadSceneMgr.Instance.OnLoadScnene(level);
        }
    }

    /// <summary>
    /// 处理对象上无效的shader，这个主要是因为shdar编辑问题造成的，按理来说应该
    /// 每个shader都对应具体机型进行编程。
    /// </summary>
    /// <param name="_gameobject"></param>
    static public void excuteShader(GameObject _gameobject)
    {
        Renderer[] renders = _gameobject.transform.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rd in renders)
        {
            if (rd != null && rd.sharedMaterial != null)
            {
                //if (rd.sharedMaterial.shader.isSupported == false)
                {
                    //Debugger.Log("Not Support mat:" + rd.sharedMaterial.name + ",shader:" + rd.sharedMaterial.shader.name);
                    rd.sharedMaterial.shader = Shader.Find(rd.sharedMaterial.shader.name);
                }
                //Debug.Log("@@@@@@@Out put shareMaterial Name:" + rd.sharedMaterial.name);
            }
        }
    }
}
