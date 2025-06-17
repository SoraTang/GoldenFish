using UnityEngine;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;
using FMODUnity;

public class PhotoCapture : MonoBehaviour
{
    [Header("Settings")]
    public int maxPhotosPerRow = 5;          // 每行最多照片数量
    public float photoWidth = 0.3f;          // 照片宽度（单位：米）
    public float spacing = 0.05f;            // 照片间最小间距
    public float maxRotation = 15f;          // 最大随机旋转角度
    public float maxOffset = 0.1f;           // 最大随机偏移量
    public float photoForwardOffset = 0.01f; // 照片在法线方向的偏移（避免Z-fighting）
    public float topMargin = 0.5f;           // 顶部边距
    public int maxPhotos = 20;               // 最大照片数量
    public LayerMask captureLayerMask;       // 设置需要捕捉的层

    [Header("照片比例设置")]
    public bool useCustomAspectRatio = false; // 是否使用自定义长宽比
    [Range(0.1f, 3f)] public float aspectRatio = 1.7778f; // 照片长宽比（宽/高），默认16:9
    public CropMode cropMode = CropMode.FitToWidth; // 裁剪模式

    public enum CropMode
    {
        FitToWidth,    // 保持宽度，高度自适应（可能裁剪上下）
        FitToHeight,   // 保持高度，宽度自适应（可能裁剪左右）
        Letterbox,    // 完整显示（添加黑边）
        Stretch       // 拉伸填充（默认行为）
    }

    [Header("References")]
    public Transform photoWall;             // 陈列照片的平面
    public Camera captureCamera;            // 用于截图的相机

    private List<GameObject> photos = new List<GameObject>();
    private List<Vector3> gridPositions = new List<Vector3>(); // 存储所有网格位置
    private int totalPhotosTaken = 0;       // 总共拍摄的照片数量
    private bool isLeftTriggerPressed = false;  // 左手扳机状态
    private bool isRightTriggerPressed = false; // 右手扳机状态
    private bool canTakePhoto = true;       // 控制是否允许拍照
    private float rowHeight = 0f;           // 行高（根据照片比例计算）
    private int originalCullingMask;        // 记录原始相机的culling mask

    [EventRef] public string shot;

    void Start()
    {
        // 计算行高（基于照片宽高比）
        rowHeight = photoWidth * (Screen.height / (float)Screen.width);

        // 预先计算所有网格位置
        PrecalculateGridPositions();

        // 记录原始相机的culling mask
        if (captureCamera != null)
        {
            originalCullingMask = captureCamera.cullingMask;
        }
    }

    void PrecalculateGridPositions()
    {
        gridPositions.Clear();

        int rows = Mathf.CeilToInt(maxPhotos / (float)maxPhotosPerRow);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < maxPhotosPerRow; col++)
            {
                if (gridPositions.Count >= maxPhotos) break;

                float xPos = col * (photoWidth + spacing);
                float yPos = -row * (rowHeight + spacing) - topMargin;
                gridPositions.Add(new Vector3(xPos, yPos, 0));
            }
        }
    }

    void Update()
    {
        // 获取左右手柄设备
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        // 检测左手扳机状态
        bool leftTriggerPressed = false;
        if (leftHand.TryGetFeatureValue(CommonUsages.triggerButton, out bool leftPressed))
        {
            leftTriggerPressed = leftPressed;
        }

        // 检测右手扳机状态
        bool rightTriggerPressed = false;
        if (rightHand.TryGetFeatureValue(CommonUsages.triggerButton, out bool rightPressed))
        {
            rightTriggerPressed = rightPressed;
        }

        // 检查左手扳机按下事件
        if (leftTriggerPressed && !isLeftTriggerPressed && canTakePhoto)
        {
            StartCoroutine(CapturePhoto());
            canTakePhoto = false;
        }

        // 检查右手扳机按下事件
        if (rightTriggerPressed && !isRightTriggerPressed && canTakePhoto)
        {
            StartCoroutine(CapturePhoto());
            canTakePhoto = false;
        }

        // 当任一扳机释放时重置拍照能力
        if (!leftTriggerPressed && !rightTriggerPressed)
        {
            canTakePhoto = true;
        }

        // 更新扳机状态
        isLeftTriggerPressed = leftTriggerPressed;
        isRightTriggerPressed = rightTriggerPressed;
    }

    IEnumerator CapturePhoto()
    {
        // 等待渲染结束
        yield return new WaitForEndOfFrame();

        // 创建截图纹理
        RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        Camera mainCamera = Camera.main;

        // 设置临时相机用于截图
        captureCamera.CopyFrom(mainCamera);

        // 应用自定义的culling mask来排除UI层
        if (captureLayerMask != 0)
        {
            captureCamera.cullingMask = captureLayerMask;
        }

        captureCamera.targetTexture = renderTexture;
        captureCamera.Render();

        // 读取渲染纹理
        RenderTexture.active = renderTexture;
        Texture2D photoTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        photoTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        photoTexture.Apply();
        RenderTexture.active = null;

        // 清理资源
        captureCamera.targetTexture = null;

        // 恢复原始culling mask
        captureCamera.cullingMask = originalCullingMask;

        Destroy(renderTexture);

        // 如果需要处理长宽比
        if (useCustomAspectRatio)
        {
            photoTexture = ProcessAspectRatio(photoTexture);
        }

        // 创建照片对象
        CreatePhotoObject(photoTexture);

        RuntimeManager.PlayOneShot(shot);

        // 更新照片计数
        totalPhotosTaken++;
    }

    Texture2D ProcessAspectRatio(Texture2D originalTexture)
    {
        int originalWidth = originalTexture.width;
        int originalHeight = originalTexture.height;

        float screenAspect = (float)originalWidth / originalHeight;
        float targetAspect = aspectRatio;

        // 如果不使用自定义比例，使用屏幕比例
        if (!useCustomAspectRatio)
        {
            targetAspect = screenAspect;
        }

        // 计算裁剪/填充区域
        int newWidth, newHeight;
        float scale;
        Rect cropRect;

        switch (cropMode)
        {
            case CropMode.FitToWidth: // 保持宽度，裁剪高度
                newWidth = originalWidth;
                newHeight = Mathf.RoundToInt(originalWidth / targetAspect);
                scale = 1f;
                cropRect = new Rect(0, (originalHeight - newHeight) / 2, originalWidth, newHeight);
                break;

            case CropMode.FitToHeight: // 保持高度，裁剪宽度
                newWidth = Mathf.RoundToInt(originalHeight * targetAspect);
                newHeight = originalHeight;
                scale = 1f;
                cropRect = new Rect((originalWidth - newWidth) / 2, 0, newWidth, originalHeight);
                break;

            case CropMode.Letterbox: // 完整显示（添加黑边）
                if (screenAspect > targetAspect) // 屏幕更宽
                {
                    newWidth = Mathf.RoundToInt(originalHeight * targetAspect);
                    newHeight = originalHeight;
                }
                else // 屏幕更高
                {
                    newWidth = originalWidth;
                    newHeight = Mathf.RoundToInt(originalWidth / targetAspect);
                }

                // 创建新纹理并填充黑色
                Texture2D letterboxTexture = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
                Color[] blackPixels = new Color[newWidth * newHeight];
                for (int i = 0; i < blackPixels.Length; i++)
                {
                    blackPixels[i] = Color.black;
                }
                letterboxTexture.SetPixels(blackPixels);

                // 计算居中位置
                int pasteX = (newWidth - originalWidth) / 2;
                int pasteY = (newHeight - originalHeight) / 2;

                // 确保在有效范围内
                pasteX = Mathf.Clamp(pasteX, 0, newWidth - originalWidth);
                pasteY = Mathf.Clamp(pasteY, 0, newHeight - originalHeight);

                // 粘贴原始图像
                letterboxTexture.SetPixels(pasteX, pasteY, originalWidth, originalHeight, originalTexture.GetPixels());
                letterboxTexture.Apply();

                Destroy(originalTexture); // 销毁原始纹理
                return letterboxTexture;

            case CropMode.Stretch: // 拉伸填充（默认）
            default:
                newWidth = originalWidth;
                newHeight = originalHeight;
                cropRect = new Rect(0, 0, originalWidth, originalHeight);
                break;
        }

        // 对于裁剪模式，创建新纹理并复制像素
        if (cropMode == CropMode.FitToWidth || cropMode == CropMode.FitToHeight)
        {
            // 确保裁剪区域在有效范围内
            cropRect.width = Mathf.Min(cropRect.width, originalWidth);
            cropRect.height = Mathf.Min(cropRect.height, originalHeight);
            cropRect.x = Mathf.Clamp(cropRect.x, 0, originalWidth - cropRect.width);
            cropRect.y = Mathf.Clamp(cropRect.y, 0, originalHeight - cropRect.height);

            // 创建新纹理
            Texture2D croppedTexture = new Texture2D((int)cropRect.width, (int)cropRect.height, TextureFormat.RGB24, false);
            croppedTexture.SetPixels(originalTexture.GetPixels((int)cropRect.x, (int)cropRect.y, (int)cropRect.width, (int)cropRect.height));
            croppedTexture.Apply();

            Destroy(originalTexture); // 销毁原始纹理
            return croppedTexture;
        }

        // 对于拉伸模式，直接返回原始纹理
        return originalTexture;
    }

    void CreatePhotoObject(Texture2D photoTexture)
    {
        // 创建照片材质
        Material photoMaterial = new Material(Shader.Find("Unlit/Texture"));
        photoMaterial.mainTexture = photoTexture;

        // 创建照片Quad
        GameObject photo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(photo.GetComponent<Collider>()); // 移除碰撞体
        photo.name = "Photo_" + totalPhotosTaken;
        photo.transform.SetParent(photoWall);

        // 计算实际宽高比
        float textureAspect = (float)photoTexture.width / photoTexture.height;

        // 设置材质和大小
        photo.GetComponent<Renderer>().material = photoMaterial;
        photo.transform.localScale = new Vector3(photoWidth, photoWidth / textureAspect, 1);

        // 计算位置和旋转
        PlacePhotoWithRandomness(photo.transform);

        // 管理照片列表
        ManagePhotoList(photo);
    }

    void PlacePhotoWithRandomness(Transform photo)
    {
        // 计算网格位置索引（循环使用位置）
        int gridIndex = totalPhotosTaken % maxPhotos;

        // 获取基础网格位置
        Vector3 basePosition = gridPositions[gridIndex % gridPositions.Count];

        // 应用随机偏移
        Vector3 randomOffset = new Vector3(
            Random.Range(-maxOffset, maxOffset),
            Random.Range(-maxOffset, maxOffset),
            0
        );

        // 应用随机旋转
        float randomRotation = Random.Range(-maxRotation, maxRotation);

        // 转换到墙面空间
        Vector3 finalPosition = photoWall.TransformPoint(basePosition + randomOffset);
        Quaternion finalRotation = photoWall.rotation * Quaternion.Euler(0, 0, randomRotation);

        // 设置位置和旋转
        photo.position = finalPosition;
        photo.rotation = finalRotation;

        // 沿法线方向轻微偏移避免重叠
        photo.position += photoWall.forward * photoForwardOffset;
    }

    void ManagePhotoList(GameObject newPhoto)
    {
        // 如果照片数量超过最大值
        if (photos.Count >= maxPhotos)
        {
            // 计算要替换的照片索引
            int replaceIndex = totalPhotosTaken % maxPhotos;

            // 确保索引在有效范围内
            if (replaceIndex < photos.Count && photos[replaceIndex] != null)
            {
                // 销毁旧照片
                Destroy(photos[replaceIndex]);

                // 替换为新照片
                photos[replaceIndex] = newPhoto;
                return;
            }
        }

        // 如果未达到上限，直接添加到列表
        photos.Add(newPhoto);
    }
}