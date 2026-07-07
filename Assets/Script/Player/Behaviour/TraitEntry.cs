using UnityEngine;

/// <summary>
/// A single trait entry on a CharacterCard.
/// References a TraitDefinition asset and holds a configured value.
/// </summary>
[System.Serializable]
public class TraitEntry
{
    public TraitDefinition definition;
    [Range(0f, 1f)] public float value = 0.5f;
}
