
/*
AWR Battle Arena - Gameplay Complete Code Only v3

هذه نسخة كاملة بالكود فقط.
أنت فقط تضيف الشخصية والأنيميشنات من عندك.

التشغيل:
1) افتح Unity 2022.3 LTS أو أحدث.
2) افتح Scene فارغ.
3) أنشئ GameObject باسم AWR_Game.
4) أضف هذا السكربت: AWR_BattleArena_GameplayComplete_v3
5) اضغط Play.

إضافة الشخصية:
- ضع FBX في:
  Assets/YourAssets/Character_Put_FBX_Here/
- اسحب Prefab الشخصية إلى External Character Prefab.
- اسحب Animator Controller إلى External Animator Controller.

Animator Parameters المطلوبة:
Float:
- Speed
- DirectionX
- DirectionY

Bool:
- Sprint
- Crouch
- Prone
- Aim
- Dead

Trigger:
- Fire
- Reload
- Throw
- Hit
- Death
- SwitchWeapon
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AWR_BattleArena_GameplayComplete_v3 : MonoBehaviour
{
    public enum GameState { Lobby, Playing, RoundEnd, MatchEnd }
    public enum MapType { Warehouse, City, Harbor }
    public enum QualityPreset { Low, Medium, Ultra }
    public enum MatchMode { Respawn, Rounds, MiniBR }
    public enum TeamMode { OneVsOne, TwoVsTwo, ThreeVsThree, ThreeVsThreeVsThree, FreeForAll }
    public enum WeaponSlot { Rifle, Pistol, Shotgun, Sniper }

    [Header("External Character")]
    public GameObject externalCharacterPrefab;
    public RuntimeAnimatorController externalAnimatorController;

    [Header("Match Defaults")]
    public MapType selectedMap = MapType.Warehouse;
    public QualityPreset quality = QualityPreset.Ultra;
    public MatchMode matchMode = MatchMode.Respawn;
    public TeamMode teamMode = TeamMode.ThreeVsThree;
    public bool thirdPerson = true;
    public bool mobileMode = true;
    public bool bloodEnabled = true;
    public int botCount = 8;
    public int respawnSeconds = 5;
    public int roundLimit = 7;
    public int matchMinutes = 10;

    [Header("Controls")]
    public float lookSensitivity = 2.2f;
    public float moveSpeed = 5.5f;
    public float sprintSpeed = 9f;
    public float jumpForce = 7f;

    GameState state = GameState.Lobby;

    Transform playerRoot;
    Transform cameraPivot;
    Camera cam;
    CharacterController controller;
    Animator animator;
    AWRProceduralSoldier fallbackSoldier;
    AWRRuntimeAnim fallbackAnim;

    float yaw;
    float pitch = 12f;
    float verticalVelocity;
    float fireCooldown;
    bool sprint, crouch, prone, aim, dead, reloading;
    float health = 100;
    float armor = 50;
    int resources = 0;
    int grenades = 2;

    WeaponSlot currentWeapon = WeaponSlot.Rifle;
    readonly Dictionary<WeaponSlot, WeaponData> weapons = new Dictionary<WeaponSlot, WeaponData>();

    readonly List<AWRSpawnPoint> spawns = new List<AWRSpawnPoint>();
    readonly List<BotAgent> bots = new List<BotAgent>();
    readonly List<Transform> spectatorTargets = new List<Transform>();
    readonly List<string> killFeed = new List<string>();
    readonly Dictionary<int, int> teamScores = new Dictionary<int, int>();

    int localTeam = 0;
    int roundNumber = 1;
    float matchTimer;
    float zoneRadius = 140f;
    Vector3 zoneCenter = Vector3.zero;
    bool spectatorMode;
    int spectatorIndex;
    float killCamTimer;
    bool killCamActive;
    Vector3 lastDeathCameraPos;
    Quaternion lastDeathCameraRot;

    Material matConcrete, matMetal, matDark, matBlue, matRed, matGreen, matGlass, matWater, matBlood, matLoot, matZone;

    Canvas canvas;
    Text hud, centerText, killFeedText, lobbyInfo;
    GameObject lobbyPanel, settingsPanel, weaponPanel;
    readonly Dictionary<string, AWRMobileButton> mobileButtons = new Dictionary<string, AWRMobileButton>();
    bool editControlsMode, settingsOpen, weaponMenuOpen;

    bool inputFire, inputAim, inputJump, inputSprint, inputCrouch, inputProne, inputReload, inputGrenade, inputInteract;
    Vector2 mobileMove, mobileLook;

    void Start()
    {
        Application.targetFrameRate = 60;
        SetupWeapons();
        BuildMaterials();
        ApplyQuality();
        BuildUI();
        ShowLobby();
    }

    void Update()
    {
        if (state == GameState.Lobby)
        {
            UpdateLobbyKeyboard();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F1))
            ToggleSettings();

        if (Input.GetKeyDown(KeyCode.Tab))
            ToggleSpectator();

        UpdateMatchTimer();

        if (spectatorMode || killCamActive)
        {
            UpdateSpectator();
            UpdateHUD();
            ResetFrameButtons();
            return;
        }

        if (dead)
        {
            UpdateHUD();
            ResetFrameButtons();
            return;
        }

        CollectInputs();
        HandleLook();
        HandleMovement();
        HandleWeapons();
        HandleInteraction();
        UpdateMiniBRZone();
        UpdateAnimation();
        UpdateHUD();

        ResetFrameButtons();
    }

    void UpdateLobbyKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) { selectedMap = MapType.Warehouse; RefreshLobbyInfo(); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { selectedMap = MapType.City; RefreshLobbyInfo(); }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { selectedMap = MapType.Harbor; RefreshLobbyInfo(); }

        if (Input.GetKeyDown(KeyCode.F2)) { matchMode = MatchMode.Respawn; RefreshLobbyInfo(); }
        if (Input.GetKeyDown(KeyCode.F3)) { matchMode = MatchMode.Rounds; RefreshLobbyInfo(); }
        if (Input.GetKeyDown(KeyCode.F4)) { matchMode = MatchMode.MiniBR; RefreshLobbyInfo(); }

        if (Input.GetKeyDown(KeyCode.F5)) { teamMode = TeamMode.OneVsOne; RefreshLobbyInfo(); }
        if (Input.GetKeyDown(KeyCode.F6)) { teamMode = TeamMode.TwoVsTwo; RefreshLobbyInfo(); }
        if (Input.GetKeyDown(KeyCode.F7)) { teamMode = TeamMode.ThreeVsThree; RefreshLobbyInfo(); }
        if (Input.GetKeyDown(KeyCode.F8)) { teamMode = TeamMode.ThreeVsThreeVsThree; RefreshLobbyInfo(); }
        if (Input.GetKeyDown(KeyCode.F9)) { teamMode = TeamMode.FreeForAll; RefreshLobbyInfo(); }

        if (Input.GetKeyDown(KeyCode.Return))
            StartMatch();
    }

    void ShowLobby()
    {
        state = GameState.Lobby;
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        if (centerText != null) centerText.text = "";
        RefreshLobbyInfo();
    }

    void StartMatch()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(false);

        ClearOldWorld();
        ApplyQuality();
        BuildWorld();
        BuildPlayer();
        BuildBots();
        ResetMatchState();

        state = GameState.Playing;
        StartCoroutine(CenterMessage("MATCH START", 1.5f));
    }

    void ResetMatchState()
    {
        health = 100;
        armor = 50;
        resources = 0;
        grenades = 2;
        dead = false;
        reloading = false;
        roundNumber = 1;
        matchTimer = matchMinutes * 60f;
        zoneRadius = 140f;
        teamScores.Clear();
        for (int i = 0; i < 4; i++) teamScores[i] = 0;

        foreach (var key in new List<WeaponSlot>(weapons.Keys))
            weapons[key].ResetAmmo();
    }

    void ClearOldWorld()
    {
        foreach (var b in bots)
            if (b != null) Destroy(b.gameObject);
        bots.Clear();
        spawns.Clear();
        spectatorTargets.Clear();

        foreach (var obj in GameObject.FindGameObjectsWithTag("AWRWorld"))
            Destroy(obj);

        if (playerRoot != null)
            Destroy(playerRoot.gameObject);
    }

    void BuildMaterials()
    {
        matConcrete = MakeMat("PBR Concrete", new Color(.42f,.42f,.39f), .05f, .25f);
        matMetal = MakeMat("PBR Metal", new Color(.22f,.24f,.27f), .65f, .55f);
        matDark = MakeMat("Dark", new Color(.035f,.04f,.05f), .25f, .35f);
        matBlue = MakeMat("Blue", new Color(.08f,.2f,.9f), .3f, .45f);
        matRed = MakeMat("Red", new Color(.9f,.08f,.05f), .3f, .45f);
        matGreen = MakeMat("Green", new Color(.08f,.7f,.22f), .3f, .45f);
        matGlass = MakeMat("Glass", new Color(.08f,.16f,.22f), .1f, .85f);
        matWater = MakeMat("Water", new Color(.02f,.12f,.18f), .05f, .7f);
        matBlood = MakeMat("Soft Blood Effect", new Color(.45f,.02f,.02f), .05f, .35f);
        matLoot = MakeMat("Loot Resource", new Color(1f,.75f,.08f), .1f, .5f);
        matZone = MakeMat("Zone", new Color(.1f,.45f,1f,.25f), .1f, .2f);
    }

    Material MakeMat(string name, Color color, float metallic, float smoothness)
    {
        var m = new Material(Shader.Find("Standard"));
        m.name = name;
        m.color = color;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smoothness);
        return m;
    }

    Material Emissive(string name, Color color, float power)
    {
        var m = MakeMat(name, color, .1f, .65f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * power);
        return m;
    }

    void ApplyQuality()
    {
        QualitySettings.antiAliasing = quality == QualityPreset.Ultra ? 4 : quality == QualityPreset.Medium ? 2 : 0;
        QualitySettings.shadowDistance = quality == QualityPreset.Ultra ? 170 : quality == QualityPreset.Medium ? 80 : 35;
        RenderSettings.fog = quality != QualityPreset.Low;
        RenderSettings.fogColor = new Color(.1f,.12f,.15f);
        RenderSettings.fogDensity = quality == QualityPreset.Ultra ? .003f : quality == QualityPreset.Medium ? .0018f : 0;
        RenderSettings.ambientLight = new Color(.18f,.2f,.23f);
        if (cam) cam.farClipPlane = quality == QualityPreset.Ultra ? 1300 : quality == QualityPreset.Medium ? 750 : 420;
    }

    void BuildWorld()
    {
        var holder = new GameObject("AWRWorld_Holder");
        holder.tag = "AWRWorld";

        var sun = new GameObject("AWR Cinematic Sun").AddComponent<Light>();
        sun.tag = "AWRWorld";
        sun.type = LightType.Directional;
        sun.intensity = quality == QualityPreset.Ultra ? 1.55f : 1.1f;
        sun.shadows = quality == QualityPreset.Low ? LightShadows.None : LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48, -35, 0);

        if (selectedMap == MapType.Warehouse) BuildWarehouse();
        if (selectedMap == MapType.City) BuildCity();
        if (selectedMap == MapType.Harbor) BuildHarbor();

        if (matchMode == MatchMode.MiniBR)
            BuildZoneVisual();
    }

    void BuildWarehouse()
    {
        Ground("Warehouse Ground", 26, 22, matConcrete);
        TeamBase(0, "Alpha", new Vector3(-95,0,0), matBlue);
        TeamBase(1, "Bravo", new Vector3(95,0,0), matRed);
        TeamBase(2, "Charlie", new Vector3(0,0,80), matGreen);

        Warehouse("Main Warehouse", new Vector3(0,0,0), 50, 30, 16);
        Warehouse("Side Storage A", new Vector3(-55,0,42), 36, 22, 12);
        Warehouse("Side Storage B", new Vector3(55,0,-42), 36, 22, 12);

        for(int i=0;i<95;i++)
        {
            float x = Mathf.Sin(i*1.47f)*92;
            float z = Mathf.Cos(i*1.21f)*70;
            var c = Cube("Metal Tactical Container", new Vector3(x,1.3f,z), new Vector3(9,2.6f,3.8f), i%3==0?matBlue:i%3==1?matRed:matMetal);
            c.transform.rotation = Quaternion.Euler(0,(i*29)%180,0);
        }

        AddLoot(18, 80, 60);
        AddAtmosphereLights(10);
        Bounds(130,105);
    }

    void BuildCity()
    {
        Ground("City Ground", 38, 32, matConcrete);
        TeamBase(0, "Alpha", new Vector3(-145,0,-100), matBlue);
        TeamBase(1, "Bravo", new Vector3(145,0,100), matRed);
        TeamBase(2, "Charlie", new Vector3(0,0,140), matGreen);

        Cube("Main Road X", new Vector3(0,.05f,0), new Vector3(320,.1f,14), matDark);
        Cube("Main Road Z", new Vector3(0,.06f,0), new Vector3(14,.1f,280), matDark);

        for(int x=-6;x<=6;x++)
        for(int z=-5;z<=5;z++)
        {
            if(Mathf.Abs(x)<2 && Mathf.Abs(z)<1) continue;
            Building(new Vector3(x*25,0,z*26),14,12+Mathf.Abs((x*7+z*5)%7)*3.4f,16);
        }

        for(int i=0;i<30;i++)
            Cube("Street Cover", new Vector3(-150+i*10,.75f,Mathf.Sin(i*1.7f)*80), new Vector3(4,1.5f,2), i%2==0?matMetal:matDark);

        AddLoot(24, 130, 105);
        AddAtmosphereLights(14);
        Bounds(185,160);
    }

    void BuildHarbor()
    {
        Ground("Harbor Concrete", 42, 34, matConcrete);
        Cube("Water Zone", new Vector3(0,-.08f,155), new Vector3(380,.08f,70), matWater);
        TeamBase(0, "Alpha", new Vector3(-155,0,-95), matBlue);
        TeamBase(1, "Bravo", new Vector3(155,0,-95), matRed);
        TeamBase(2, "Charlie", new Vector3(0,0,125), matGreen);

        Cube("Cargo Ship Hull", new Vector3(0,2.2f,125), new Vector3(100,4.4f,20), matMetal);
        Cube("Ship Cabin", new Vector3(30,8,125), new Vector3(24,12,12), matConcrete);

        for(int i=0;i<7;i++)
        {
            float x=-155+i*52;
            Cube("Crane Tower",new Vector3(x,12,55),new Vector3(4,24,4),matMetal);
            Cube("Crane Arm",new Vector3(x+20,24,55),new Vector3(44,3,4),matMetal);
        }

        for(int i=0;i<105;i++)
        {
            float x=-165+(i%14)*25;
            float z=-55+(i/14)*16;
            var c=Cube("Harbor Container",new Vector3(x,1.3f,z),new Vector3(10,2.6f,4),i%3==0?matBlue:i%3==1?matRed:matGreen);
            c.transform.rotation=Quaternion.Euler(0,i%2==0?0:90,0);
        }

        AddLoot(26, 150, 130);
        AddAtmosphereLights(12);
        Bounds(210,175);
    }

    void TeamBase(int team, string name, Vector3 pos, Material mat)
    {
        Cube(name+" Base Floor", pos+new Vector3(0,.06f,0), new Vector3(32,.12f,28), mat);
        Cube(name+" Shield L", pos+new Vector3(-12,1.5f,0), new Vector3(4,3,20), matDark);
        Cube(name+" Shield R", pos+new Vector3(12,1.5f,0), new Vector3(4,3,20), matDark);

        for(int i=0;i<5;i++)
        {
            var sp = new GameObject(name+" Spawn "+i);
            sp.tag = "AWRWorld";
            sp.transform.position = pos + new Vector3(-10+i*5,.2f,-7+Mathf.Sin(i)*4);
            spawns.Add(new AWRSpawnPoint { team = team, position = sp.transform.position });
        }
    }

    void Warehouse(string name, Vector3 p, float w, float d, float h)
    {
        Cube(name+" Back", p+new Vector3(0,h/2,d/2), new Vector3(w,h,1), matMetal);
        Cube(name+" Left", p+new Vector3(-w/2,h/2,0), new Vector3(1,h,d), matMetal);
        Cube(name+" Right", p+new Vector3(w/2,h/2,0), new Vector3(1,h,d), matMetal);
        Cube(name+" Roof", p+new Vector3(0,h,0), new Vector3(w,1,d), matDark);
        Cube(name+" Catwalk", p+new Vector3(0,h*.45f,0), new Vector3(w*.8f,.8f,2), matConcrete);
    }

    void Building(Vector3 p, float w, float h, float d)
    {
        Cube("City Building", p+new Vector3(0,h/2,0), new Vector3(w,h,d), matConcrete);
        Cube("Roof Cover", p+new Vector3(0,h+.5f,0), new Vector3(w*.75f,1,d*.75f), matDark);
        for(int fl=0; fl<Mathf.FloorToInt(h/3f); fl++)
        {
            Cube("Glass Window",p+new Vector3(-w/2-.05f,2+fl*3,0),new Vector3(.1f,1.1f,2),matGlass);
            Cube("Glass Window",p+new Vector3(w/2+.05f,2+fl*3,0),new Vector3(.1f,1.1f,2),matGlass);
        }
    }

    void AddLoot(int count, float rangeX, float rangeZ)
    {
        for (int i = 0; i < count; i++)
        {
            var loot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            loot.tag = "AWRWorld";
            loot.name = "Resource Loot";
            loot.transform.position = new Vector3(Random.Range(-rangeX, rangeX), .45f, Random.Range(-rangeZ, rangeZ));
            loot.transform.localScale = Vector3.one * .55f;
            loot.GetComponent<Renderer>().material = matLoot;
            var l = loot.AddComponent<ResourceLoot>();
            l.amount = Random.Range(5, 30);
        }
    }

    void AddAtmosphereLights(int count)
    {
        for(int i=0;i<count;i++)
        {
            var l = new GameObject("Atmosphere Light "+i).AddComponent<Light>();
            l.gameObject.tag = "AWRWorld";
            l.type = LightType.Point;
            l.range = 22;
            l.intensity = quality == QualityPreset.Low ? 0 : 1.35f;
            l.color = i%2==0?new Color(.25f,.55f,1):new Color(1,.55f,.22f);
            l.transform.position = new Vector3(Mathf.Sin(i*1.4f)*90, 7, Mathf.Cos(i*1.1f)*70);
        }
    }

    void Bounds(float x,float z)
    {
        Cube("North Boundary",new Vector3(0,2,z),new Vector3(x*2,4,2),matDark);
        Cube("South Boundary",new Vector3(0,2,-z),new Vector3(x*2,4,2),matDark);
        Cube("East Boundary",new Vector3(x,2,0),new Vector3(2,4,z*2),matDark);
        Cube("West Boundary",new Vector3(-x,2,0),new Vector3(2,4,z*2),matDark);
    }

    void BuildZoneVisual()
    {
        var zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        zone.tag = "AWRWorld";
        zone.name = "MiniBR Zone Visual";
        zone.transform.position = zoneCenter + Vector3.up * .05f;
        zone.transform.localScale = new Vector3(zoneRadius * 2f, .02f, zoneRadius * 2f);
        zone.GetComponent<Renderer>().material = matZone;
    }

    void Ground(string name, float x, float z, Material m)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.tag = "AWRWorld";
        g.name=name;
        g.transform.localScale=new Vector3(x,1,z);
        g.GetComponent<Renderer>().material=m;
    }

    GameObject Cube(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var c=GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.tag = "AWRWorld";
        c.name=name;
        c.transform.position=pos;
        c.transform.localScale=scale;
        c.GetComponent<Renderer>().material=mat;
        return c;
    }

    void BuildPlayer()
    {
        playerRoot = new GameObject("AWR Player").transform;
        playerRoot.position = GetSpawnForTeam(localTeam);
        controller = playerRoot.gameObject.AddComponent<CharacterController>();
        controller.height = 1.85f;
        controller.radius = .35f;
        controller.center = new Vector3(0,.92f,0);

        if (externalCharacterPrefab)
        {
            var model = Instantiate(externalCharacterPrefab, playerRoot);
            model.name = "External Character";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            animator = model.GetComponentInChildren<Animator>();
            if (animator && externalAnimatorController)
                animator.runtimeAnimatorController = externalAnimatorController;
        }
        else
        {
            fallbackSoldier = AWRProceduralSoldier.Create("Fallback Procedural Soldier", playerRoot, matBlue, matDark, matMetal);
            fallbackAnim = playerRoot.gameObject.AddComponent<AWRRuntimeAnim>();
            fallbackAnim.body = fallbackSoldier.body;
            fallbackAnim.head = fallbackSoldier.head;
            fallbackAnim.leftArm = fallbackSoldier.leftArm;
            fallbackAnim.rightArm = fallbackSoldier.rightArm;
            fallbackAnim.leftLeg = fallbackSoldier.leftLeg;
            fallbackAnim.rightLeg = fallbackSoldier.rightLeg;
            fallbackAnim.weapon = fallbackSoldier.weapon;
        }

        cameraPivot = new GameObject("Camera Pivot").transform;
        cameraPivot.SetParent(playerRoot);
        cameraPivot.localPosition = new Vector3(0,1.7f,0);

        var camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        camObj.transform.SetParent(cameraPivot);
        camObj.transform.localPosition = thirdPerson ? new Vector3(0,1.1f,-5.2f) : new Vector3(0,.05f,.1f);
        camObj.transform.localRotation = Quaternion.identity;
        cam = camObj.AddComponent<Camera>();
        cam.fieldOfView = 62;
        ApplyQuality();

        spectatorTargets.Add(playerRoot);
    }

    Vector3 GetSpawnForTeam(int team)
    {
        foreach (var s in spawns)
            if (s.team == team)
                return s.position + Vector3.up;
        return new Vector3(0, 1, -28);
    }

    void BuildBots()
    {
        int activeBots = botCount;
        if (teamMode == TeamMode.OneVsOne) activeBots = 1;
        if (teamMode == TeamMode.TwoVsTwo) activeBots = 3;
        if (teamMode == TeamMode.ThreeVsThree) activeBots = 5;
        if (teamMode == TeamMode.ThreeVsThreeVsThree) activeBots = 8;

        for(int i=0;i<activeBots;i++)
        {
            int team = teamMode == TeamMode.FreeForAll ? i + 1 : (i % 3);
            if (team == localTeam && teamMode != TeamMode.FreeForAll) team = (team + 1) % 3;

            var botRoot = new GameObject("Bot Agent "+i).transform;
            botRoot.position = GetSpawnForTeam(team) + new Vector3(Random.Range(-4,4),0,Random.Range(-4,4));

            var botColor = team==0?matBlue:team==1?matRed:matGreen;
            AWRProceduralSoldier.Create("Bot Soldier Model "+i, botRoot, botColor, matDark, matMetal);

            var agent = botRoot.gameObject.AddComponent<BotAgent>();
            agent.manager = this;
            agent.team = team;
            agent.health = 100;
            bots.Add(agent);
            spectatorTargets.Add(botRoot);
        }
    }

    void BuildUI()
    {
        var canvasObj = new GameObject("AWR UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);

        if (!FindObjectOfType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        hud = Text("HUD", new Vector2(40,-40), new Vector2(0,1), new Vector2(0,1), new Vector2(1250,290), 24, TextAnchor.UpperLeft);
        killFeedText = Text("KillFeed", new Vector2(-40,-40), new Vector2(1,1), new Vector2(1,1), new Vector2(620,220), 22, TextAnchor.UpperRight);
        centerText = Text("Center", Vector2.zero, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(1200,100), 32, TextAnchor.MiddleCenter);

        BuildLobbyPanel();
        BuildMobileControls();
        BuildSettingsPanel();
        BuildWeaponPanel();
    }

    Text Text(string name, Vector2 pos, Vector2 min, Vector2 max, Vector2 size, int fontSize, TextAnchor anchor)
    {
        var t = new GameObject(name, typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(canvas.transform,false);
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = fontSize;
        t.color = Color.white;
        t.alignment = anchor;
        var rt = t.rectTransform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(min.x == 1 ? 1 : min.x == .5f ? .5f : 0, max.y == 1 ? 1 : max.y == .5f ? .5f : 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return t;
    }

    void BuildLobbyPanel()
    {
        lobbyPanel = Panel("Lobby Panel", Vector2.zero, new Vector2(.5f,.5f), new Vector2(980,650), new Color(.03f,.04f,.06f,.96f), new Vector2(.5f,.5f));
        PanelText(lobbyPanel.transform, "AWR BATTLE ARENA", 44, new Vector2(0,-35), new Vector2(940,70), TextAnchor.MiddleCenter);
        lobbyInfo = PanelText(lobbyPanel.transform, "", 23, new Vector2(40,-120), new Vector2(900,300), TextAnchor.UpperLeft);

        AddPanelButton(lobbyPanel.transform, "Map: Warehouse", new Vector2(50,-430), () => { selectedMap = MapType.Warehouse; RefreshLobbyInfo(); });
        AddPanelButton(lobbyPanel.transform, "Map: City", new Vector2(330,-430), () => { selectedMap = MapType.City; RefreshLobbyInfo(); });
        AddPanelButton(lobbyPanel.transform, "Map: Harbor", new Vector2(610,-430), () => { selectedMap = MapType.Harbor; RefreshLobbyInfo(); });

        AddPanelButton(lobbyPanel.transform, "Respawn", new Vector2(50,-500), () => { matchMode = MatchMode.Respawn; RefreshLobbyInfo(); });
        AddPanelButton(lobbyPanel.transform, "Rounds", new Vector2(330,-500), () => { matchMode = MatchMode.Rounds; RefreshLobbyInfo(); });
        AddPanelButton(lobbyPanel.transform, "MiniBR", new Vector2(610,-500), () => { matchMode = MatchMode.MiniBR; RefreshLobbyInfo(); });

        AddPanelButton(lobbyPanel.transform, "START MATCH", new Vector2(330,-570), StartMatch);
    }

    void RefreshLobbyInfo()
    {
        if (!lobbyInfo) return;
        lobbyInfo.text =
            "Map: " + selectedMap + "\n" +
            "Mode: " + matchMode + "\n" +
            "Teams: " + teamMode + "\n" +
            "Bots: " + botCount + "\n" +
            "Quality: " + quality + "\n\n" +
            "Keyboard shortcuts:\n" +
            "1 Warehouse | 2 City | 3 Harbor\n" +
            "F2 Respawn | F3 Rounds | F4 MiniBR\n" +
            "F5 1v1 | F6 2v2 | F7 3v3 | F8 3v3v3 | F9 FFA\n" +
            "Enter Start";
    }

    void BuildMobileControls()
    {
        if (!mobileMode) return;

        AddMobileButton("MOVE", new Vector2(165,160), new Vector2(0,0), new Vector2(190,190), b => { mobileMove = b.Direction; });
        AddMobileButton("LOOK", new Vector2(-260,165), new Vector2(1,0), new Vector2(210,210), b => { mobileLook = b.Direction * 2.5f; });
        AddMobileButton("FIRE", new Vector2(-115,355), new Vector2(1,0), new Vector2(130,130), b => { inputFire = b.IsHeld; });
        AddMobileButton("AIM", new Vector2(-290,360), new Vector2(1,0), new Vector2(110,110), b => { inputAim = b.IsHeld; });
        AddMobileButton("JUMP", new Vector2(-455,230), new Vector2(1,0), new Vector2(105,105), b => { if(b.PressedThisFrame) inputJump = true; });
        AddMobileButton("CROUCH", new Vector2(-455,105), new Vector2(1,0), new Vector2(105,105), b => { if(b.PressedThisFrame) inputCrouch = true; });
        AddMobileButton("PRONE", new Vector2(-575,105), new Vector2(1,0), new Vector2(105,105), b => { if(b.PressedThisFrame) inputProne = true; });
        AddMobileButton("RELOAD", new Vector2(-145,210), new Vector2(1,0), new Vector2(105,105), b => { if(b.PressedThisFrame) inputReload = true; });
        AddMobileButton("GRENADE", new Vector2(-145,90), new Vector2(1,0), new Vector2(105,105), b => { if(b.PressedThisFrame) inputGrenade = true; });
        AddMobileButton("SPRINT", new Vector2(365,155), new Vector2(0,0), new Vector2(110,110), b => { inputSprint = b.IsHeld; });
        AddMobileButton("USE", new Vector2(500,155), new Vector2(0,0), new Vector2(100,100), b => { if(b.PressedThisFrame) inputInteract = true; });

        AddMenuButton("SET", new Vector2(35,-35), new Vector2(0,1), new Vector2(95,55), ToggleSettings);
        AddMenuButton("WPN", new Vector2(140,-35), new Vector2(0,1), new Vector2(95,55), ToggleWeaponMenu);
        AddMenuButton("EDIT", new Vector2(245,-35), new Vector2(0,1), new Vector2(105,55), ToggleEditControls);
        AddMenuButton("FPS", new Vector2(365,-35), new Vector2(0,1), new Vector2(95,55), ToggleCameraMode);
    }

    void AddMobileButton(string id, Vector2 pos, Vector2 anchor, Vector2 size, System.Action<AWRMobileButton> onUpdate)
    {
        var go = new GameObject(id, typeof(Image), typeof(AWRMobileButton));
        go.transform.SetParent(canvas.transform,false);
        go.GetComponent<Image>().color = new Color(.08f,.1f,.16f,.55f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x == 1 ? 1 : 0, anchor.y == 1 ? 1 : 0);
        rt.anchoredPosition = LoadButtonPos(id, pos);
        rt.sizeDelta = size;

        var label = new GameObject(id+"_Text", typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(go.transform,false);
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 20;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.sizeDelta = Vector2.zero;
        label.text = id;

        var btn = go.GetComponent<AWRMobileButton>();
        btn.id = id;
        btn.manager = this;
        btn.onUpdate = onUpdate;
        mobileButtons[id] = btn;
    }

    void AddMenuButton(string label, Vector2 pos, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction act)
    {
        var go = new GameObject(label, typeof(Image), typeof(Button));
        go.transform.SetParent(canvas.transform,false);
        go.GetComponent<Image>().color = new Color(.08f,.1f,.16f,.85f);
        go.GetComponent<Button>().onClick.AddListener(act);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x == 1 ? 1 : 0, anchor.y == 1 ? 1 : 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var t = new GameObject(label+"_Text", typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(go.transform,false);
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 18;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.sizeDelta = Vector2.zero;
        t.text = label;
    }

    Vector2 LoadButtonPos(string id, Vector2 defaultPos)
    {
        string keyX = "AWR_BTN_" + id + "_X";
        string keyY = "AWR_BTN_" + id + "_Y";
        if (!PlayerPrefs.HasKey(keyX)) return defaultPos;
        return new Vector2(PlayerPrefs.GetFloat(keyX), PlayerPrefs.GetFloat(keyY));
    }

    public void SaveButtonPos(string id, Vector2 pos)
    {
        PlayerPrefs.SetFloat("AWR_BTN_" + id + "_X", pos.x);
        PlayerPrefs.SetFloat("AWR_BTN_" + id + "_Y", pos.y);
        PlayerPrefs.Save();
    }

    public bool CanEditControls() => editControlsMode;

    void ToggleEditControls()
    {
        editControlsMode = !editControlsMode;
        StartCoroutine(CenterMessage(editControlsMode ? "Edit Controls ON" : "Edit Controls OFF - Saved", 1.5f));
    }

    void ToggleSettings()
    {
        settingsOpen = !settingsOpen;
        if(settingsPanel) settingsPanel.SetActive(settingsOpen);
    }

    void ToggleWeaponMenu()
    {
        weaponMenuOpen = !weaponMenuOpen;
        if(weaponPanel) weaponPanel.SetActive(weaponMenuOpen);
    }

    void ToggleCameraMode()
    {
        thirdPerson = !thirdPerson;
        if(cam)
            cam.transform.localPosition = thirdPerson ? new Vector3(0,1.1f,-5.2f) : new Vector3(0,.05f,.1f);
    }

    void ToggleSpectator()
    {
        spectatorMode = !spectatorMode;
        if (spectatorMode)
            StartCoroutine(CenterMessage("Spectator ON - Arrow Keys switch target", 1.5f));
    }

    void BuildSettingsPanel()
    {
        settingsPanel = Panel("Settings Panel", new Vector2(-455,-105), new Vector2(1,1), new Vector2(420,500), new Color(.03f,.04f,.06f,.92f), new Vector2(1,1));
        settingsPanel.SetActive(false);

        PanelText(settingsPanel.transform, "SETTINGS", 28, new Vector2(20,-20), new Vector2(360,50), TextAnchor.MiddleLeft);
        AddPanelButton(settingsPanel.transform, "Graphics: LOW", new Vector2(25,-80), () => { quality = QualityPreset.Low; ApplyQuality(); });
        AddPanelButton(settingsPanel.transform, "Graphics: MEDIUM", new Vector2(25,-145), () => { quality = QualityPreset.Medium; ApplyQuality(); });
        AddPanelButton(settingsPanel.transform, "Graphics: ULTRA", new Vector2(25,-210), () => { quality = QualityPreset.Ultra; ApplyQuality(); });
        AddPanelButton(settingsPanel.transform, "Blood: ON/OFF", new Vector2(25,-275), () => { bloodEnabled = !bloodEnabled; });
        AddPanelButton(settingsPanel.transform, "Mobile: ON/OFF", new Vector2(25,-340), () => {
            mobileMode = !mobileMode;
            foreach(var b in mobileButtons.Values) b.gameObject.SetActive(mobileMode);
        });
        AddPanelButton(settingsPanel.transform, "Back To Lobby", new Vector2(25,-405), () => {
            ToggleSettings();
            ShowLobby();
        });
    }

    void BuildWeaponPanel()
    {
        weaponPanel = Panel("Weapon Panel", new Vector2(-455,-105), new Vector2(1,1), new Vector2(420,370), new Color(.03f,.04f,.06f,.92f), new Vector2(1,1));
        weaponPanel.SetActive(false);

        PanelText(weaponPanel.transform, "WEAPONS", 28, new Vector2(20,-20), new Vector2(360,50), TextAnchor.MiddleLeft);
        AddPanelButton(weaponPanel.transform, "Rifle", new Vector2(25,-80), () => SelectWeapon(WeaponSlot.Rifle));
        AddPanelButton(weaponPanel.transform, "Pistol", new Vector2(25,-145), () => SelectWeapon(WeaponSlot.Pistol));
        AddPanelButton(weaponPanel.transform, "Shotgun", new Vector2(25,-210), () => SelectWeapon(WeaponSlot.Shotgun));
        AddPanelButton(weaponPanel.transform, "Sniper", new Vector2(25,-275), () => SelectWeapon(WeaponSlot.Sniper));
    }

    GameObject Panel(string name, Vector2 pos, Vector2 anchor, Vector2 size, Color color, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(canvas.transform,false);
        go.GetComponent<Image>().color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return go;
    }

    Text PanelText(Transform parent, string text, int size, Vector2 pos, Vector2 rect, TextAnchor anchor)
    {
        var t = new GameObject(text, typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent,false);
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = anchor;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0,1);
        rt.anchorMax = new Vector2(0,1);
        rt.pivot = new Vector2(0,1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = rect;
        t.text = text;
        return t;
    }

    void AddPanelButton(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(text, typeof(Image), typeof(Button));
        go.transform.SetParent(parent,false);
        go.GetComponent<Image>().color = new Color(.1f,.14f,.22f,.95f);
        go.GetComponent<Button>().onClick.AddListener(action);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0,1);
        rt.anchorMax = new Vector2(0,1);
        rt.pivot = new Vector2(0,1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(260,52);

        var label = new GameObject(text+"_Text", typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(go.transform,false);
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 20;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.sizeDelta = Vector2.zero;
        label.text = text;
    }

    void SetupWeapons()
    {
        weapons[WeaponSlot.Rifle] = new WeaponData("AWR-R7 Rifle", 30, 150, 24, 240, 11.5f, 2.15f, 3f, .65f, 42, 1);
        weapons[WeaponSlot.Pistol] = new WeaponData("AWR-P9 Pistol", 12, 60, 28, 110, 4.2f, 1.45f, 2.2f, .35f, 48, 1);
        weapons[WeaponSlot.Shotgun] = new WeaponData("AWR-SG12 Shotgun", 6, 36, 13, 55, 1.15f, 2.6f, 5.5f, 3.4f, 46, 9);
        weapons[WeaponSlot.Sniper] = new WeaponData("AWR-X1 Sniper", 5, 25, 88, 650, .7f, 2.9f, 8f, .08f, 25, 1);
    }

    void SelectWeapon(WeaponSlot slot)
    {
        currentWeapon = slot;
        TriggerAnimator("SwitchWeapon");
        if(weaponPanel) weaponPanel.SetActive(false);
        weaponMenuOpen = false;
        StartCoroutine(CenterMessage("Weapon: " + weapons[currentWeapon].name, 1f));
    }

    WeaponSlot NextWeapon(WeaponSlot s)
    {
        if(s==WeaponSlot.Rifle) return WeaponSlot.Pistol;
        if(s==WeaponSlot.Pistol) return WeaponSlot.Shotgun;
        if(s==WeaponSlot.Shotgun) return WeaponSlot.Sniper;
        return WeaponSlot.Rifle;
    }

    void UpdateMatchTimer()
    {
        if (state != GameState.Playing) return;

        if (matchMode != MatchMode.Rounds)
        {
            matchTimer -= Time.deltaTime;
            if (matchTimer <= 0)
                EndMatch();
        }
    }

    void UpdateMiniBRZone()
    {
        if (matchMode != MatchMode.MiniBR || playerRoot == null) return;

        zoneRadius = Mathf.Max(28f, zoneRadius - Time.deltaTime * 1.6f);
        float dist = Vector3.Distance(new Vector3(playerRoot.position.x, 0, playerRoot.position.z), zoneCenter);
        if (dist > zoneRadius)
        {
            health -= Time.deltaTime * 5f;
            if (health <= 0) PlayerDie("Zone");
        }
    }

    void CollectInputs()
    {
        if (!mobileMode)
        {
            mobileMove = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            mobileLook = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectWeapon(WeaponSlot.Rifle);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectWeapon(WeaponSlot.Pistol);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectWeapon(WeaponSlot.Shotgun);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectWeapon(WeaponSlot.Sniper);
        if (Input.GetKeyDown(KeyCode.Q)) SelectWeapon(NextWeapon(currentWeapon));
    }

    void ResetFrameButtons()
    {
        inputJump = false;
        inputReload = false;
        inputGrenade = false;
        inputCrouch = false;
        inputProne = false;
        inputInteract = false;
    }

    void HandleLook()
    {
        Vector2 look = mobileMode ? mobileLook : new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        yaw += look.x * lookSensitivity;
        pitch -= look.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -25, 55);
        playerRoot.rotation = Quaternion.Euler(0,yaw,0);
        cameraPivot.localRotation = Quaternion.Euler(pitch,0,0);
    }

    void HandleMovement()
    {
        float x = mobileMode ? mobileMove.x : Input.GetAxis("Horizontal");
        float z = mobileMode ? mobileMove.y : Input.GetAxis("Vertical");

        bool keyboardSprint = Input.GetKey(KeyCode.LeftShift);
        sprint = (keyboardSprint || inputSprint) && z > .1f && !crouch && !prone;

        if(Input.GetKeyDown(KeyCode.C) || inputCrouch){ crouch = !crouch; prone = false; }
        if(Input.GetKeyDown(KeyCode.Z) || inputProne){ prone = !prone; crouch = false; }

        float speed = moveSpeed;
        if (sprint) speed = sprintSpeed;
        if (crouch) speed = 2.8f;
        if (prone) speed = 1.5f;

        float targetHeight = prone ? .8f : crouch ? 1.25f : 1.85f;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime*8f);
        controller.center = new Vector3(0, controller.height/2f,0);

        Vector3 move = playerRoot.right*x + playerRoot.forward*z;
        if(move.magnitude>1) move.Normalize();

        if(controller.isGrounded && verticalVelocity<0) verticalVelocity=-2;
        if((Input.GetKeyDown(KeyCode.Space) || inputJump) && controller.isGrounded && !prone) verticalVelocity = jumpForce;
        verticalVelocity += -22f * Time.deltaTime;

        controller.Move((move*speed + Vector3.up*verticalVelocity) * Time.deltaTime);
    }

    void HandleWeapons()
    {
        aim = Input.GetMouseButton(1) || inputAim;
        var w = weapons[currentWeapon];
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, aim ? w.adsFov : 62, Time.deltaTime*10f);

        fireCooldown -= Time.deltaTime;

        bool fire = Input.GetMouseButton(0) || inputFire;
        if(fire && fireCooldown<=0 && w.ammo>0 && !reloading)
            FireWeapon(w);

        if((Input.GetKeyDown(KeyCode.R) || inputReload) && !reloading)
            StartCoroutine(Reload(w));

        if((Input.GetKeyDown(KeyCode.G) || inputGrenade) && grenades>0)
            ThrowGrenade();
    }

    void FireWeapon(WeaponData w)
    {
        w.ammo--;
        fireCooldown = 1f / w.fireRate;
        TriggerAnimator("Fire");
        if(fallbackAnim) fallbackAnim.Fire();

        PlayTone(115f,.055f,.35f);
        MuzzleFlash(cam.transform.position+cam.transform.forward*1.5f, cam.transform.forward);

        int shots = Mathf.Max(1, w.pellets);
        for(int i=0;i<shots;i++)
        {
            float spread = aim ? w.spreadAds : w.spreadHip;
            if(w.pellets>1) spread += 7f;
            Vector3 dir = cam.transform.forward;
            dir += cam.transform.right * Random.Range(-spread,spread)*.01f;
            dir += cam.transform.up * Random.Range(-spread,spread)*.01f;
            dir.Normalize();

            if(Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, w.range))
            {
                ImpactSparks(hit.point, hit.normal);

                var bot = hit.collider.GetComponentInParent<BotAgent>();
                if(bot)
                {
                    if(bloodEnabled) Blood(hit.point, hit.normal);
                    bot.TakeDamage(w.damage, localTeam, this);
                    TriggerAnimator("Hit");
                }
            }
        }
    }

    IEnumerator Reload(WeaponData w)
    {
        if(w.ammo>=w.magSize || w.reserve<=0) yield break;
        reloading = true;
        TriggerAnimator("Reload");
        if(fallbackAnim) fallbackAnim.Reload();
        PlayTone(420f,.12f,.18f);
        yield return new WaitForSeconds(w.reloadTime);
        int need = w.magSize - w.ammo;
        int take = Mathf.Min(need, w.reserve);
        w.ammo += take;
        w.reserve -= take;
        reloading = false;
    }

    void ThrowGrenade()
    {
        grenades--;
        TriggerAnimator("Throw");
        if(fallbackAnim) fallbackAnim.Throw();

        Vector3 pos = cam.transform.position + cam.transform.forward*8f;
        Explosion(pos);
        PlayTone(65f,.25f,.5f);

        foreach(var c in Physics.OverlapSphere(pos,8f))
        {
            var bot = c.GetComponentInParent<BotAgent>();
            if(bot) bot.TakeDamage(90, localTeam, this);
        }
    }

    void HandleInteraction()
    {
        if (!(Input.GetKeyDown(KeyCode.E) || inputInteract)) return;

        foreach (var c in Physics.OverlapSphere(playerRoot.position, 3f))
        {
            var loot = c.GetComponent<ResourceLoot>();
            if (loot)
            {
                resources += loot.amount;
                Destroy(loot.gameObject);
                StartCoroutine(CenterMessage("Picked resources +" + loot.amount, 1f));
                return;
            }
        }
    }

    public void OnBotKilled(BotAgent bot, int killerTeam)
    {
        AddKillFeed("Team " + killerTeam + " eliminated Team " + bot.team);
        if (!teamScores.ContainsKey(killerTeam)) teamScores[killerTeam] = 0;
        teamScores[killerTeam]++;
        resources += 10;
        DropLoot(bot.transform.position, Random.Range(8, 25));

        if (matchMode == MatchMode.Rounds)
            CheckRoundEnd();

        if (matchMode == MatchMode.MiniBR)
            CheckMiniBREnd();
    }

    void DropLoot(Vector3 pos, int amount)
    {
        var loot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        loot.name = "Dropped Resources";
        loot.transform.position = pos + Vector3.up * .5f;
        loot.transform.localScale = Vector3.one * .55f;
        loot.GetComponent<Renderer>().material = matLoot;
        var l = loot.AddComponent<ResourceLoot>();
        l.amount = amount;
    }

    void CheckRoundEnd()
    {
        int alive = 0;
        int lastTeam = -1;
        foreach (var b in bots)
        {
            if (b && !b.dead)
            {
                alive++;
                lastTeam = b.team;
            }
        }

        if (alive <= 0)
        {
            if (!teamScores.ContainsKey(localTeam)) teamScores[localTeam] = 0;
            teamScores[localTeam]++;
            StartCoroutine(NextRound(localTeam));
        }
    }

    IEnumerator NextRound(int winner)
    {
        state = GameState.RoundEnd;
        AddKillFeed("Round winner: Team " + winner);
        yield return CenterMessage("ROUND " + roundNumber + " WINNER: TEAM " + winner, 2f);

        roundNumber++;
        if (roundNumber > roundLimit)
        {
            EndMatch();
            yield break;
        }

        foreach (var b in bots)
            if (b) Destroy(b.gameObject);
        bots.Clear();
        spectatorTargets.Clear();

        playerRoot.position = GetSpawnForTeam(localTeam);
        health = 100;
        armor = 50;
        dead = false;
        spectatorTargets.Add(playerRoot);

        BuildBots();
        state = GameState.Playing;
        yield return CenterMessage("ROUND " + roundNumber, 1.5f);
    }

    void CheckMiniBREnd()
    {
        int alive = 0;
        foreach (var b in bots)
            if (b && !b.dead) alive++;
        if (alive <= 0)
            EndMatch();
    }

    void PlayerDie(string reason)
    {
        if (dead) return;
        dead = true;
        TriggerAnimator("Death");
        lastDeathCameraPos = cam.transform.position;
        lastDeathCameraRot = cam.transform.rotation;
        AddKillFeed("You were eliminated by " + reason);

        if (matchMode == MatchMode.Respawn)
            StartCoroutine(RespawnPlayer());
        else
            StartCoroutine(KillCamThenSpectate());
    }

    IEnumerator RespawnPlayer()
    {
        yield return CenterMessage("Respawn in " + respawnSeconds + " seconds", respawnSeconds);
        playerRoot.position = GetSpawnForTeam(localTeam);
        health = 100;
        armor = 50;
        dead = false;
        if(animator) animator.SetBool("Dead", false);
    }

    IEnumerator KillCamThenSpectate()
    {
        killCamActive = true;
        killCamTimer = 3f;
        yield return new WaitForSeconds(3f);
        killCamActive = false;
        spectatorMode = true;
    }

    void EndMatch()
    {
        state = GameState.MatchEnd;
        int bestTeam = -1;
        int bestScore = -999;
        foreach (var kv in teamScores)
        {
            if (kv.Value > bestScore)
            {
                bestScore = kv.Value;
                bestTeam = kv.Key;
            }
        }
        StartCoroutine(CenterMessage("MATCH END - WINNER TEAM " + bestTeam, 5f));
    }

    void UpdateAnimation()
    {
        float moveAmount = Mathf.Abs(mobileMode ? mobileMove.x : Input.GetAxis("Horizontal")) + Mathf.Abs(mobileMode ? mobileMove.y : Input.GetAxis("Vertical"));

        if(animator)
        {
            animator.SetFloat("Speed", moveAmount);
            animator.SetFloat("DirectionX", mobileMode ? mobileMove.x : Input.GetAxis("Horizontal"));
            animator.SetFloat("DirectionY", mobileMode ? mobileMove.y : Input.GetAxis("Vertical"));
            animator.SetBool("Sprint", sprint);
            animator.SetBool("Crouch", crouch);
            animator.SetBool("Prone", prone);
            animator.SetBool("Aim", aim);
            animator.SetBool("Dead", dead);
        }

        if(fallbackAnim)
        {
            fallbackAnim.speed = moveAmount;
            fallbackAnim.sprint = sprint;
            fallbackAnim.crouch = crouch;
            fallbackAnim.prone = prone;
            fallbackAnim.aim = aim;
        }
    }

    void TriggerAnimator(string trigger)
    {
        if(animator)
            animator.SetTrigger(trigger);
    }

    void UpdateSpectator()
    {
        if (killCamActive)
        {
            cam.transform.position = lastDeathCameraPos;
            cam.transform.rotation = lastDeathCameraRot;
            killCamTimer -= Time.deltaTime;
            if (killCamTimer <= 0) killCamActive = false;
            return;
        }

        if(spectatorTargets.Count==0 || !cam) return;
        if(Input.GetKeyDown(KeyCode.RightArrow)) spectatorIndex = (spectatorIndex+1)%spectatorTargets.Count;
        if(Input.GetKeyDown(KeyCode.LeftArrow)) spectatorIndex = spectatorIndex<=0?spectatorTargets.Count-1:spectatorIndex-1;

        Transform t = spectatorTargets[Mathf.Clamp(spectatorIndex, 0, spectatorTargets.Count - 1)];
        if (!t) return;
        cam.transform.position = Vector3.Lerp(cam.transform.position, t.position + new Vector3(0,4,-7), Time.deltaTime*5f);
        cam.transform.LookAt(t.position + Vector3.up*1.5f);
    }

    void UpdateHUD()
    {
        if(!hud) return;

        if (state == GameState.Lobby)
        {
            hud.text = "";
            killFeedText.text = "";
            return;
        }

        var w = weapons[currentWeapon];
        string scoreText = "";
        foreach (var kv in teamScores)
            scoreText += "T" + kv.Key + ":" + kv.Value + " ";

        hud.text =
            "AWR Battle Arena v3 - Gameplay Complete\n" +
            "Map: " + selectedMap + " | Mode: " + matchMode + " | Round: " + roundNumber + "/" + roundLimit + " | Time: " + Mathf.CeilToInt(matchTimer) + "\n" +
            "Weapon: " + w.name + " | Ammo: " + w.ammo + "/" + w.reserve + " | Grenades: " + grenades + "\n" +
            "Health: " + Mathf.RoundToInt(health) + " | Armor: " + Mathf.RoundToInt(armor) + " | Resources: " + resources + " | Scores: " + scoreText + "\n" +
            "Quality: " + quality + " | Blood: " + (bloodEnabled ? "ON" : "OFF") + " | Camera: " + (thirdPerson ? "TPS" : "FPS") + " | EditBtns: " + (editControlsMode ? "ON" : "OFF") + "\n" +
            "PC: WASD Mouse Shift C Z RMB LMB R Q G E | Mobile buttons | TAB Spectator | F1 Settings";

        string feed = "";
        for (int i = 0; i < killFeed.Count; i++)
            feed += killFeed[i] + "\n";
        killFeedText.text = feed;
    }

    void AddKillFeed(string msg)
    {
        killFeed.Insert(0, msg);
        while (killFeed.Count > 6) killFeed.RemoveAt(killFeed.Count - 1);
    }

    IEnumerator CenterMessage(string msg, float seconds)
    {
        if(!centerText) yield break;
        centerText.text = msg;
        yield return new WaitForSeconds(seconds);
        if(centerText) centerText.text = "";
    }

    void MuzzleFlash(Vector3 pos, Vector3 dir)
    {
        if(quality == QualityPreset.Low) return;
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "VFX Muzzle Flash";
        s.transform.position = pos + dir*.3f;
        s.transform.localScale = Vector3.one*.25f;
        s.GetComponent<Renderer>().material = Emissive("Muzzle", new Color(1f,.7f,.1f), 2.5f);
        Destroy(s,.055f);
    }

    void ImpactSparks(Vector3 pos, Vector3 normal)
    {
        int count = quality == QualityPreset.Ultra ? 8 : quality == QualityPreset.Medium ? 4 : 1;
        for(int i=0;i<count;i++)
        {
            var sp=GameObject.CreatePrimitive(PrimitiveType.Cube);
            sp.name="VFX Spark";
            sp.transform.position=pos+normal*.04f;
            sp.transform.localScale=new Vector3(.035f,.035f,.22f);
            sp.transform.rotation=Random.rotation;
            var rb=sp.AddComponent<Rigidbody>();
            rb.mass=.05f;
            rb.AddForce((normal+Random.insideUnitSphere)*Random.Range(2f,5f),ForceMode.VelocityChange);
            sp.GetComponent<Renderer>().material=Emissive("Spark", Color.yellow, 1.8f);
            Destroy(sp,.45f);
        }
    }

    void Blood(Vector3 pos, Vector3 normal)
    {
        if(!bloodEnabled) return;
        var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        b.name = "Blood Impact";
        b.transform.position = pos + normal * .03f;
        b.transform.localScale = Vector3.one * .15f;
        b.GetComponent<Renderer>().material = matBlood;
        Destroy(b, .75f);
    }

    void Explosion(Vector3 pos)
    {
        var ring=GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ring.name="VFX Explosion";
        ring.transform.position=pos;
        ring.transform.localScale=Vector3.one*2.5f;
        ring.GetComponent<Renderer>().material=Emissive("Explosion", new Color(1f,.35f,.05f), 2f);
        Destroy(ring,.28f);
    }

    void PlayTone(float freq, float duration, float volume)
    {
        if(!playerRoot) return;
        var src = playerRoot.gameObject.GetComponent<AudioSource>();
        if(!src) src = playerRoot.gameObject.AddComponent<AudioSource>();
        int sr=44100;
        int samples=Mathf.CeilToInt(sr*duration);
        var clip=AudioClip.Create("AWR Tone",samples,1,sr,false);
        float[] data=new float[samples];
        for(int i=0;i<samples;i++)
        {
            float t=i/(float)sr;
            float env=1f-i/(float)samples;
            data[i]=Mathf.Sin(2*Mathf.PI*freq*t)*env*volume;
        }
        clip.SetData(data,0);
        src.PlayOneShot(clip);
    }

    [System.Serializable]
    public class WeaponData
    {
        public string name;
        public int magSize;
        public int ammo;
        public int reserve;
        public int startReserve;
        public float damage;
        public float range;
        public float fireRate;
        public float reloadTime;
        public float spreadHip;
        public float spreadAds;
        public float adsFov;
        public int pellets;

        public WeaponData(string name, int mag, int reserve, float damage, float range, float fireRate, float reloadTime, float hip, float ads, float fov, int pellets)
        {
            this.name = name;
            magSize = mag;
            ammo = mag;
            this.reserve = reserve;
            startReserve = reserve;
            this.damage = damage;
            this.range = range;
            this.fireRate = fireRate;
            this.reloadTime = reloadTime;
            spreadHip = hip;
            spreadAds = ads;
            adsFov = fov;
            this.pellets = pellets;
        }

        public void ResetAmmo()
        {
            ammo = magSize;
            reserve = startReserve;
        }
    }

    public class AWRSpawnPoint
    {
        public int team;
        public Vector3 position;
    }

    public class ResourceLoot : MonoBehaviour
    {
        public int amount = 10;
        float t;
        void Update()
        {
            t += Time.deltaTime;
            transform.Rotate(0, 90 * Time.deltaTime, 0);
            transform.position += Vector3.up * Mathf.Sin(t * 4f) * .001f;
        }
    }

    public class BotAgent : MonoBehaviour
    {
        public AWR_BattleArena_GameplayComplete_v3 manager;
        public int team;
        public float health = 100;
        public bool dead;
        Vector3 start;
        float t;
        float fireTimer;

        void Start()
        {
            start = transform.position;
        }

        void Update()
        {
            if (dead || manager == null) return;

            t += Time.deltaTime;
            transform.position = start + new Vector3(Mathf.Sin(t*.45f)*3f,0,Mathf.Cos(t*.32f)*3f);
            transform.rotation = Quaternion.Euler(0, Mathf.Sin(t*.4f)*45f, 0);

            fireTimer -= Time.deltaTime;
            if (manager.playerRoot != null && fireTimer <= 0)
            {
                float dist = Vector3.Distance(transform.position, manager.playerRoot.position);
                if (dist < 42f && team != manager.localTeam)
                {
                    fireTimer = Random.Range(.8f, 1.8f);
                    manager.BotShootPlayer(this);
                }
            }
        }

        public void TakeDamage(float dmg, int killerTeam, AWR_BattleArena_GameplayComplete_v3 mgr)
        {
            if (dead) return;
            health -= dmg;

            foreach(var r in GetComponentsInChildren<Renderer>())
                r.material.color = Color.Lerp(Color.red, Color.white, Mathf.Clamp01(health/100f));

            if(health <= 0)
            {
                dead = true;
                transform.rotation = Quaternion.Euler(82, transform.rotation.eulerAngles.y, 0);
                mgr.OnBotKilled(this, killerTeam);
            }
        }
    }

    public void BotShootPlayer(BotAgent bot)
    {
        if (dead) return;

        float damage = Random.Range(4f, 9f);
        if (armor > 0)
        {
            float absorbed = Mathf.Min(armor, damage * .55f);
            armor -= absorbed;
            damage -= absorbed;
        }

        health -= damage;
        TriggerAnimator("Hit");

        if (bloodEnabled)
            Blood(playerRoot.position + Vector3.up * 1.2f, Vector3.up);

        if (health <= 0)
            PlayerDie("Bot Team " + bot.team);
    }

    public class AWRProceduralSoldier
    {
        public Transform body, head, leftArm, rightArm, leftLeg, rightLeg, weapon;

        public static AWRProceduralSoldier Create(string name, Transform parent, Material fabric, Material armor, Material metal)
        {
            var s = new AWRProceduralSoldier();
            var root = new GameObject(name).transform;
            root.SetParent(parent);
            root.localPosition = Vector3.zero;

            s.body = Part(root,"Body",PrimitiveType.Cube,new Vector3(0,1.28f,0),new Vector3(.95f,1.35f,.52f),fabric);
            Part(root,"Chest Plate",PrimitiveType.Cube,new Vector3(0,1.5f,-.3f),new Vector3(.75f,.5f,.1f),armor);
            s.head = Part(root,"Head",PrimitiveType.Sphere,new Vector3(0,2.22f,0),new Vector3(.42f,.42f,.42f),armor);
            Part(root,"Helmet",PrimitiveType.Sphere,new Vector3(0,2.4f,0),new Vector3(.52f,.24f,.52f),metal);
            s.leftArm = Part(root,"Left Arm",PrimitiveType.Cube,new Vector3(-.72f,1.4f,0),new Vector3(.3f,1.05f,.3f),fabric);
            s.rightArm = Part(root,"Right Arm",PrimitiveType.Cube,new Vector3(.72f,1.4f,0),new Vector3(.3f,1.05f,.3f),fabric);
            s.leftLeg = Part(root,"Left Leg",PrimitiveType.Cube,new Vector3(-.3f,.5f,0),new Vector3(.34f,1.0f,.34f),armor);
            s.rightLeg = Part(root,"Right Leg",PrimitiveType.Cube,new Vector3(.3f,.5f,0),new Vector3(.34f,1.0f,.34f),armor);
            Part(root,"Backpack",PrimitiveType.Cube,new Vector3(0,1.35f,.42f),new Vector3(.64f,.95f,.2f),armor);
            s.weapon = Part(root,"Rifle",PrimitiveType.Cube,new Vector3(.55f,1.38f,-.46f),new Vector3(.2f,.18f,1.35f),metal);
            s.weapon.localRotation = Quaternion.Euler(12,8,0);

            return s;
        }

        static Transform Part(Transform parent,string n,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(type);
            go.name=n;
            go.transform.SetParent(parent);
            go.transform.localPosition=pos;
            go.transform.localScale=scale;
            go.GetComponent<Renderer>().material=mat;
            return go.transform;
        }
    }

    public class AWRRuntimeAnim : MonoBehaviour
    {
        public Transform body, head, leftArm, rightArm, leftLeg, rightLeg, weapon;
        public float speed;
        public bool sprint,crouch,prone,aim;
        float t;
        float actionTimer;
        string action;

        void Update()
        {
            t += Time.deltaTime;
            actionTimer -= Time.deltaTime;
            float animSpeed = sprint ? 10f : speed>.1f ? 6f : 2f;
            float amount = sprint ? 1.25f : speed>.1f ? .8f : .2f;
            if(crouch) amount *= .5f;
            if(prone) amount *= .3f;

            if(body)
            {
                float y = prone ? .45f : crouch ? .95f : 1.28f;
                body.localPosition = Vector3.Lerp(body.localPosition, new Vector3(0,y+Mathf.Sin(t*animSpeed)*.04f*amount,0), Time.deltaTime*8f);
                body.localRotation = Quaternion.Lerp(body.localRotation, Quaternion.Euler(0,Mathf.Sin(t*animSpeed*.4f)*4f*amount,0), Time.deltaTime*8f);
            }

            if(head) head.localRotation = Quaternion.Lerp(head.localRotation, Quaternion.Euler(Mathf.Sin(t*.8f)*2,Mathf.Sin(t*.6f)*4,0), Time.deltaTime*8f);
            if(leftArm) leftArm.localRotation = Quaternion.Lerp(leftArm.localRotation, Quaternion.Euler(Mathf.Sin(t*animSpeed)*28f*amount,0,0), Time.deltaTime*10f);
            if(rightArm) rightArm.localRotation = Quaternion.Lerp(rightArm.localRotation, Quaternion.Euler(Mathf.Sin(t*animSpeed+3.14f)*28f*amount,0,0), Time.deltaTime*10f);
            if(leftLeg) leftLeg.localRotation = Quaternion.Lerp(leftLeg.localRotation, Quaternion.Euler(Mathf.Sin(t*animSpeed)*32f*amount,0,0), Time.deltaTime*10f);
            if(rightLeg) rightLeg.localRotation = Quaternion.Lerp(rightLeg.localRotation, Quaternion.Euler(Mathf.Sin(t*animSpeed+3.14f)*32f*amount,0,0), Time.deltaTime*10f);

            if(weapon)
            {
                Quaternion target = Quaternion.Euler(12+Mathf.Sin(t*2)*2,8,0);
                if(aim) target = Quaternion.Euler(0,0,0);
                if(actionTimer>0 && action=="Reload") target = Quaternion.Euler(-35,18,12);
                if(actionTimer>0 && action=="Throw") target = Quaternion.Euler(55,0,-30);
                if(actionTimer>0 && action=="Fire") target = Quaternion.Euler(-8,10,0);
                weapon.localRotation = Quaternion.Lerp(weapon.localRotation,target,Time.deltaTime*10f);
            }
        }

        public void Fire(){ action="Fire"; actionTimer=.15f; }
        public void Reload(){ action="Reload"; actionTimer=.9f; }
        public void Throw(){ action="Throw"; actionTimer=.45f; }
    }
}

public class AWRMobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public string id;
    public AWR_BattleArena_GameplayComplete_v3 manager;
    public System.Action<AWRMobileButton> onUpdate;
    public bool IsHeld { get; private set; }
    public bool PressedThisFrame { get; private set; }
    public Vector2 Direction { get; private set; }

    RectTransform rt;
    Vector2 startPos;
    Vector2 pointerStart;
    bool draggingForEdit;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        PressedThisFrame = false;
        if (onUpdate != null) onUpdate(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsHeld = true;
        PressedThisFrame = true;
        startPos = rt.anchoredPosition;
        pointerStart = eventData.position;
        draggingForEdit = manager != null && manager.CanEditControls();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsHeld = false;
        Direction = Vector2.zero;
        if (draggingForEdit && manager != null)
            manager.SaveButtonPos(id, rt.anchoredPosition);
        draggingForEdit = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - pointerStart;

        if (draggingForEdit)
        {
            rt.anchoredPosition = startPos + delta;
            return;
        }

        Direction = Vector2.ClampMagnitude(delta / 90f, 1f);
    }
}
