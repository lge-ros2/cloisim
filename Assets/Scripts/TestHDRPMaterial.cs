using UnityEngine;
public class TestHDRPMaterial : MonoBehaviour {
    void Start() {
        var baseMat = Resources.Load<Material>("Materials/Material(white)");
        Debug.Log("[TestHDRPMaterial] Loaded Material(white): " + (baseMat != null ? baseMat.shader.name : "null"));
    }
}
