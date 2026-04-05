using UnityEngine;

namespace RepoDeltaForceMod;

[DisallowMultipleComponent]
public sealed class HavocSupplyIdentity : MonoBehaviour
{
    [SerializeField]
    private string stableId = string.Empty;

    [SerializeField]
    private string displayName = string.Empty;

    [SerializeField]
    private string prefabRootName = string.Empty;

    public string StableId => stableId;

    public string DisplayName => displayName;

    public string PrefabRootName => prefabRootName;

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(prefabRootName))
        {
            prefabRootName = gameObject.name;
        }
    }
}
