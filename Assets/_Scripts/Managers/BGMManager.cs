using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class BGMManager : MonoBehaviour
{
    public static BGMManager instance;

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

    void Awake()
    {
        if (instance == null)
        {
            // 誰もインスタンスを持っていなければ、自分がインスタンスになる
            instance = this;
            // シーンをまたいでも破棄されないようにする
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 既にインスタンスが存在する場合は、自分を破棄する
            Destroy(gameObject);
            return;
        }

        // 自身のAudioSourceコンポーネントを取得し、ループ再生を有効にする
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
    }

    void OnEnable()
    {
        // シーンがロードされた時に呼ばれるメソッドを登録
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        // オブジェクトが破棄される時に、登録を解除
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // シーンがロードされた時に実行されるメソッド
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // もしロードされたのが同じシーンなら、BGMは変更しない
        if (scene.name == currentSceneName)
        {
            return;
        }
        currentSceneName = scene.name;

        // 設定リストの中から、新しいシーン名に一致するBGMを探す
        foreach (var sceneBgm in sceneBgmList)
        {
            if (sceneBgm.sceneName == scene.name)
            {
                // もし既に同じ曲が流れていたら、何もしない
                if (audioSource.clip == sceneBgm.bgmClip)
                {
                    return;
                }

                // BGMを新しい曲に設定し、再生する
                audioSource.Stop();
                audioSource.clip = sceneBgm.bgmClip;
                audioSource.volume = sceneBgm.volume;
                audioSource.Play();

                // 対応するBGMが見つかったのでループを抜ける
                return;
            }
        }
    }
}