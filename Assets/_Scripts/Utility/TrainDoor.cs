using System.Collections;
using UnityEngine;

public class TrainDoor : MonoBehaviour
{
    [Header("ドアのオブジェクト設定")]
    public GameObject leftDoor;
    public GameObject rightDoor;

    [Header("ドアの動作設定")]
    public float openDuration = 0.5f;
    public float stayOpenTime = 2.0f;
    public float openDistance = 3.0f;

    private Vector3 leftDoorInitialPos;
    private Vector3 rightDoorInitialPos;
    private bool isAnimating = false;

    void Start()
    {
        if (leftDoor != null) leftDoorInitialPos = leftDoor.transform.position;
        if (rightDoor != null) rightDoorInitialPos = rightDoor.transform.position;
    }
    void OnEnable()
    {
        DoorManager.Register(this);
    }

    void OnDisable()
    {
        DoorManager.Unregister(this);
    }

    public void OpenAndClose()
    {
        if (isAnimating) return;
        StartCoroutine(OpenAndCloseRoutine());
    }

    private IEnumerator OpenAndCloseRoutine()
    {
        isAnimating = true;

        float timer = 0f;
        Vector3 leftDoorTargetPos = leftDoorInitialPos + Vector3.left * openDistance;
        Vector3 rightDoorTargetPos = rightDoorInitialPos + Vector3.right * openDistance;

        while (timer < openDuration)
        {
            float progress = timer / openDuration;
            if (leftDoor != null) leftDoor.transform.position = Vector3.Lerp(leftDoorInitialPos, leftDoorTargetPos, progress);
            if (rightDoor != null) rightDoor.transform.position = Vector3.Lerp(rightDoorInitialPos, rightDoorTargetPos, progress);
            timer += Time.deltaTime;
            yield return null;
        }
        if (leftDoor != null) leftDoor.transform.position = leftDoorTargetPos;
        if (rightDoor != null) rightDoor.transform.position = rightDoorTargetPos;

        yield return new WaitForSeconds(stayOpenTime);

        timer = 0f;
        while (timer < openDuration)
        {
            float progress = timer / openDuration;
            if (leftDoor != null) leftDoor.transform.position = Vector3.Lerp(leftDoorTargetPos, leftDoorInitialPos, progress);
            if (rightDoor != null) rightDoor.transform.position = Vector3.Lerp(rightDoorTargetPos, rightDoorInitialPos, progress);
            timer += Time.deltaTime;
            yield return null;
        }
        if (leftDoor != null) leftDoor.transform.position = leftDoorInitialPos;
        if (rightDoor != null) rightDoor.transform.position = rightDoorInitialPos;

        isAnimating = false;
    }
}