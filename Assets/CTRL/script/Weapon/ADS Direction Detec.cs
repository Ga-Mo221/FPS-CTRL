using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
public class ADSDirectionDetec : MonoBehaviour
{
    public Transform End;

    [Button]
    public void SetForward()
    {
        transform.forward = End.position - transform.position;
    }
}
#endif
