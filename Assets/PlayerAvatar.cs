using UnityEngine;

/// <summary>
/// 用基本几何体（球体 / 方块）在运行时拼出一个卡通小人，替换原来单调的方块外观：
/// 戴棒球帽的圆头小人，有眼睛、腮红、嘴、身体、四肢和鞋子；走路时上下起伏并朝移动方向轻微倾斜。
///
/// 由 createfruit 生成人物后调用 Build()，整体尺寸随 cellSize 缩放（人脚落在 y = 0 的地面上）。
/// </summary>
public class PlayerAvatar : MonoBehaviour
{
    [Tooltip("一格的世界长度，决定小人的整体尺寸")]
    public float unit = 1f;

    private static readonly Color Skin = new Color(1.00f, 0.85f, 0.72f);
    private static readonly Color Shirt = new Color(0.20f, 0.60f, 0.95f);
    private static readonly Color Pants = new Color(0.25f, 0.30f, 0.50f);
    private static readonly Color Shoes = new Color(0.90f, 0.32f, 0.30f);
    private static readonly Color Cap = new Color(0.32f, 0.21f, 0.17f);
    private static readonly Color Eye = new Color(0.10f, 0.10f, 0.14f);
    private static readonly Color Mouth = new Color(0.84f, 0.34f, 0.34f);
    private static readonly Color Blush = new Color(0.98f, 0.62f, 0.60f);

    private Transform bobRoot;      // 整体上下起伏
    private Transform tiltRoot;     // 走路时左右倾斜
    private float lastX;
    private float walkTimer;
    private float walkDirection = 1f;
    private float tiltNow;
    private bool built;

    /// <summary>搭出小人（每个实例只需调用一次），cellSize 为一格的世界长度。</summary>
    public void Build(float cellSize)
    {
        if (built) return;
        built = true;
        unit = Mathf.Max(0.2f, cellSize);

        // 隐藏人物预制体自带的方块外观；拾取靠格子下标判定，不需要碰撞体
        Renderer selfRenderer = GetComponent<Renderer>();
        if (selfRenderer != null) selfRenderer.enabled = false;
        Collider selfCollider = GetComponent<Collider>();
        if (selfCollider != null) selfCollider.enabled = false;

        GameObject bob = new GameObject("AvatarBob");
        bob.transform.SetParent(transform, false);
        bobRoot = bob.transform;

        GameObject tilt = new GameObject("AvatarTilt");
        tilt.transform.SetParent(bobRoot, false);
        tiltRoot = tilt.transform;

        // 腿和鞋
        Part(tiltRoot, "Leg_L", PrimitiveType.Cube, new Vector3(-0.075f, 0.09f, 0f), new Vector3(0.07f, 0.18f, 0.07f), Pants);
        Part(tiltRoot, "Leg_R", PrimitiveType.Cube, new Vector3(0.075f, 0.09f, 0f), new Vector3(0.07f, 0.18f, 0.07f), Pants);
        Part(tiltRoot, "Shoe_L", PrimitiveType.Cube, new Vector3(-0.075f, 0.03f, -0.03f), new Vector3(0.09f, 0.06f, 0.14f), Shoes);
        Part(tiltRoot, "Shoe_R", PrimitiveType.Cube, new Vector3(0.075f, 0.03f, -0.03f), new Vector3(0.09f, 0.06f, 0.14f), Shoes);

        // 圆滚滚的身体
        Part(tiltRoot, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.30f, 0f), new Vector3(0.30f, 0.30f, 0.26f), Shirt);

        // 手臂（略微向外张开）
        Part(tiltRoot, "Arm_L", PrimitiveType.Sphere, new Vector3(-0.17f, 0.33f, 0f), new Vector3(0.09f, 0.20f, 0.09f), Skin, 18f);
        Part(tiltRoot, "Arm_R", PrimitiveType.Sphere, new Vector3(0.17f, 0.33f, 0f), new Vector3(0.09f, 0.20f, 0.09f), Skin, -18f);

        // 头
        Part(tiltRoot, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f), new Vector3(0.30f, 0.30f, 0.30f), Skin);

        // 棒球帽：帽身 + 朝镜头方向（-z）的帽檐
        Part(tiltRoot, "Cap", PrimitiveType.Sphere, new Vector3(0f, 0.625f, 0f), new Vector3(0.32f, 0.20f, 0.32f), Cap);
        Part(tiltRoot, "CapBrim", PrimitiveType.Cube, new Vector3(0f, 0.60f, -0.16f), new Vector3(0.28f, 0.03f, 0.16f), Cap);

        // 表情：眼睛、腮红、嘴（都朝 -z，也就是镜头方向）
        Part(tiltRoot, "Eye_L", PrimitiveType.Sphere, new Vector3(-0.055f, 0.55f, -0.135f), new Vector3(0.055f, 0.06f, 0.05f), Eye);
        Part(tiltRoot, "Eye_R", PrimitiveType.Sphere, new Vector3(0.055f, 0.55f, -0.135f), new Vector3(0.055f, 0.06f, 0.05f), Eye);
        Part(tiltRoot, "Blush_L", PrimitiveType.Sphere, new Vector3(-0.10f, 0.51f, -0.11f), new Vector3(0.05f, 0.035f, 0.03f), Blush);
        Part(tiltRoot, "Blush_R", PrimitiveType.Sphere, new Vector3(0.10f, 0.51f, -0.11f), new Vector3(0.05f, 0.035f, 0.03f), Blush);
        Part(tiltRoot, "Mouth", PrimitiveType.Sphere, new Vector3(0f, 0.475f, -0.13f), new Vector3(0.09f, 0.045f, 0.03f), Mouth);

        lastX = transform.position.x;
    }

    /// <summary>造一个部件：位置与尺寸都以“格”为单位，再乘上 unit。</summary>
    private GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 position,
                            Vector3 scale, Color color, float tiltZ = 0f)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position * unit;
        go.transform.localScale = scale * unit;
        if (Mathf.Abs(tiltZ) > 0.001f)
        {
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tiltZ);
        }

        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);        // 只做外形，不参与物理

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = color;
        return go;
    }

    void Update()
    {
        if (!built || bobRoot == null) return;

        // 依据 x 方向的位置变化判断是否正在走路
        float x = transform.position.x;
        float delta = x - lastX;
        lastX = x;

        if (Mathf.Abs(delta) > 0.0001f)
        {
            walkTimer = 0.28f;
            walkDirection = delta > 0f ? 1f : -1f;
        }
        else if (walkTimer > 0f)
        {
            walkTimer -= Time.deltaTime;
        }

        bool walking = walkTimer > 0f;
        float bob = walking
            ? Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.045f
            : 0.008f + Mathf.Sin(Time.time * 2f) * 0.006f;    // 站着的时候轻轻呼吸
        bobRoot.localPosition = new Vector3(0f, bob * unit, 0f);

        // 走路时朝移动方向略微倾斜（画面上就是左右歪一点）
        float targetTilt = walking ? -walkDirection * 9f : 0f;
        tiltNow = Mathf.Lerp(tiltNow, targetTilt, Mathf.Min(1f, 10f * Time.deltaTime));
        tiltRoot.localRotation = Quaternion.Euler(0f, 0f, tiltNow);
    }
}
