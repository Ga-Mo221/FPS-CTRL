using UnityEngine;

public class BodyPartsHandler : MonoBehaviour
{
    [SerializeField] private BodyParts _bodyParts;
    public BodyParts GetBodyParts() => _bodyParts;
}

[System.Serializable]
public enum BodyParts
{
    Head,
    Body,
    Legs,
    Hand
}
