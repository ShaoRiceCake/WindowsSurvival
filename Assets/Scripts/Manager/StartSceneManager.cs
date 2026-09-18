using UnityEngine;
using UnityEngine.UI;

public class StartSceneManager : MonoBehaviour
{
    public GameObject StartButton;
    private void Awake()
    {
        StartButton.SetActive(true);
        Bind("EnterGame", () => { SaveHubUI.Ensure(); SaveHubUI.Instance.ShowRuns(); });
        Bind("Setting", () => { SaveHubUI.Ensure(); SaveHubUI.Instance.ShowSettings(); });
        Bind("Exit", OnExit);
    }
    private static void OnExit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    private void Bind(string name, UnityEngine.Events.UnityAction action)
    {
        var button = StartButton.transform.Find(name).GetComponent<Button>();
        button.onClick.RemoveAllListeners(); button.onClick.AddListener(action);
    }
}
