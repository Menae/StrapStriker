using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// シーンごとのBGMを自動切り替えするマネージャー。
/// シングルトンパターンで、シーン遷移時にも破棄されない。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BGMManager : MonoBehaviour
{
    public static BGMManager instance;

    /// <summary>
    /// シーンとBGMの紐付けを定義するクラス。
    /// </summary>
    [System.Serializable]
    public class SceneBGM
    {
        [Tooltip("対象のシーン名")]
        public string sceneName;

        [Tooltip("そのシーンで流すBGM")]
        public AudioClip bgmClip;

        [Tooltip("BGMの音量")]
        [Range(0f, 1f)]
        public float volume = 0.7f;
    }

    [Header("シーンごとのBGM設定")]
    [Tooltip("各シーンと、そこで流すBGMのリスト")]
    public List<SceneBGM> sceneBgmList;

    private AudioSource audioSource;
    private string currentSceneName;

    /// <summary>
    /// 初期化処理。シングルトンの確立とAudioSourceの設定を行う。
    /// </summary>
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
    }

    /// <summary>
    /// オブジェクトが有効化された時、シーンロードイベントに登録する。
    /// </summary>
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// オブジェクトが無効化された時、シーンロードイベントから登録解除する。
    /// </summary>
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// シーンがロードされた時に実行される。
    /// シーン名に応じたBGMを検索し、異なる曲であれば切り替える。
    /// </summary>
    /// <param name="scene">ロードされたシーン</param>
    /// <param name="mode">ロードモード</param>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == currentSceneName)
        {
            return;
        }
        currentSceneName = scene.name;

        foreach (var sceneBgm in sceneBgmList)
        {
            if (sceneBgm.sceneName == scene.name)
            {
                if (audioSource.clip == sceneBgm.bgmClip)
                {
                    return;
                }

                audioSource.Stop();
                audioSource.clip = sceneBgm.bgmClip;
                audioSource.volume = sceneBgm.volume;
                audioSource.Play();

                return;
            }
        }
    }
}