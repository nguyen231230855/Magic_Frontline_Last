using UnityEngine;

[CreateAssetMenu(fileName = "Encounter", menuName = "Magic Frontline/Encounter")]
public class EncounterCard : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    [TextArea] public string strength;
    [TextArea] public string weakness;
}
