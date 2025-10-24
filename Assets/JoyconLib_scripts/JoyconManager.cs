using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using System;

public class JoyconManager : MonoBehaviour
{
    // Settings accessible via Unity
    public bool EnableIMU = true;
    public bool EnableLocalize = true;

    // Different operating systems either do or don't like the trailing zero
    private const ushort vendor_id = 0x57e;
    private const ushort vendor_id_ = 0x057e;
    private const ushort product_l = 0x2006;
    private const ushort product_r = 0x2007;

    public List<Joycon> j; // Array of all connected Joy-Cons
    public static JoyconManager instance;

    public static JoyconManager Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        j = new List<Joycon>();
        HIDapi.hid_init();

        IntPtr ptr = HIDapi.hid_enumerate(vendor_id, 0x0);
        IntPtr top_ptr = ptr;

        if (ptr == IntPtr.Zero)
        {
            ptr = HIDapi.hid_enumerate(vendor_id_, 0x0);
            if (ptr == IntPtr.Zero)
            {
                HIDapi.hid_free_enumeration(ptr);
                Debug.Log("No Joy-Cons found!");
            }
        }

        hid_device_info enumerate;
        while (ptr != IntPtr.Zero)
        {
            enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));

            if (enumerate.product_id == product_l || enumerate.product_id == product_r)
            {
                bool isLeft = enumerate.product_id == product_l;
                string side = isLeft ? "Left" : "Right";
                Debug.Log($"{side} Joy-Con connected.");

                IntPtr handle = HIDapi.hid_open_path(enumerate.path);
                HIDapi.hid_set_nonblocking(handle, 1);
                j.Add(new Joycon(handle, EnableIMU, EnableLocalize & EnableIMU, 0.05f, isLeft));
            }
            ptr = enumerate.next;
        }
        HIDapi.hid_free_enumeration(top_ptr);
    }

    void Start()
    {
        for (int i = 0; i < j.Count; ++i)
        {
            Joycon jc = j[i];
            byte LEDs = (byte)(0x1 << i);
            jc.Attach(leds_: LEDs);
            jc.Begin();
        }
    }

    void Update()
    {
        for (int i = 0; i < j.Count; ++i)
        {
            j[i].Update();
        }
    }

    // Unityエディタでの再生停止時に呼ばれる
    void OnDestroy()
    {
        Cleanup();
    }

    // ビルドしたゲームの終了時に呼ばれる
    void OnApplicationQuit()
    {
        Cleanup();
    }

    // 終了処理を共通化
    private void Cleanup()
    {
        if (j != null)
        {
            for (int i = 0; i < j.Count; ++i)
            {
                j[i].Detach();
            }
        }
        // HIDAPIをクリーンアップ
        HIDapi.hid_exit();
    }
}