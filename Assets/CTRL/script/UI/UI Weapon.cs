using TMPro;
using UnityEngine;

public class UIWeapon : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _maxBullet;
    [SerializeField] private TextMeshProUGUI _CurrentBullet;

    public void SetBullet(int currentBullet, int maxBullet, WeaponData data)
    {
        _maxBullet.text = $"{maxBullet} / {data.MaxBullet}";
        _CurrentBullet.text = $"{currentBullet} / {data.DefaultBullet}";
    }
}
