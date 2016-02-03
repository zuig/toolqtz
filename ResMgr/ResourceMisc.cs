using UnityEngine;
using UnityEngine.Assertions;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace ResourceMisc
{

    //---------------------------------------
    // bundle ref wrapper
    public class BundleWrapper
    {
        string _path;
        AssetBundle _bundle;
        HashSet<string> _refAssets = new HashSet<string>();

        public BundleWrapper(string path, AssetBundle bundle)
        {
            _path = path;
            _bundle = bundle;
            _refAssets.Clear();
        }

        public void AddRefAsset(string assetKey)
        {
            Assert.IsFalse(_refAssets.Contains(assetKey), string.Format("资源{0}已经被{1}引用!", assetKey, _path));
            _refAssets.Add(assetKey);
        }

        public void DecRefAsset(string assetKey)
        {
            Assert.IsTrue(_refAssets.Contains(assetKey), string.Format("{0}不存在于bundle{1}", assetKey, _path));
            _refAssets.Remove(assetKey);
        }

        public int GetRefAssetCount()
        {
            return _refAssets.Count;
        }

        public AssetBundle GetBundle()
        {
            return _bundle;
        }

        public string GetPath()
        {
            return _path;
        }
    }
    //---------------------------------------
    // asset ref wrapper
    public class AssetWrapper
    {
        int _refCount;
        Object _asset;
        string _path;
        System.Type _assetType;
        string _bundleName;
        //该资源所依赖的所有bundle的名称;
        string [] _refBundle;
        
        public AssetWrapper(Object asset, string path, System.Type assetType, string bundle_name, string[] refBundle)
        {
            if(asset != null)
            {
                GameObject temp = asset as GameObject;
                if (temp != null)
                {
                    ResourceManager.excuteShader(temp);
                }
            }

            _refCount = 1;
            _asset = asset;
            _path = path;
            _assetType = assetType;
            _bundleName = bundle_name;
            _refBundle = refBundle;
        }

        public string[] GetRefBundle()
        {
            return _refBundle;
        }

        public void AddRef()
        {
            ++_refCount;
        }

        public void Release()
        {
            Assert.IsTrue(_refCount > 0, string.Format("{0} 引用计数出错", _refCount));
            --_refCount;
        }

        public int GetRefCount()
        {
            return _refCount;
        }

        public Object GetAsset()
        {
            return _asset;
        }

        public string GetAssetPath()
        {
            return _path;
        }

        public System.Type GetAssetType()
        {
            return _assetType;
        }

        public string GetBundleName()
        {
            return _bundleName;
        }
    }

}