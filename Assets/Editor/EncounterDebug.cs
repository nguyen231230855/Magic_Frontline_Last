using UnityEditor;

public static class EncounterDebug
{
    [MenuItem("Magic Frontline/Clear discovered encounters")]
    static void Clear()
    {
        EncounterBook.Clear();
    }
}
