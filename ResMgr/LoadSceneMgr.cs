/*
 * Copyright (c) 2015,广州擎天柱网络科技有限公司
 * All rights reserved.
 *
 * 文件名称：LoadSceneMgr.cs
 * 简    述：控制场景的更新，在此进行特效和角色资源的预加载，导出到。
 * 创建标识：Lorry 2015/10/19 
 */

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 进度条相关逻辑代码
/// </summary>
public class LoadingBar : MonoBehaviour
{
    private Image m_image;
    private Text m_text;
    void Awake()
    {
        //GameManager gameMgr = GameObject.Find("GameManager") as GameManager;
        m_image = GameObject.Find("ProgC_Loading").GetComponent("Image") as Image;
        m_text = GameObject.Find("Text").GetComponent("Text") as Text;
    }

    /// <summary>
    /// 设置加载进度条的百分比
    /// </summary>
    /// <param name="percentage"></param>
    public void SetPercentage(float percentage)
    {
        m_image.fillAmount = percentage;
    }

    //Tip集合，实际开发中需要从外部文件中读取
    private string[] mTips = new string[]
                  {
                    "异步加载过程中你可以浏览游戏攻略",
                    "异步加载过程中你可以查看当前进度",
                    "异步加载过程中你可以判断是否加载完成",
                    "不理解现在读条为什么那样慢",
                    "难道是因为DOOM不懂Unity3D",
                  };

    void Update()
    {
        if(m_text != null)
        {
            m_text.text = mTips[Random.Range(0, 5)] + "(" + m_image.fillAmount * 100 + "%" + ")";
        }
    }

}

/// <summary>
/// 场景加载完成之后的代理
/// </summary>
/// <param name="level"></param>
public delegate void LevelLoadedDelegate(int level);

/// <summary>
/// 场景切换加载的管理，同时处理了预加载和进度条。已经导出luawrap
/// </summary>
public class LoadSceneMgr
{
    static LoadSceneMgr m_instance;
    /// <summary>
    /// 场景加载单例，便于在lua中调用
    /// </summary>
    public static LoadSceneMgr Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = new LoadSceneMgr();
            }
            return m_instance;
        }
    }

    private LoadingBar m_bar;
    /// <summary>
    /// 本来命名为scene,现在决定与Application.LoadLevel对应
    /// </summary>
    private string m_levelToLoad;
    private string m_levelAssetToLoad;

    private ResourceMisc.AssetWrapper m_levelAsset;
    //需要预加载的资源列表;
    private List<string> m_resourceList = new List<string>();
    private List<int> m_goCountInPool = new List<int>();

    /// <summary>
    /// 判断是否由LoadSceneMgr控制载入;
    /// </summary>
    //public bool IsStartLoad = false;

    //用于回调的
    private LevelLoadedDelegate m_onLevelLoadedFunc;
    
    #region //公共函数初始化
    /// <summary>
    /// 注册加载完成所有资源后调用的函数，这里应该是ulua函数的调用。
    /// </summary>
    /// <param name="func"></param>
    public void RegLevelLoaded(LevelLoadedDelegate func)
    {
        m_onLevelLoadedFunc = func;
    }

    private void init(GameObject load)
    {
        //GameObject load = GameObject.Find("LoadCanvas");
        if(load == null)
        {
            Debug.LogError("There is not LoadScene");
            return;
        }
        m_bar = load.AddComponent<LoadingBar>();
        GameObject.DontDestroyOnLoad(load);
    }
    /// <summary>
    /// 设置需要载入场景的名字
    /// </summary>
    /// <param name="sceneName"></param>
    public void SetLoadScene(string sceneName)
    {
        m_levelToLoad = sceneName;
        m_levelAssetToLoad = "Assets/Scenes/" + m_levelToLoad + "/" + m_levelToLoad + ".unity";
    }
    /// <summary>
    /// 设置需要载入的场景名，及场景所需的assetbundle资源名称
    /// </summary>
    /// <param name="assetName">资源名</param>
    /// <param name="sceneName">场景level名</param>
    public void SetLoadScene(string assetName, string sceneName)
    {
        m_levelAssetToLoad = assetName;
        m_levelToLoad = sceneName;
    }

    /// <summary>
    /// 在添加预加载对象
    /// </summary>
    /// <param name="prefabName">prefab的相对路径及文件名</param>
    /// <param name="count">在池中实例化对象的个数，按需填写</param>
    public void AddPreLoadPrefab(string prefabName, int count = 0)
    {
        m_resourceList.Add(prefabName);
        m_goCountInPool.Add(count);
    }

    /// <summary>
    /// 开始进入加载流程，首先载入空的场景，清理掉原来场景中的资源
    /// </summary>
    public void StartLoad()
    {
        Application.LoadLevel("empty");
    }
    #endregion
    
    #region 为了处理Loading背景和进度条而编写的函数(部分由GameManager调用)
    /// <summary>
    /// 载入空场景之后，显示loading背景和进度条
    /// </summary>
    public void OnLoadEmptylevel()
    {
        ResourceManager.Instance.ClearAllAsset();
        ResourceMisc.AssetWrapper asset = ResourceManager.Instance.LoadAsset(Downloading.LOAD_UI, typeof(GameObject));
        GameObject obj = GameObject.Instantiate(asset.GetAsset()) as GameObject;
        init(obj);
        m_bar.SetPercentage(0);
        ResourceManager.Instance.ReleaseAsset(asset);
        m_bar.StartCoroutine(OnSceneAssetLoading());
    }

    IEnumerator OnSceneAssetLoading()
    {
        Debug.Log("Time StartLoadLevelAsset:" + Time.realtimeSinceStartup);
        if(m_levelAssetToLoad!= "")
        {
            m_levelAsset = ResourceManager.Instance.LoadScene(m_levelAssetToLoad);
        }
        m_bar.SetPercentage(0.1f);
        Debug.Log("Time StartLoadLevel:" + Time.realtimeSinceStartup);
        yield return new WaitForEndOfFrame();
        //如果场景特别大，这里应该用
        Application.LoadLevel(m_levelToLoad);
    }

    int m_resIndex;
    int m_level;
    /// <summary>
    /// 场景加载完后，处理预加载资源，现在是按场景占据进度条0.3，预加载资源占据0.7来划分的。
    /// 并不是精确的数字，为了进度条好看而已 Lorry
    /// </summary>
    /// <param name="level"></param>
    public void OnLoadScnene(int level)
    {
        Debug.Log("Time LoadLevelEnd:" + Time.realtimeSinceStartup);
        m_bar.SetPercentage(0.3f);
        m_resIndex = 0;
        if (m_resourceList.Count == 0)
        {
            OnEndLoad();
            return;
        }

        m_level = level;
        m_bar.StartCoroutine(OnPreLoadingRes());
    }

    IEnumerator OnPreLoadingRes()
    {
        int resCount = m_resourceList.Count;
        float delta = 0.7f / resCount;

        int count = m_goCountInPool[m_resIndex];
        if (count == 0)
        {
            ResourceManager.Instance.LoadAsset(m_resourceList[m_resIndex], typeof(GameObject));
        }
        else
        {
            SimplePool.Instance.CreatePrefabPool(m_resourceList[m_resIndex], count);
        }
        m_bar.SetPercentage(0.3f + m_resIndex * delta);
        m_resIndex++;
        if (m_resIndex < resCount)
        {
            yield return new WaitForSeconds(0.1f);
            m_bar.StartCoroutine(OnPreLoadingRes());
            //yield return new WaitForEndOfFrame();
        }
        else
        {
            yield return new WaitForEndOfFrame();
            Debug.Log("PreLoadingRes Over!!!!!!");
            //IsStartLoad = false;
            m_resourceList.Clear();
            m_goCountInPool.Clear();
            OnEndLoad();
        }
    }

    //场景及预加载资源完毕时进行的操作;
    void OnEndLoad()
    {
        if (m_levelAsset != null)
        {
            ResourceManager.Instance.ReleaseAsset(m_levelAsset);
            m_levelAsset = null;
        }
        ////处理map的无效shader
        //GameObject _gameobject = GameObject.Find("map");
        //if (_gameobject != null)
        //    ProjectileBase.excuteShader(_gameobject);
        //资源加载完成之后，所有camera 的初始化和其他变化还是交给lua处理。
        //AudioListener temp = m_bar.gameObject.GetComponent<AudioListener>();
        GameObject.Destroy(m_bar.gameObject);

        //这里才调用lua的对应函数做进度;
        m_onLevelLoadedFunc(m_level);
        //ioo.gameManager.uluaMgr.OnLevelLoaded(m_level);
    }
    #endregion

}

