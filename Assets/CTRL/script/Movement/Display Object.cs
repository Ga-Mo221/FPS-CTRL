using UnityEngine;

public class DisplayObject : MonoBehaviour
{
    [SerializeField] private GameObject[] Objects;

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
}
