//******************************************************************************
//
// Copyright (C) SAAB AB
//
// All rights, including the copyright, to the computer program(s) 
// herein belong to Saab AB. The program(s) may be used and/or
// copied only with the written permission of Saab AB, or in
// accordance with the terms and conditions stipulated in the
// agreement/contract under which the program(s) have been
// supplied. 
//
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
//
// File			: MaterialManager.cs
// Module		:
// Description	: Helper for Material and state uploads
// Author		: Anders Modén
// Product		: Gizmo3D 2.12.326
//
// NOTE:	Gizmo3D is a high performance 3D Scene Graph and effect visualisation 
//			C++ toolkit for Linux, Mac OS X, Windows, Android, iOS and HoloLens for  
//			usage in Game or VisSim development.
//
//
// Revision History...
//
// Who	Date	Description
//
// Albni 250326	Created file                                        
// AMO	261104	Moved to namespace CSW.Unity.	(2.12.327)
//
//******************************************************************************

// Framework
using System.Collections.Generic;

// Unity
using UnityEngine;

// GizmoSDK

namespace CSW.Unity
{
    public class MaterialManager
    {
        private struct MaterialCacheItem
        {
            public Material Material;
            public int RefCount;
        }

        private readonly Dictionary<int, MaterialCacheItem> _MaterialCache = new Dictionary<int, MaterialCacheItem>();
        private readonly Dictionary<Material, int> _lookup = new Dictionary<Material, int>();

        public bool TryAdd(int key, Material value)
        {
            if (_MaterialCache.TryAdd(key, new MaterialCacheItem() { Material = value, RefCount = 1 }))
            {
                // add a reverse lookup to support the free operation
                _lookup.Add(value, key);
                return true;
            }

            // resource already existed, no operation performed
            return false;
        }

        public bool TryGet(int key, out Material value)
        {
            if (_MaterialCache.TryGetValue(key, out MaterialCacheItem item))
            {
                // item existed in the cache, increase the ref count and return the resource
                item.RefCount++;
                _MaterialCache[key] = item;

                value = item.Material;
                return true;
            }

            // failed to find the given resource
            value = null;
            return false;
        }

        public bool Free(Material Material)
        {
            if (_lookup.TryGetValue(Material, out int key))
            {
                MaterialCacheItem item = _MaterialCache[key];
                item.RefCount--;

                // check if this was the last reference
                if (item.RefCount > 0)
                {
                    // simply update the ref counter
                    _MaterialCache[key] = item;
                    return true;
                }

                // this was the last reference for the Material, we should release it
                _lookup.Remove(Material);
                _MaterialCache.Remove(key);
                GameObject.Destroy(Material);
                return true;
            }

            // failed to find the given resource, no operation performed
            return false;
        }

        public void Clear()
        {
            foreach (var kvp in _lookup)
                GameObject.Destroy(kvp.Key);

            _lookup.Clear();
            _MaterialCache.Clear();
        }
    }
}
