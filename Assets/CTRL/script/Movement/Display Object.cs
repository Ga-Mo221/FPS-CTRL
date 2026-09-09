using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

public class DisplayObject : MonoBehaviour
{
    [SerializeField] private bool AutoDisable = false;

    [ShowIf("AutoDisable")] [SerializeField]
    private float time = 2f;
    
    [HideIf("AutoDisable")]
    [SerializeField] private GameObject[] Objects;

    private void Start()
    {
        if (AutoDisable)
            StartCoroutine(Cor_Disable(time));
    }

    private void OnEnable()
    {
        if (AutoDisable)
            StartCoroutine(Cor_Disable(time));
    }

    public void DisableObj(int index)
    {
        if (index >= 0 && index < Objects.Length)
        {
            Objects[index].SetActive(false);
        }
    }
    
    public void EnableObj(int index)
    {
        if (index >= 0 && index < Objects.Length)
            Objects[index].SetActive(true);
    }

    private IEnumerator Cor_Disable(float time)
    {
        yield return new WaitForSeconds(time);
        gameObject.SetActive(false);
    }
}
