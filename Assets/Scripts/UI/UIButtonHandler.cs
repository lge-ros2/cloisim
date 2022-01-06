
using UnityEngine;

public class UIButtonHandler : MonoBehaviour
{
    private GameObject modelList = null;

    void Awake()
	{
        modelList = transform.parent.Find("ModelList").gameObject;
    }

    public void OnButtonClickedAddModel()
    {
        modelList.SetActive(!modelList.activeSelf);
    }
}