using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.VFX;

public class DissolvingController : MonoBehaviour
{
    [SerializeField] private float _dissolveRate = 0.0125f;
    [SerializeField] private float _refreshRate = 0.025f;
    
    [SerializeField] private SkinedMeshInfo[] _skinedMeshInfos;
    
    [SerializeField] private Material[] _all_dissolveMaterials;

    private void Start()
    {
        if (_skinedMeshInfos != null)
        {
            List<Material> materials = new();
            foreach (var info in _skinedMeshInfos)
            {
                if (info._skinedMeshs != null)
                    info._skinedMaterials = info._skinedMeshs.materials;
                materials.AddRange(info._dissolveMaterials);
            }
            if (_all_dissolveMaterials != null)
                _all_dissolveMaterials = materials.ToArray();
        }
        ResetMaterials();
    }

    [Button]
    public void GetOriginMaterials()
    {
        List<Material> materials = new();
        foreach (var info in _skinedMeshInfos)
        {
            if (info._skinedMeshs != null)
                info._skinedMaterials = info._skinedMeshs.sharedMaterials;
            materials.AddRange(info._dissolveMaterials);
        }
        _all_dissolveMaterials = materials.ToArray();
    }

    [Button]
    public void Dissolve()
    {
        Debug.Log("<color=blue>[DEAD]</color> Dissolve");
        ResetMaterials();
        
        if (_skinedMeshInfos != null)
        {
            foreach (var info in _skinedMeshInfos)
            {
                if (info._skinedMeshs != null)
                    info._skinedMeshs.materials = info._dissolveMaterials;
            }
        }
        StartCoroutine(Dissolveco());
    }

    [Button]
    public void ResetMaterials()
    {
        Debug.Log("<color=blue>[DEAD]</color> Reset Materials");
        if (_skinedMeshInfos != null)
        {
            foreach (var info in _skinedMeshInfos)
            {
                if (info._skinedMeshs != null)
                {
                    info._skinedMeshs.materials = info._skinedMaterials;
                    if (info._dissolveMaterials.Length > 0)
                        for (int i = 0; i < info._dissolveMaterials.Length; i++)
                            info._dissolveMaterials[i].SetFloat("_DissolveAmount", 0);
                }
            }
        }
    }

    IEnumerator Dissolveco()
    {
        if (_skinedMeshInfos != null)
        {
            foreach (var info in _skinedMeshInfos)
            {
                if (info._vfxGraph != null)
                {
                    info._vfxGraph.Play();
                }
            }
        }

        if (_all_dissolveMaterials != null && _all_dissolveMaterials.Length > 0)
        {
            float counter = 0;
            while (_all_dissolveMaterials[0].GetFloat("_DissolveAmount") < 1)
            {
                counter += _dissolveRate;
                for (int i = 0; i < _all_dissolveMaterials.Length; i++)
                {
                    _all_dissolveMaterials[i].SetFloat("_DissolveAmount", counter);
                }

                yield return new WaitForSeconds(_refreshRate);
            }
        }
    }
}

[System.Serializable]
public class SkinedMeshInfo
{
    public SkinnedMeshRenderer _skinedMeshs;
    public VisualEffect _vfxGraph;
    public Material[] _skinedMaterials;
    public Material[] _dissolveMaterials;
}
