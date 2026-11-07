using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tạo không khí chiếu cờ Cá Ngựa vỉa hè / sàn gạch bông Việt Nam quanh 4 góc bàn cờ:
/// - 2 góc ngồi khoanh chân trên chiếu cói (quần đùi, bàn chân trần, dép tổ ong vàng đặt cạnh, ly trà đá ống hút xanh).
/// - 2 góc ngồi ghế đẩu nhựa đỏ kiểu mấy cô ngoài chợ (quần bông/quần lửng, mang dép tổ ong vàng, đĩa hạt hướng dương).
/// - Tự động diễn hoạt "nhịp đùi" (knee-bounce) cho người đang tới lượt, giậm chân phấn khích khi đá quân và giật mình khi bị đá.
/// </summary>
public class VietnameseFloorAtmosphere : MonoBehaviour
{
    public static VietnameseFloorAtmosphere Instance { get; private set; }

    private enum SitterStyle
    {
        CrossLegged, // Ngồi khoanh chân + dép tổ ong để bên cạnh + trà đá
        MarketStool  // Ngồi chồm hỗm ghế đẩu nhựa đỏ + mang dép tổ ong + đĩa hướng dương
    }

    private class SitterRig
    {
        public int playerIndex;
        public SitterStyle style;
        public Transform root;
        public Transform leftLegPivot;
        public Transform rightLegPivot;
        public Transform propRoot;
        public Vector3 baseLeftEuler;
        public Vector3 baseRightEuler;
        public Vector3 baseRootLocalPos;
        public float reactionTimer;
        public int reactionType; // 0 = none, 1 = happy kick stomp, 2 = startled jolt
    }

    private readonly SitterRig[] sitters = new SitterRig[4];
    private Shader cachedLitShader;

    public static VietnameseFloorAtmosphere EnsureCreated()
    {
        if (Instance != null)
        {
            return Instance;
        }

        VietnameseFloorAtmosphere existing = FindFirstObjectByType<VietnameseFloorAtmosphere>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("VietnameseFloorAtmosphere");
        Instance = go.AddComponent<VietnameseFloorAtmosphere>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildAllSitters();
    }

    public void TriggerKickReaction(int attackerColorIndex, int victimColorIndex)
    {
        if (attackerColorIndex >= 0 && attackerColorIndex < sitters.Length && sitters[attackerColorIndex] != null)
        {
            sitters[attackerColorIndex].reactionType = 1;
            sitters[attackerColorIndex].reactionTimer = 1.25f;
        }

        if (victimColorIndex >= 0 && victimColorIndex < sitters.Length && sitters[victimColorIndex] != null)
        {
            sitters[victimColorIndex].reactionType = 2;
            sitters[victimColorIndex].reactionTimer = 1.25f;
        }
    }

    private void BuildAllSitters()
    {
        // Toạ độ thực tế quanh ludo_board (center=(0.11, 0.07, 0.09), min=(-0.89, 0, -0.91), max=(1.11, 0.13, 1.09))
        // Tận dụng mặt sàn gạch bông bên dưới/trên/phải hoàn toàn nằm trong khung nhìn Camera.main và không bị UI che
        CreateSitter(
            0,
            SitterStyle.CrossLegged,
            new Vector3(-0.38f, -0.02f, -1.10f),
            0f,
            new Color(0.18f, 0.45f, 0.85f), // Quần đùi xanh dương
            new Color(0.92f, 0.74f, 0.58f)  // Da chân rám nắng
        );

        CreateSitter(
            1,
            SitterStyle.MarketStool,
            new Vector3(-0.38f, -0.02f, 1.26f),
            180f,
            new Color(0.78f, 0.20f, 0.24f), // Quần bông đỏ đô ngoài chợ
            new Color(0.95f, 0.78f, 0.62f)
        );

        CreateSitter(
            2,
            SitterStyle.CrossLegged,
            new Vector3(1.32f, -0.02f, 0.56f),
            -90f,
            new Color(0.16f, 0.62f, 0.34f), // Quần đùi xanh lá
            new Color(0.91f, 0.72f, 0.56f)
        );

        CreateSitter(
            3,
            SitterStyle.MarketStool,
            new Vector3(1.32f, -0.02f, -0.44f),
            -90f,
            new Color(0.88f, 0.64f, 0.14f), // Quần bông vàng nghệ ngoài chợ
            new Color(0.94f, 0.76f, 0.60f)
        );
    }

    private void CreateSitter(
        int playerIndex,
        SitterStyle style,
        Vector3 worldPos,
        float yawDegrees,
        Color pantsColor,
        Color skinColor)
    {
        GameObject rootObj = new GameObject($"Sitter_P{playerIndex}_{style}");
        rootObj.transform.SetParent(transform, false);
        rootObj.transform.position = worldPos;
        rootObj.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

        SitterRig rig = new SitterRig
        {
            playerIndex = playerIndex,
            style = style,
            root = rootObj.transform,
            baseRootLocalPos = rootObj.transform.localPosition
        };

        if (style == SitterStyle.CrossLegged)
        {
            BuildCrossLeggedRig(rig, pantsColor, skinColor);
        }
        else
        {
            BuildMarketStoolRig(rig, pantsColor, skinColor);
        }

        sitters[playerIndex] = rig;
    }

    private void BuildCrossLeggedRig(SitterRig rig, Color shortsColor, Color skinColor)
    {
        // 1. Góc chiếu cói lót ngồi (Woven sedge mat corner with red hem)
        Transform matRoot = new GameObject("SedgeMat").transform;
        matRoot.SetParent(rig.root, false);
        CreatePart(matRoot, PrimitiveType.Cube, "MatBody",
            new Vector3(0f, -0.04f, -0.04f), Vector3.zero, new Vector3(0.56f, 0.012f, 0.38f),
            new Color(0.84f, 0.76f, 0.52f), 0.15f);
        CreatePart(matRoot, PrimitiveType.Cube, "MatRedHemFront",
            new Vector3(0f, -0.038f, 0.14f), Vector3.zero, new Vector3(0.57f, 0.014f, 0.028f),
            new Color(0.78f, 0.16f, 0.18f), 0.2f);

        // 2. Hông / Eo quần đùi ở rìa ngoài bàn cờ
        CreatePart(rig.root, PrimitiveType.Capsule, "PelvisShorts",
            new Vector3(0f, 0.048f, -0.15f), new Vector3(0f, 0f, 90f), new Vector3(0.115f, 0.145f, 0.105f),
            shortsColor, 0.2f);

        // 3. Chân trái: Đùi mở rộng sang trái (-40 độ hình chữ V), bắp chân gập chéo vào lòng
        Transform leftPivot = new GameObject("LeftLegPivot").transform;
        leftPivot.SetParent(rig.root, false);
        leftPivot.localPosition = new Vector3(-0.085f, 0.048f, -0.13f);
        leftPivot.localRotation = Quaternion.Euler(8f, -42f, 0f);

        // Ống quần đùi trái mở ra ngoài
        CreatePart(leftPivot, PrimitiveType.Capsule, "LeftShortsLeg",
            new Vector3(0f, 0f, 0.075f), new Vector3(86f, 0f, 0f), new Vector3(0.102f, 0.085f, 0.092f),
            shortsColor, 0.2f);
        CreatePart(leftPivot, PrimitiveType.Cylinder, "LeftShortsStripe",
            new Vector3(0f, 0f, 0.135f), new Vector3(86f, 0f, 0f), new Vector3(0.105f, 0.010f, 0.095f),
            new Color(0.95f, 0.95f, 0.95f), 0.2f);
        // Đầu gối trái nằm ở góc ngoài bên trái
        CreatePart(leftPivot, PrimitiveType.Sphere, "LeftKnee",
            new Vector3(0f, -0.004f, 0.175f), Vector3.zero, new Vector3(0.086f, 0.078f, 0.086f),
            skinColor, 0.25f);
        // Bắp chân trái gập từ đầu gối trái bẻ ngang vào giữa lòng (góc +112 độ so với đùi)
        CreatePart(leftPivot, PrimitiveType.Capsule, "LeftCalf",
            new Vector3(0.095f, -0.018f, 0.205f), new Vector3(88f, 112f, 0f), new Vector3(0.072f, 0.105f, 0.068f),
            skinColor, 0.25f);
        // Bàn chân trần trái đặt dưới đùi phải
        CreateBareFoot(leftPivot, new Vector3(0.195f, -0.026f, 0.165f), new Vector3(0f, 115f, 8f), skinColor);

        // 4. Chân phải: Đùi mở rộng sang phải (+42 độ hình chữ V), bắp chân gập chéo vào lòng
        Transform rightPivot = new GameObject("RightLegPivot").transform;
        rightPivot.SetParent(rig.root, false);
        rightPivot.localPosition = new Vector3(0.085f, 0.048f, -0.13f);
        rightPivot.localRotation = Quaternion.Euler(10f, 42f, 0f);

        CreatePart(rightPivot, PrimitiveType.Capsule, "RightShortsLeg",
            new Vector3(0f, 0f, 0.075f), new Vector3(86f, 0f, 0f), new Vector3(0.102f, 0.085f, 0.092f),
            shortsColor, 0.2f);
        CreatePart(rightPivot, PrimitiveType.Cylinder, "RightShortsStripe",
            new Vector3(0f, 0f, 0.135f), new Vector3(86f, 0f, 0f), new Vector3(0.105f, 0.010f, 0.095f),
            new Color(0.95f, 0.95f, 0.95f), 0.2f);
        // Đầu gối phải nằm ở góc ngoài bên phải
        CreatePart(rightPivot, PrimitiveType.Sphere, "RightKnee",
            new Vector3(0f, -0.004f, 0.175f), Vector3.zero, new Vector3(0.086f, 0.078f, 0.086f),
            skinColor, 0.25f);
        // Bắp chân phải gập từ đầu gối phải bẻ ngang vào giữa lòng (góc -112 độ so với đùi)
        CreatePart(rightPivot, PrimitiveType.Capsule, "RightCalf",
            new Vector3(-0.095f, -0.010f, 0.215f), new Vector3(88f, -112f, 0f), new Vector3(0.072f, 0.105f, 0.068f),
            skinColor, 0.25f);
        // Bàn chân trần phải đặt dưới đùi trái
        CreateBareFoot(rightPivot, new Vector3(-0.195f, -0.018f, 0.175f), new Vector3(0f, -115f, -8f), skinColor);

        rig.leftLegPivot = leftPivot;
        rig.rightLegPivot = rightPivot;
        rig.baseLeftEuler = leftPivot.localEulerAngles;
        rig.baseRightEuler = rightPivot.localEulerAngles;

        // 5. Đôi dép tổ ong vàng huyền thoại tháo để bên cạnh chiếu + Ly trà đá vỉa hè
        Transform props = new GameObject("StreetProps").transform;
        props.SetParent(rig.root, false);
        rig.propRoot = props;

        CreateHoneycombSlipper(props, new Vector3(0.34f, -0.03f, 0.04f), 22f);
        CreateHoneycombSlipper(props, new Vector3(0.44f, -0.03f, 0.01f), -14f);
        CreateIcedTeaCup(props, new Vector3(-0.34f, -0.03f, 0.06f));
    }

    private void BuildMarketStoolRig(SitterRig rig, Color floralPantsColor, Color skinColor)
    {
        // 1. Ghế đẩu nhựa đỏ ngoài chợ (Red plastic low stool with 4 legs + center hole)
        Transform stool = new GameObject("RedPlasticStool").transform;
        stool.SetParent(rig.root, false);
        Color redPlastic = new Color(0.86f, 0.14f, 0.14f);
        CreatePart(stool, PrimitiveType.Cylinder, "StoolTop",
            new Vector3(0f, 0.035f, -0.11f), Vector3.zero, new Vector3(0.24f, 0.018f, 0.24f),
            redPlastic, 0.55f);
        CreatePart(stool, PrimitiveType.Cylinder, "StoolHole",
            new Vector3(0f, 0.054f, -0.11f), Vector3.zero, new Vector3(0.045f, 0.004f, 0.045f),
            new Color(0.18f, 0.05f, 0.05f), 0.1f);

        float[] legAngles = { 45f, 135f, 225f, 315f };
        for (int i = 0; i < legAngles.Length; i++)
        {
            float rad = legAngles[i] * Mathf.Deg2Rad;
            Vector3 legPos = new Vector3(Mathf.Cos(rad) * 0.095f, -0.01f, -0.11f + Mathf.Sin(rad) * 0.095f);
            CreatePart(stool, PrimitiveType.Cube, $"StoolLeg_{i}",
                legPos, new Vector3(Mathf.Sin(rad) * -8f, 0f, Mathf.Cos(rad) * 8f), new Vector3(0.026f, 0.09f, 0.026f),
                redPlastic, 0.5f);
        }

        // 2. Dáng ngồi chồm hỗm / ngồi ghế đẩu thấp kiểu các cô ngoài chợ (1 đầu gối dựng cao, 1 chân nhịp đùi)
        Color patternDotColor = new Color(0.98f, 0.92f, 0.76f);

        // Chân trái (đầu gối chống cao nhìn từ top-down cực rõ)
        Transform leftPivot = new GameObject("LeftKneeUpPivot").transform;
        leftPivot.SetParent(rig.root, false);
        leftPivot.localPosition = new Vector3(-0.095f, 0.06f, -0.08f);
        leftPivot.localRotation = Quaternion.Euler(-8f, 12f, 0f);

        CreatePart(leftPivot, PrimitiveType.Capsule, "LeftThighPants",
            new Vector3(0f, 0.05f, 0.07f), new Vector3(48f, 0f, 0f), new Vector3(0.105f, 0.095f, 0.105f),
            floralPantsColor, 0.2f);
        // Chấm bông trên quần ngoài chợ
        CreatePart(leftPivot, PrimitiveType.Sphere, "LeftFloral1",
            new Vector3(-0.02f, 0.10f, 0.06f), Vector3.zero, new Vector3(0.028f, 0.012f, 0.028f),
            patternDotColor, 0.2f);
        CreatePart(leftPivot, PrimitiveType.Sphere, "LeftFloral2",
            new Vector3(0.025f, 0.08f, 0.10f), Vector3.zero, new Vector3(0.026f, 0.012f, 0.026f),
            patternDotColor, 0.2f);
        // Đầu gối dựng cao
        CreatePart(leftPivot, PrimitiveType.Sphere, "LeftHighKneeCap",
            new Vector3(0f, 0.12f, 0.13f), Vector3.zero, new Vector3(0.096f, 0.096f, 0.096f),
            floralPantsColor, 0.25f);
        // Ống chân trái cắm xuống dép tổ ong
        CreatePart(leftPivot, PrimitiveType.Capsule, "LeftShin",
            new Vector3(0f, 0.03f, 0.16f), new Vector3(12f, 0f, 0f), new Vector3(0.076f, 0.085f, 0.076f),
            skinColor, 0.25f);
        // Bàn chân trái xỏ trong dép tổ ong vàng
        CreateBareFoot(leftPivot, new Vector3(0f, -0.07f, 0.19f), Vector3.zero, skinColor);
        CreateHoneycombSlipper(leftPivot, new Vector3(0f, -0.085f, 0.19f), 4f);

        // Chân phải (hơi khuỳnh sang ngang để nhịp đùi thoải mái)
        Transform rightPivot = new GameObject("RightKneePivot").transform;
        rightPivot.SetParent(rig.root, false);
        rightPivot.localPosition = new Vector3(0.095f, 0.06f, -0.08f);
        rightPivot.localRotation = Quaternion.Euler(4f, -18f, 0f);

        CreatePart(rightPivot, PrimitiveType.Capsule, "RightThighPants",
            new Vector3(0f, 0.03f, 0.08f), new Vector3(64f, 0f, 0f), new Vector3(0.105f, 0.095f, 0.105f),
            floralPantsColor, 0.2f);
        CreatePart(rightPivot, PrimitiveType.Sphere, "RightFloral1",
            new Vector3(0.018f, 0.075f, 0.07f), Vector3.zero, new Vector3(0.028f, 0.012f, 0.028f),
            patternDotColor, 0.2f);
        CreatePart(rightPivot, PrimitiveType.Sphere, "RightKneeCap",
            new Vector3(0f, 0.08f, 0.15f), Vector3.zero, new Vector3(0.094f, 0.094f, 0.094f),
            floralPantsColor, 0.25f);
        CreatePart(rightPivot, PrimitiveType.Capsule, "RightShin",
            new Vector3(0f, 0.01f, 0.17f), new Vector3(18f, 0f, 0f), new Vector3(0.075f, 0.08f, 0.075f),
            skinColor, 0.25f);
        CreateBareFoot(rightPivot, new Vector3(0f, -0.07f, 0.20f), Vector3.zero, skinColor);
        CreateHoneycombSlipper(rightPivot, new Vector3(0f, -0.085f, 0.20f), -8f);

        rig.leftLegPivot = leftPivot;
        rig.rightLegPivot = rightPivot;
        rig.baseLeftEuler = leftPivot.localEulerAngles;
        rig.baseRightEuler = rightPivot.localEulerAngles;

        // 3. Đĩa hạt hướng dương + Ly trà đá bên cạnh ghế đẩu
        Transform props = new GameObject("MarketProps").transform;
        props.SetParent(rig.root, false);
        rig.propRoot = props;

        CreateSunflowerSeedPlate(props, new Vector3(0.32f, -0.03f, 0.05f));
        CreateIcedTeaCup(props, new Vector3(-0.31f, -0.03f, 0.04f));
    }

    private void CreateBareFoot(Transform parent, Vector3 localPos, Vector3 localEuler, Color skinColor)
    {
        Transform foot = new GameObject("BareFoot").transform;
        foot.SetParent(parent, false);
        foot.localPosition = localPos;
        foot.localRotation = Quaternion.Euler(localEuler);

        CreatePart(foot, PrimitiveType.Capsule, "FootSole",
            Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.058f, 0.066f, 0.032f),
            skinColor, 0.25f);

        // 5 ngón chân nhỏ xinh nhìn từ Top-Down
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            float x = Mathf.Lerp(-0.024f, 0.024f, t);
            float size = Mathf.Lerp(0.016f, 0.011f, t);
            CreatePart(foot, PrimitiveType.Sphere, $"Toe_{i}",
                new Vector3(x, 0.004f, 0.074f - i * 0.003f), Vector3.zero, new Vector3(size, size * 0.8f, size * 1.2f),
                skinColor, 0.25f);
        }
    }

    private void CreateHoneycombSlipper(Transform parent, Vector3 localPos, float yaw)
    {
        Transform slipper = new GameObject("DepToOng").transform;
        slipper.SetParent(parent, false);
        slipper.localPosition = localPos;
        slipper.localRotation = Quaternion.Euler(0f, yaw, 0f);

        Color classicYellow = new Color(0.95f, 0.80f, 0.18f);
        Color holeShadow = new Color(0.42f, 0.32f, 0.04f);

        // Đế dép tổ ong bầu dục
        CreatePart(slipper, PrimitiveType.Cylinder, "SoleFront",
            new Vector3(0f, 0.008f, 0.032f), Vector3.zero, new Vector3(0.082f, 0.008f, 0.095f),
            classicYellow, 0.35f);
        CreatePart(slipper, PrimitiveType.Cylinder, "SoleHeel",
            new Vector3(0f, 0.008f, -0.036f), Vector3.zero, new Vector3(0.072f, 0.008f, 0.085f),
            classicYellow, 0.35f);

        // Quai dép tổ ong dày có lỗ tổ ong đặc trưng
        CreatePart(slipper, PrimitiveType.Cube, "Strap",
            new Vector3(0f, 0.024f, 0.018f), Vector3.zero, new Vector3(0.084f, 0.014f, 0.064f),
            classicYellow, 0.35f);

        // Các chấm lỗ tổ ong trên quai dép (nhìn từ camera Top-Down nhận ra ngay)
        for (int r = -1; r <= 1; r++)
        {
            for (int c = -2; c <= 2; c++)
            {
                CreatePart(slipper, PrimitiveType.Cylinder, $"Hole_{r}_{c}",
                    new Vector3(c * 0.014f, 0.032f, 0.018f + r * 0.016f),
                    Vector3.zero,
                    new Vector3(0.008f, 0.002f, 0.008f),
                    holeShadow, 0.1f);
            }
        }
    }

    private void CreateIcedTeaCup(Transform parent, Vector3 localPos)
    {
        Transform cup = new GameObject("LyTraDa").transform;
        cup.SetParent(parent, false);
        cup.localPosition = localPos;

        // Đế lót ly nhựa đỏ
        CreatePart(cup, PrimitiveType.Cylinder, "Coaster",
            new Vector3(0f, 0.004f, 0f), Vector3.zero, new Vector3(0.085f, 0.004f, 0.085f),
            new Color(0.82f, 0.15f, 0.18f), 0.4f);

        // Thân ly trà đá màu hổ phách
        CreatePart(cup, PrimitiveType.Cylinder, "TeaGlass",
            new Vector3(0f, 0.045f, 0f), Vector3.zero, new Vector3(0.068f, 0.042f, 0.068f),
            new Color(0.76f, 0.48f, 0.12f), 0.75f);

        // Viên đá nổi trên mặt ly
        CreatePart(cup, PrimitiveType.Cube, "Ice1",
            new Vector3(-0.012f, 0.088f, 0.008f), new Vector3(14f, 25f, 8f), new Vector3(0.022f, 0.016f, 0.022f),
            new Color(0.92f, 0.97f, 1f), 0.85f);
        CreatePart(cup, PrimitiveType.Cube, "Ice2",
            new Vector3(0.014f, 0.087f, -0.010f), new Vector3(-10f, 50f, 12f), new Vector3(0.020f, 0.015f, 0.020f),
            new Color(0.92f, 0.97f, 1f), 0.85f);

        // Ống hút nhựa xanh lá
        CreatePart(cup, PrimitiveType.Cylinder, "GreenStraw",
            new Vector3(0.015f, 0.085f, 0.012f), new Vector3(16f, 20f, -18f), new Vector3(0.010f, 0.055f, 0.010f),
            new Color(0.18f, 0.85f, 0.36f), 0.6f);
    }

    private void CreateSunflowerSeedPlate(Transform parent, Vector3 localPos)
    {
        Transform plate = new GameObject("DiaHuongDuong").transform;
        plate.SetParent(parent, false);
        plate.localPosition = localPos;

        // Đĩa nhựa xanh ngọc
        CreatePart(plate, PrimitiveType.Cylinder, "Plate",
            new Vector3(0f, 0.006f, 0f), Vector3.zero, new Vector3(0.125f, 0.006f, 0.125f),
            new Color(0.28f, 0.76f, 0.68f), 0.5f);

        // Hạt hướng dương đen sọc trắng trên đĩa
        for (int i = 0; i < 9; i++)
        {
            float angle = i * 40f * Mathf.Deg2Rad;
            float dist = (i % 2 == 0) ? 0.018f : 0.036f;
            CreatePart(plate, PrimitiveType.Capsule, $"Seed_{i}",
                new Vector3(Mathf.Cos(angle) * dist, 0.014f, Mathf.Sin(angle) * dist),
                new Vector3(90f, i * 37f, 0f),
                new Vector3(0.012f, 0.016f, 0.008f),
                new Color(0.18f, 0.16f, 0.15f), 0.3f);
        }
    }

    private GameObject CreatePart(
        Transform parent,
        PrimitiveType type,
        string partName,
        Vector3 localPos,
        Vector3 localEuler,
        Vector3 localScale,
        Color color,
        float smoothness)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = partName;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = Quaternion.Euler(localEuler);
        obj.transform.localScale = localScale;

        // Xoá Collider để không bao giờ cản Raycast click chọn quân cờ
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }

        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            if (cachedLitShader == null)
            {
                cachedLitShader = Shader.Find("Universal Render Pipeline/Lit");
                if (cachedLitShader == null)
                {
                    cachedLitShader = Shader.Find("Standard");
                }
            }

            Material mat = new Material(cachedLitShader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }
            rend.material = mat;
            rend.shadowCastingMode = ShadowCastingMode.On;
            rend.receiveShadows = true;
        }

        return obj;
    }

    private void Update()
    {
        int activeTurnColor = GetActiveTurnColorIndex();
        float time = Time.time;

        for (int i = 0; i < sitters.Length; i++)
        {
            SitterRig rig = sitters[i];
            if (rig == null || rig.root == null)
            {
                continue;
            }

            if (rig.reactionTimer > 0f)
            {
                rig.reactionTimer -= Time.deltaTime;
                if (rig.reactionType == 1)
                {
                    // Phấn khích giậm chân / vỗ đùi khi đá trúng đối thủ
                    float stomp = Mathf.Sin(time * 28f) * 14f;
                    rig.leftLegPivot.localRotation = Quaternion.Euler(
                        rig.baseLeftEuler.x + stomp,
                        rig.baseLeftEuler.y,
                        rig.baseLeftEuler.z);
                    rig.rightLegPivot.localRotation = Quaternion.Euler(
                        rig.baseRightEuler.x - stomp,
                        rig.baseRightEuler.y,
                        rig.baseRightEuler.z);
                    rig.root.localPosition = rig.baseRootLocalPos + new Vector3(0f, Mathf.Abs(Mathf.Sin(time * 22f)) * 0.025f, 0f);
                }
                else if (rig.reactionType == 2)
                {
                    // Giật mình rụt chân khi quân mình bị đá bay về chuồng
                    float jolt = Mathf.Sin(time * 34f) * 10f;
                    rig.leftLegPivot.localRotation = Quaternion.Euler(
                        rig.baseLeftEuler.x - 10f + jolt,
                        rig.baseLeftEuler.y,
                        rig.baseLeftEuler.z);
                    rig.rightLegPivot.localRotation = Quaternion.Euler(
                        rig.baseRightEuler.x - 10f - jolt,
                        rig.baseRightEuler.y,
                        rig.baseRightEuler.z);
                    rig.root.localPosition = rig.baseRootLocalPos + rig.root.forward * -0.035f;
                }

                if (rig.reactionTimer <= 0f)
                {
                    rig.reactionType = 0;
                    rig.root.localPosition = rig.baseRootLocalPos;
                }

                continue;
            }

            if (i == activeTurnColor)
            {
                // Tới lượt: Nhịp đùi đều đặn đậm chất chiếu cờ Việt Nam
                float bounceRight = Mathf.Sin(time * 7.5f) * 6.5f;
                float bounceLeft = Mathf.Sin(time * 3.75f) * 2.5f;

                rig.rightLegPivot.localRotation = Quaternion.Euler(
                    rig.baseRightEuler.x + bounceRight,
                    rig.baseRightEuler.y + bounceRight * 0.35f,
                    rig.baseRightEuler.z);

                rig.leftLegPivot.localRotation = Quaternion.Euler(
                    rig.baseLeftEuler.x + bounceLeft,
                    rig.baseLeftEuler.y,
                    rig.baseLeftEuler.z);
            }
            else
            {
                // Ngoài lượt: Thở nhẹ tự nhiên
                float idle = Mathf.Sin(time * 1.8f + i * 1.3f) * 1.2f;
                rig.rightLegPivot.localRotation = Quaternion.Euler(
                    rig.baseRightEuler.x + idle,
                    rig.baseRightEuler.y,
                    rig.baseRightEuler.z);
                rig.leftLegPivot.localRotation = Quaternion.Euler(
                    rig.baseLeftEuler.x - idle * 0.5f,
                    rig.baseLeftEuler.y,
                    rig.baseLeftEuler.z);
            }
        }
    }

    private int GetActiveTurnColorIndex()
    {
        if (NetworkTurnManager.Instance != null && NetworkTurnManager.Instance.IsSpawned)
        {
            return NetworkTurnManager.Instance.currentPlayerIndex.Value;
        }

        if (TurnManager.Instance != null)
        {
            return TurnManager.Instance.currentPlayerIndex;
        }

        return 0;
    }
}
