using UnityEngine;
using System.Collections;
using FMODUnity;

public class Eat : MonoBehaviour
{
    public int score = 0; // 计数器，用于记录碰到的预制体数量
    public string prefabTag = "Collectible"; // 预制体的标签，用于识别需要消失的预制体
    public GameObject fallingObject; // 需要下落的物体
    public float fallDistance = 10f; // 下落的距离
    public float fallSpeed = 5f; // 下落的速度

    [EventRef] public string catchSoundEvent;

    private bool objectFalling = false; // 判断物体是否已经开始下落

    // 触发碰撞时调用的方法
    private void OnTriggerEnter(Collider other)
    {
        // 判断碰撞的物体是否为指定标签的预制体
        if (other.CompareTag(prefabTag))
        {
            // 增加计数器
            score++;
            RuntimeManager.PlayOneShot(catchSoundEvent);

            // 输出当前分数
            Debug.Log("Score: " + score);

            // 销毁被碰撞的预制体
            Destroy(other.gameObject);

            // 判断是否达到第七个，启动下落物体
            if (score == 9 && !objectFalling)
            {
                StartFallingObject();
            }
        }
    }

    // 启动下落物体的方法
    private void StartFallingObject()
    {
        // 启用物体
        fallingObject.SetActive(true);

        // 启动下落的协程
        StartCoroutine(FallCoroutine());
    }

    // 下落协程
    private IEnumerator FallCoroutine()
    {
        objectFalling = true;

        Vector3 startPosition = fallingObject.transform.position;
        Vector3 targetPosition = startPosition + Vector3.down * fallDistance;

        float elapsedTime = 0f;

        // 模拟下落过程
        while (elapsedTime < fallDistance / fallSpeed)
        {
            fallingObject.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / (fallDistance / fallSpeed));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 确保物体最终落到目标位置
        fallingObject.transform.position = targetPosition;

        objectFalling = false;
    }
}
