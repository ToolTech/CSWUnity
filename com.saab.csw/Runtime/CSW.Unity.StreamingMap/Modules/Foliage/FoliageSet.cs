/* 
 * Copyright (C) SAAB AB
 *
 * All rights, including the copyright, to the computer program(s) 
 * herein belong to Saab AB. The program(s) may be used and/or
 * copied only with the written permission of Saab AB, or in
 * accordance with the terms and conditions stipulated in the
 * agreement/contract under which the program(s) have been
 * supplied. 
 * 
 * Information Class:          COMPANY RESTRICTED
 * Defence Secrecy:            UNCLASSIFIED
 * Export Control:             NOT EXPORT CONTROLLED
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CSW.Unity.StreamingMap
{
    [CreateAssetMenu(fileName = "FoliageSet", menuName = "Terrain/FoliageSet")]
    public class FoliageSet : ScriptableObject
    {
        [SerializeField]
        public List<MappedFoliageAsset> Assets;

        private List<Foliage> _foliages = new List<Foliage>();
        public List<Foliage> GetFoliageList
        { 
            get 
            {
                _foliages.Clear();
                foreach (var item in Assets)
                {
                    _foliages.Add(item.Foliage);
                }
                return _foliages; 
            } 
        }

        public float GetMaxHeight
        {
            get
            {
                float max = 0;
                foreach (var item in Assets)
                {
                    max = MathF.Max(item.Foliage.MaxMin.y, max);
                }
                return max;
            }
        }

    }

    [Serializable]
    public struct MappedFoliageAsset
    {
        [SerializeField]
        public Foliage Foliage;
        [SerializeField]
        public float Weight;
    }
}