using UnityEngine;
using UnityEngine.XR;
using FMODUnity;

public class WindChimeGame : MonoBehaviour
{
    [Header("风粒子设置")]
    public GameObject windParticlePrefab;
    public Transform windSpawnPlane;
    public Vector2 spawnSize = new Vector2(5f, 3f);
    public Vector2 windSpeedRange = new Vector2(1f, 3f);
    public Vector2 spawnIntervalRange = new Vector2(0.5f, 2f);

    [Header("玩家手部")]
    public Transform leftHand;
    public Transform rightHand;
    public float catchRadius = 0.3f;

    [Header("特效与音效")]
    public ParticleSystem catchEffect;

    [Header("FMOD音效")]
    [EventRef] public string catchSoundEvent;

    [Header("游戏逻辑")]
    public int maxCatches = 10;
    private int currentCatches = 0;
    private bool gameActive = true;

    [Header("物品激活")]
    public GameObject itemAt5;   // 第5次捕捉启用
    public GameObject itemAt12;  // 第12次捕捉启用

    private float nextSpawnTime;

    void Start()
    {
        nextSpawnTime = Time.time + Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);

        // 确保两个物品初始为不激活
        if (itemAt5 != null) itemAt5.SetActive(false);
        if (itemAt12 != null) itemAt12.SetActive(false);
    }

    void Update()
    {
        if (!gameActive) return;

        if (Time.time >= nextSpawnTime)
        {
            SpawnWindParticle();
            nextSpawnTime = Time.time + Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
        }

        DetectCatches();
    }

    void SpawnWindParticle()
    {
        Vector3 spawnPos = windSpawnPlane.position +
                          windSpawnPlane.right * Random.Range(-spawnSize.x / 2, spawnSize.x / 2) +
                          windSpawnPlane.up * Random.Range(-spawnSize.y / 2, spawnSize.y / 2);

        GameObject wind = Instantiate(windParticlePrefab, spawnPos, Quaternion.identity);
        WindParticle wp = wind.AddComponent<WindParticle>();

        wp.speed = Random.Range(windSpeedRange.x, windSpeedRange.y);
        wp.direction = -windSpawnPlane.forward;

        Destroy(wind, 10f);
    }

    void DetectCatches()
    {
        WindParticle[] winds = FindObjectsOfType<WindParticle>();

        foreach (WindParticle wind in winds)
        {
            if (Vector3.Distance(leftHand.position, wind.transform.position) < catchRadius)
            {
                CatchWind(wind.gameObject, leftHand.position);
                break;
            }

            if (Vector3.Distance(rightHand.position, wind.transform.position) < catchRadius)
            {
                CatchWind(wind.gameObject, rightHand.position);
                break;
            }
        }
    }

    void CatchWind(GameObject wind, Vector3 catchPosition)
    {
        if (!string.IsNullOrEmpty(catchSoundEvent))
        {
            RuntimeManager.PlayOneShot(catchSoundEvent, catchPosition);
        }

        if (catchEffect != null)
        {
            ParticleSystem effect = Instantiate(catchEffect, catchPosition, Quaternion.identity);
            Destroy(effect.gameObject, 2f);
        }

        currentCatches++;

        // 启用第5次的物品
        if (currentCatches == 5 && itemAt5 != null)
        {
            itemAt5.SetActive(true);
        }

        // 启用第12次的物品
        if (currentCatches == 12 && itemAt12 != null)
        {
            itemAt12.SetActive(true);
        }

        if (currentCatches >= maxCatches)
        {
            gameActive = false;
            Debug.Log("游戏结束，已达到最大捕捉次数。");
        }

        Destroy(wind);
    }
}

public class WindParticle : MonoBehaviour
{
    public float speed;
    public Vector3 direction;

    void Update()
    {
        transform.position += direction.normalized * speed * Time.deltaTime;
    }
}
