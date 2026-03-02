using UnityEngine;
using UnityEngine.Rendering;
public class CheckCoreUtils : MonoBehaviour {
    void Start() {
        var shader = Shader.Find("HDRP/Lit");
        var mat = CoreUtils.CreateEngineMaterial(shader);
        Debug.Log("[CheckCoreUtils] Material created using CoreUtils: " + (mat != null));
    }
}
