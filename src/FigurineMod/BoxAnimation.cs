using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FigurinePort
{
    /// <summary>
    /// 盲盒开盒动画 —— **1:1 移植 0.8.0-beta 的 `BoxAnimation`**。
    ///
    /// 反编译来源：`E:\旧mod\原神手办\发布\0.8.0-beta\Mods\GenshinFigurines.dll`
    ///   → `beta_decomp_080\BoxAnimation.decompiled.cs`（ilspycmd -t BoxAnimation）
    ///
    /// 结构（与 beta 完全一致）：
    /// ```
    /// Enqueue(box)  入队；没在播就 StartNext
    /// StartNext     EnsureUI → BuildCards（40 张全表洗牌）→ 进入 Scroll
    /// Tick          驱动三阶段：
    ///                 Scroll  缓出滚动，时长 Random(7.4, 8.6)s
    ///                 Snap    0.07s 从「目标 + OvershootPx」回弹到目标
    ///                 Hold    0.9s（任何输入可提前结束）
    /// EnterHold     按停下的位置算出压中第几张 → 建奖品 → BlindBox.GrantPrize
    /// EndCurrent    隐藏覆盖层 → 若还有排队就播下一条
    /// ```
    ///
    /// beta 里这些数值都存在 `FigurineConfig` 里；正式版移植没有配置系统，
    /// 这里**照抄 beta 的默认值**（不是我自己定的数）：
    ///   Speed 1 / HoldSec 0.9 / OvershootPx 0.5 / FadeWidth 0.15 / ClearRadius 0.24
    ///   DimAlpha 0.72 / CardW 280 / CardH 260 / CardGap 14
    ///
    /// ⚠️ beta 的 `CardCount`（默认 5）在 `BuildCards` 里是**死代码**（`_ = CardCount;`），
    ///    实际永远按整张目录洗牌 → 40 张卡。这里保持原样，不"修"它。
    /// ⚠️ beta **没有锁输入**：覆盖层不挡点击（Dim 的 raycastTarget=false），
    ///    滚动中按任意键/点鼠标只会把 `_t` 拉到末尾，Hold 中按键直接收尾。照抄。
    /// </summary>
    internal static class BoxAnimation
    {
        private enum Phase { Scroll, Snap, Hold }

        private static readonly Queue<GameItem> _queue = new Queue<GameItem>();

        private static bool _playing;
        private static Phase _phase;
        private static float _t;
        private static float _dur = 3.2f;
        private static float _scroll;
        private static float _startScroll;
        private static float _targetScroll;
        private static float _snapFrom;
        private static float _snapT;
        private static float _holdTimer;

        /// <summary>
        /// 刚开播的那一帧要忽略输入。
        /// 原因：beta 的触发点是「物品菜单里的使用」，点击发生在上一帧；
        /// 正式版移植的触发点是**双击**，鼠标按下和 Tick 在同一帧，
        /// 不做这个保护的话第一次 Tick 就会命中 `Input.GetMouseButtonDown(0)`，
        /// 把整个滚动段直接跳掉（实测：0.9 秒内就到结果）。
        /// 只忽略**第一帧**，之后的输入仍然照 beta 的规则跳过。
        /// </summary>
        private static bool _ignoreInputOnce;

        private static GameItem _box;
        /// <summary>本次开盒的奖品池（普通盒=三 IP 混池，专属盒=单 IP）。</summary>
        private static System.Collections.Generic.List<Catalog.Def> _pool;
        /// <summary>按盒子档位权重预先抽好的奖品（普通档偏低星、高级档偏高星）。</summary>
        private static Catalog.Def _prizeDef;
        private static Catalog.Def? _def;
        private static GameItem _prize;

        private static GameObject _root;
        private static RectTransform _strip;
        private static readonly List<RectTransform> _cards = new List<RectTransform>();
        private static readonly List<Catalog.Def> _cardDefs = new List<Catalog.Def>();
        private static readonly List<CanvasGroup> _cardGroups = new List<CanvasGroup>();
        private static readonly HashSet<GameItem> _seen = new HashSet<GameItem>();   // 已入队/正在开的盒子
        private static Text _titleText;
        private static Image _dimImage;
        private static Image _edgeFadeImage;
        private static readonly Dictionary<string, Sprite> _cardGradCache = new Dictionary<string, Sprite>();

        private static Sprite _blurSprite;
        private static Sprite _edgeFadeSprite;
        private static float _blurRadiusCached = -1f;
        private static Sprite _spotSprite;
        private static float _spotRadiusCached = -1f;

        // beta 里这张表用来把稀有度轮换映射成档位；BoxAnimation 内部没直接用，保留以示同源。
        private static readonly string[] TierCycle = new string[5] { "white", "blue", "purple", "red", "gold" };

        // ───────────────────────── beta 的配置默认值（无配置系统，取默认） ─────────────────────────

        public static bool Enabled { get { return true; } }
        public static float Speed { get { return 1f; } }
        public static int CardCount { get { return 5; } }

        private static float HoldSec { get { return 0.9f; } }
        private static float OvershootPx { get { return 0.5f; } }
        private static float FadeWidth { get { return 0.15f; } }
        private static float ClearRadius { get { return 0.24f; } }
        private static float DimAlpha { get { return 0.72f; } }
        public static float CardW { get { return 280f; } }
        public static float CardH { get { return 260f; } }
        public static float CardGap { get { return 14f; } }

        // ───────────────────────── 对外入口 ─────────────────────────

        /// <summary>双击盲盒时调用：入队；没在播就立刻开始。</summary>
        public static void Enqueue(GameItem box)
        {
            if (box == null) return;
            // 未拥有 → 锁住互动（双保险：双击入口已拦，这里防止其它调用方绕过）
            try
            {
                if (!GeneralHelper.IsItemOwned(box))
                {
                    BlindBox.ShowMessage("这个盲盒还不是你的，先买下来/捡起来再开");
                    Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 未拥有，拒绝开盒：" + box.identifier);
                    return;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("BoxAnimation.EnqueueOwn", __ex); }
            try
            {
                // 同一个盒子实例只能处理一次：动画播放期间重复双击不再入队
                // （否则一次开盒会发两次奖 —— 2026-11-05 玩家反馈「3 个盒子开出 4 件」）
                if (_seen.Contains(box))
                {
                    Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 该盒子已在队列/播放中，忽略重复开盒");
                    return;
                }
                _seen.Add(box);
                _queue.Enqueue(box);
                if (!_playing) StartNext();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 入队失败：" + ex.GetType().Name + ": " + ex.Message);
                try { BlindBox.OpenImmediate(box); } catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:119", __ex); }
            }
        }

        /// <summary>由 ProbeDriver.Update 每帧调用。</summary>
        public static void Tick()
        {
            if (!_playing) return;
            try
            {
                float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime) * Speed;

                if (_phase == Phase.Hold)
                {
                    _holdTimer -= dt;
                    if (!(_holdTimer > 0f) || Input.anyKeyDown || Input.GetMouseButtonDown(0))
                        EndCurrent();
                    return;
                }

                // 滚动/回弹中：任何输入 → 直接跳到本阶段末尾（beta 的"跳过"）
                // 但开播第一帧例外：那一下是触发开盒本身的点击/按键。
                if (_ignoreInputOnce) _ignoreInputOnce = false;
                else if (_phase != Phase.Snap && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
                    _t = _dur;

                _t += dt;

                if (_phase == Phase.Snap)
                {
                    _snapT += dt;
                    float k = Mathf.Clamp01(_snapT / 0.07f);
                    _scroll = Mathf.Lerp(_snapFrom, _targetScroll, k);
                    ApplyScroll();
                    if (k >= 1f) EnterHold();
                    return;
                }

                float p = (_dur <= 0f) ? 1f : Mathf.Clamp01(_t / _dur);
                float e = 1f - Mathf.Pow(1f - p, 3f);          // 缓出三次
                _scroll = Mathf.Lerp(_startScroll, _targetScroll, e);
                ApplyScroll();
                ApplyFade();

                if (p >= 1f)
                {
                    // 进入回弹：先冲过头 OvershootPx，再用 0.07s 拉回来
                    _phase = Phase.Snap;
                    _snapFrom = _targetScroll + OvershootPx;
                    _snapT = 0f;
                    _scroll = _snapFrom;
                    ApplyScroll();
                    ApplyFade();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] Tick 异常：" + ex.GetType().Name + ": " + ex.Message);
                Abort();
            }
        }

        /// <summary>是否有动画正在播放（外部判断用）。</summary>
        public static bool Playing { get { return _playing; } }

        // ───────────────────────── 内部流程 ─────────────────────────

        private static float CardX(int i)
        {
            return (float)i * (CardW + CardGap);
        }

        private static void StartNext()
        {
            GameItem box = null;
            try
            {
                if (_queue.Count == 0) return;
                box = _queue.Dequeue();
                if (box == null) { StartNext(); return; }

                _def = null;
                _prize = null;
                _box = box;
            _pool = BlindBox.PoolFor(box);
                _prizeDef = BlindBox.PickPrize(box != null ? box.identifier : BlindBox.IdGs);

                if (!EnsureUI()) { BlindBox.OpenImmediate(box); return; }

                BuildCards();
                _playing = true;
                _phase = Phase.Scroll;
                _t = 0f;
                _snapT = 0f;
                _ignoreInputOnce = true;
                // 0.8.0：_dur = Random.Range(7.4f, 8.6f) / max(0.3, Speed)（不是固定 8 秒）
                _dur = UnityEngine.Random.Range(7.4f, 8.6f) / Mathf.Max(0.3f, Speed);
                _scroll = _startScroll;

                if (_dimImage != null) _dimImage.color = new Color(0f, 0f, 0f, 0f);
                UseBlurBackdrop();
                if (_root != null) _root.SetActive(true);
                if (_titleText != null) _titleText.text = "开盲盒";

                ApplyScroll();
                ApplyFade();

                Debug.Log(FigurineMod.LogTag + $"[盲盒动画] 开始播放：卡片 {_cards.Count} 张 / 时长 {_dur:0.0}s");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 开始失败：" + ex.GetType().Name + ": " + ex.Message);
                Abort();
                if (box != null)
                {
                    try { BlindBox.OpenImmediate(box); } catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:234", __ex); }
                }
            }
        }

        private static void EnterHold()
        {
            try
            {
                float step = CardW + CardGap;
                int idx = Mathf.RoundToInt(_scroll / step);
                if (idx < 0) idx = 0;
                if (idx >= _cardDefs.Count) idx = _cardDefs.Count - 1;

                Catalog.Def def = (idx >= 0 && idx < _cardDefs.Count) ? _cardDefs[idx] : default(Catalog.Def);
                Debug.Log(FigurineMod.LogTag + $"[盲盒动画] 停下位置 {_scroll:0.0} → 压中第 {idx} 张：{def.Zh}");

                if (!string.IsNullOrEmpty(def.Id))
                {
                    GameItem prize = FigurineMod.BuildFigurine(def);
                    if (prize != null)
                    {
                        _def = def;
                        _prize = prize;
                        try { BlindBox.Acknowledge(def); } catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:258", __ex); }
                        BlindBox.GrantPrize(_box, def, prize);
                        if (_titleText != null)
                            _titleText.text = "开盲盒 · 开出【" + TierNameOf(def) + "】" + def.Zh;
                    }
                    else
                    {
                        Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 构建奖品失败");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 结算失败：" + ex.GetType().Name + ": " + ex.Message);
            }
            _phase = Phase.Hold;
            _holdTimer = HoldSec;
        }

        private static void EndCurrent()
        {
            try { if (_root != null) _root.SetActive(false); } catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:279", __ex); }
            Debug.Log(FigurineMod.LogTag + "[盲盒动画] 收尾（隐藏覆盖层）");
            _playing = false;
            if (_box != null) { _seen.Remove(_box); Debug.Log(FigurineMod.LogTag + "[盲盒动画] 已开盒解禁：" + (_box.identifier ?? "?")); }
            _box = null;
            _def = null;
            _prize = null;
            if (_queue.Count > 0) StartNext();
        }

        private static void Abort()
        {
            try { if (_root != null) _root.SetActive(false); } catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:290", __ex); }
            _playing = false;
            _box = null;
            _def = null;
            _prize = null;
        }

        private static void ApplyScroll()
        {
            if (_strip != null) _strip.anchoredPosition = new Vector2(0f - _scroll, 0f);
        }

        /// <summary>离中心越近的卡越大（beta 里 alpha 那段是死代码，恒为 1，照抄）。</summary>
        private static void ApplyFade()
        {
            try
            {
                for (int i = 0; i < _cardGroups.Count; i++)
                {
                    var g = _cardGroups[i];
                    if (g == null) continue;

                    float dist = Mathf.Abs(CardX(i) - _scroll);
                    g.alpha = 1f;

                    float near = Mathf.Clamp01(1f - dist / (CardW * 2.2f));
                    var rt = _cards[i];
                    if (rt != null)
                    {
                        // 0.8.0 是 1 + 0.12×near，但 CardW 280 / CardGap 14 时放大会超出间距 → 相邻卡重叠。
                        // 这里把放大上限压到「刚好填满间隙」，既保留放大观感，又不再叠在一起。
                        float maxGrow = Mathf.Max(0f, (CardGap - 2f) / CardW);
                        float grow = Mathf.Min(0.12f, maxGrow);
                        rt.localScale = Vector3.one * (1f + grow * near);
                    }
                }
            }
            catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:327", __ex); }
        }

        // ───────────────────────── 建 UI ─────────────────────────

        private static bool EnsureUI()
        {
            if (_root != null) return true;
            try
            {
                _root = new GameObject("FigurineBoxAnim");
                UnityEngine.Object.DontDestroyOnLoad(_root);
                _root.SetActive(false);

                var canvas = _root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000;
                // 全屏遮挡必须能接住射线，否则播放期间拖拽会穿到后面的世界物品
                _root.AddComponent<GraphicRaycaster>();

                var scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                // 暗色遮罩（beta 是透明起手，靠背景虚化接管）
                var dim = new GameObject("Dim");
                dim.transform.SetParent(_root.transform, false);
                _dimImage = dim.AddComponent<Image>();
                _dimImage.raycastTarget = true;   // 阻挡鼠标穿透（以前 false → 拖拽会抓到后面的物品）
                _dimImage.color = new Color(0f, 0f, 0f, 0f);
                FullStretch(dim);

                // 聚光/背景虚化层（UseBlurBackdrop 会替换贴图）
                var spot = new GameObject("Spotlight");
                spot.transform.SetParent(_root.transform, false);
                var spotImg = spot.AddComponent<Image>();
                spotImg.raycastTarget = false;
                spotImg.sprite = SpotlightSprite();
                var spotRt = spot.GetComponent<RectTransform>();
                spotRt.anchorMin = new Vector2(0.5f, 0.5f);
                spotRt.anchorMax = new Vector2(0.5f, 0.5f);
                spotRt.pivot = new Vector2(0.5f, 0.5f);
                spotRt.sizeDelta = new Vector2(1500f, 1500f);
                spotRt.anchoredPosition = Vector2.zero;

                // 标题
                var title = new GameObject("Title");
                title.transform.SetParent(_root.transform, false);
                _titleText = title.AddComponent<Text>();
                _titleText.raycastTarget = false;
                _titleText.text = "开盲盒";
                _titleText.fontSize = 46;
                _titleText.alignment = TextAnchor.MiddleCenter;
                _titleText.color = new Color(0.98f, 0.98f, 1f, 1f);
                try
                {
                    var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    if (f != null) _titleText.font = f;
                }
                catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:386", __ex); }
                var titleRt = title.GetComponent<RectTransform>();
                titleRt.sizeDelta = new Vector2(1000f, 80f);
                titleRt.anchoredPosition = new Vector2(0f, 360f);

                // 滚动条容器
                var strip = new GameObject("Strip");
                strip.transform.SetParent(_root.transform, false);
                _strip = strip.AddComponent<RectTransform>();
                _strip.anchorMin = new Vector2(0.5f, 0.5f);
                _strip.anchorMax = new Vector2(0.5f, 0.5f);
                _strip.pivot = new Vector2(0.5f, 0.5f);
                _strip.sizeDelta = new Vector2(0f, CardH);
                _strip.anchoredPosition = Vector2.zero;

                // 左右边缘压暗
                var edge = new GameObject("EdgeFade");
                edge.transform.SetParent(_root.transform, false);
                _edgeFadeImage = edge.AddComponent<Image>();
                _edgeFadeImage.raycastTarget = false;
                _edgeFadeImage.color = Color.white;
                FullStretch(edge);

                // 中间指示线
                var marker = new GameObject("Marker");
                marker.transform.SetParent(_root.transform, false);
                var markerImg = marker.AddComponent<Image>();
                markerImg.raycastTarget = false;
                markerImg.color = new Color(1f, 0.85f, 0.15f, 0.95f);
                var markerRt = marker.GetComponent<RectTransform>();
                markerRt.anchorMin = new Vector2(0.5f, 0.5f);
                markerRt.anchorMax = new Vector2(0.5f, 0.5f);
                markerRt.pivot = new Vector2(0.5f, 0.5f);
                markerRt.sizeDelta = new Vector2(4f, CardH + 70f);
                markerRt.anchoredPosition = new Vector2(0f, 0f);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 建 UI 失败：" + ex.GetType().Name + ": " + ex.Message);
                _root = null;
                return false;
            }
        }

        private static void FullStretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ───────────────────────── 贴图生成 ─────────────────────────

        /// <summary>卡片底：竖向渐变，顶部淡 → 底部深（beta 的 CardGradient）。</summary>
        private static Sprite CardGradient(Color c, string key)
        {
            try
            {
                if (_cardGradCache.TryGetValue(key, out var cached) && cached != null) return cached;

                var tex = new Texture2D(8, 128, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;

                var px = new Color32[1024];
                for (int i = 0; i < 128; i++)
                {
                    float t = (float)i / 127f;
                    float a = Mathf.Lerp(0.95f, 0.12f, t);
                    byte r = (byte)(Mathf.Clamp01(c.r) * 255f);
                    byte g = (byte)(Mathf.Clamp01(c.g) * 255f);
                    byte b = (byte)(Mathf.Clamp01(c.b) * 255f);
                    byte al = (byte)(Mathf.Clamp01(a) * 255f);
                    for (int j = 0; j < 8; j++) px[i * 8 + j] = new Color32(r, g, b, al);
                }

                tex.SetPixels32(px);
                tex.Apply(false, false);

                var sp = Sprite.Create(tex, new Rect(0f, 0f, 8f, 128f), new Vector2(0.5f, 0.5f), 100f);
                _cardGradCache[key] = sp;
                return sp;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 卡片渐变生成失败：" + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>背景虚化：截屏 → 200px 宽缩略图 → 点阵下采样得模糊底；中心留圆洞。</summary>
        private static Sprite MakeBlurredBackdrop()
        {
            try
            {
                float clearRadius = ClearRadius;
                if (_blurSprite != null && Mathf.Abs(_blurRadiusCached - clearRadius) < 0.01f) return _blurSprite;

                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                if (shot == null) return null;

                int width = shot.width;
                int height = shot.height;
                if (width <= 0 || height <= 0) return null;

                int w = 200;
                int h = Mathf.Max(1, Mathf.RoundToInt((float)w * (float)height / (float)width));

                var src = shot.GetPixels32();
                var dst = new Color32[w * h];

                float cx = (float)w * 0.5f;
                float cy = (float)h * 0.5f;
                float half = (float)Mathf.Min(w, h) * 0.5f;
                float holeR = half * (clearRadius * 2f);

                for (int i = 0; i < h; i++)
                {
                    for (int j = 0; j < w; j++)
                    {
                        int sr = 0, sg = 0, sb = 0, n = 0;
                        int x0 = j * width / w;
                        int y0 = i * height / h;
                        int x1 = (j + 1) * width / w;
                        int y1 = (i + 1) * height / h;
                        if (x1 <= x0) x1 = x0 + 1;
                        if (y1 <= y0) y1 = y0 + 1;

                        for (int y = y0; y < y1 && y < height; y += 2)
                        {
                            for (int x = x0; x < x1 && x < width; x += 2)
                            {
                                Color32 c = src[y * width + x];
                                sr += c.r; sg += c.g; sb += c.b; n++;
                            }
                        }
                        if (n == 0) n = 1;

                        float dx = (float)j - cx;
                        float dy = (float)i - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        float kx = Mathf.Abs(dx) / Mathf.Max(1f, half);
                        float ky = Mathf.Abs(dy) / Mathf.Max(1f, half);
                        float kMax = Mathf.Max(kx, ky);
                        float holeK = holeR / Mathf.Max(1f, half);

                        float alpha;
                        if (dist <= holeR) alpha = 0f;
                        else alpha = Mathf.Clamp01((kMax - holeK) / Mathf.Max(0.001f, 1f - holeK));

                        dst[i * w + j] = new Color32((byte)(sr / n), (byte)(sg / n), (byte)(sb / n), (byte)(Mathf.Clamp01(alpha) * 255f));
                    }
                }

                // 左右两侧再压一道暗，避免边缘卡突然出现
                var edge = new Color32[w * h];
                float keep = (float)w * 0.3f;
                float ramp = Mathf.Max(1f, (float)w * 0.5f - keep);
                for (int i = 0; i < h; i++)
                {
                    for (int j = 0; j < w; j++)
                    {
                        Color32 c = dst[i * w + j];
                        float a = Mathf.Clamp01((Mathf.Abs((float)j - (float)w * 0.5f) - keep) / ramp);
                        edge[i * w + j] = new Color32(c.r, c.g, c.b, (byte)(a * 255f));
                    }
                }

                var edgeTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                edgeTex.wrapMode = TextureWrapMode.Clamp;
                edgeTex.filterMode = FilterMode.Bilinear;
                edgeTex.SetPixels32(edge);
                edgeTex.Apply(false, false);
                _edgeFadeSprite = Sprite.Create(edgeTex, new Rect(0f, 0f, (float)w, (float)h), new Vector2(0.5f, 0.5f), 100f);

                var blurTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                blurTex.wrapMode = TextureWrapMode.Clamp;
                blurTex.filterMode = FilterMode.Bilinear;
                blurTex.SetPixels32(dst);
                blurTex.Apply(false, false);
                _blurSprite = Sprite.Create(blurTex, new Rect(0f, 0f, (float)w, (float)h), new Vector2(0.5f, 0.5f), 100f);

                _blurRadiusCached = clearRadius;
                Debug.Log(FigurineMod.LogTag + $"[盲盒动画] 背景虚化：截屏 {width}×{height}（屏幕 {Screen.width}×{Screen.height}）→ 模糊 {w}×{h}，圆洞半径 {holeR:0} px");
                return _blurSprite;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 背景虚化失败：" + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        private static void UseBlurBackdrop()
        {
            try
            {
                if (_root == null) return;
                var spot = _root.transform.Find("Spotlight");
                if (spot == null) return;
                var img = spot.GetComponent<Image>();
                if (img == null) return;

                var rt = img.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }

                try
                {
                    spot.SetAsLastSibling();
                    var title = _root.transform.Find("Title");
                    if (title != null) title.SetAsLastSibling();
                    var marker = _root.transform.Find("Marker");
                    if (marker != null) marker.SetAsLastSibling();
                }
                catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:612", __ex); }

                _blurSprite = null;
                var blur = MakeBlurredBackdrop();
                if (blur != null)
                {
                    img.sprite = blur;
                    img.color = Color.white;
                }

                if (_edgeFadeImage != null && _edgeFadeSprite != null)
                {
                    _edgeFadeImage.sprite = _edgeFadeSprite;
                    _edgeFadeImage.color = Color.white;
                }
                else
                {
                    img.sprite = SpotlightSprite();
                    img.color = Color.white;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 应用虚化失败：" + ex.GetType().Name);
            }
        }

        /// <summary>圆形遮罩（1024²，中心透明、外围 55% 白）——截屏失败时的兜底。</summary>
        private static Sprite SpotlightSprite()
        {
            try
            {
                float clearRadius = ClearRadius;
                if (_spotSprite != null && Mathf.Abs(_spotRadiusCached - clearRadius) < 0.01f) return _spotSprite;

                var tex = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;

                var px = new Color32[1048576];
                float cx = 511.5f, cy = 511.5f, rad = 512f;
                for (int i = 0; i < 1024; i++)
                {
                    for (int j = 0; j < 1024; j++)
                    {
                        float dx = ((float)j - cx) / rad;
                        float dy = ((float)i - cy) / rad;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float soft = 1.5f / rad;

                        float a;
                        if (d <= clearRadius - soft) a = 0f;
                        else if (d >= clearRadius + soft) a = 0.55f;
                        else a = Mathf.Lerp(0f, 0.55f, (d - (clearRadius - soft)) / (2f * soft));

                        px[i * 1024 + j] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                    }
                }

                tex.SetPixels32(px);
                tex.Apply(false, false);
                _spotSprite = Sprite.Create(tex, new Rect(0f, 0f, 1024f, 1024f), new Vector2(0.5f, 0.5f), 100f);
                _spotRadiusCached = clearRadius;
                return _spotSprite;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 生成圆形遮罩失败：" + ex.GetType().Name);
                return null;
            }
        }

        private static Color TierColorOf(string tier)
        {
            // tier 串仍是 beta 的 white/blue/purple/red/gold，映射到新的五档配色
            switch (tier)
            {
                case "gold": return Tier.Ui(5);
                case "red": return Tier.Ui(4);
                case "purple": return Tier.Ui(3);
                case "blue": return Tier.Ui(2);
                default: return Tier.Ui(1);
            }
        }

        // ───────────────────────── 卡片 ─────────────────────────

        /// <summary>
        /// 整张目录洗牌 → 每款一张卡 → 随机定「压中第几张」。
        /// 与 beta 一致：目标下标从 8 起步（clamp 后的下限），保证至少滚过 8 张。
        /// </summary>
        private static void BuildCards()
        {
            try
            {
                if (_strip == null) return;

                for (int i = _strip.childCount - 1; i >= 0; i--)
                {
                    try { UnityEngine.Object.Destroy(_strip.GetChild(i).gameObject); } catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:711", __ex); }
                }
                _cards.Clear();
                _cardGroups.Clear();
                _cardDefs.Clear();

                var list = new List<Catalog.Def>();
                try
                {
                    if (_pool != null && _pool.Count > 0) list.AddRange(_pool);
                    else
                    {
                        var all = Catalog.All;
                        if (all != null)
                            for (int i = 0; i < all.Length; i++) list.Add(all[i]);
                    }
                }
                catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:728", __ex); }
                if (list.Count == 0) return;

                // 整条带固定 80 张（用户 2026-11-03 定）：池子够大就取洗牌后的前 80 件，
                // 池子不足 80 就循环铺满；奖品仍由档位权重抽出后塞进停下的那一格。
                int count = 80;
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int k = UnityEngine.Random.Range(0, i + 1);
                    var tmp = list[i];
                    list[i] = list[k];
                    list[k] = tmp;
                }

                // ★ 先把「停下的那一格」和「抽好的奖品」定下来，再建卡。
                //   之前是先建卡、后写 list[target]，导致画面上显示的是原来那件、真正给的是另一件。
                float step0 = CardW + CardGap;
                int target = UnityEngine.Random.Range(Mathf.Clamp(8, 1, Mathf.Max(1, count - 1)), count);
                if (!string.IsNullOrEmpty(_prizeDef.Id))
                {
                    int at = ((target % list.Count) + list.Count) % list.Count;
                    list[at] = _prizeDef;
                }

                for (int j = 0; j < count; j++)
                {
                    Catalog.Def def = list[j % list.Count];

                    var card = new GameObject("Card" + j);
                    card.transform.SetParent(_strip, false);
                    var rt = card.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(CardW, CardH);
                    rt.anchoredPosition = new Vector2(CardX(j), 0f);

                    var cg = card.AddComponent<CanvasGroup>();
                    cg.blocksRaycasts = false;
                    cg.interactable = false;
                    cg.alpha = 1f;

                    string tier = TierOf(def);
                    Color tint = TierColorOf(tier);

                    var bg = card.AddComponent<Image>();
                    bg.raycastTarget = false;
                    var grad = CardGradient(tint, tier);
                    if (grad != null)
                    {
                        bg.sprite = grad;
                        bg.color = Color.white;
                    }
                    else
                    {
                        bg.color = new Color(tint.r, tint.g, tint.b, 0.92f);
                    }

                    var iconGo = new GameObject("Icon");
                    iconGo.transform.SetParent(card.transform, false);
                    iconGo.AddComponent<RectTransform>();
                    var icon = iconGo.AddComponent<Image>();
                    icon.raycastTarget = false;
                    var sp = SpriteOf(def.Id);
                    if (sp != null)
                    {
                        icon.sprite = sp;
                        icon.preserveAspect = true;
                    }
                    else
                    {
                        icon.color = new Color(0.25f, 0.25f, 0.3f, 1f);
                    }
                    var iconRt = iconGo.GetComponent<RectTransform>();
                    iconRt.anchorMin = new Vector2(0.06f, 0.1f);
                    iconRt.anchorMax = new Vector2(0.94f, 0.92f);
                    iconRt.offsetMin = Vector2.zero;
                    iconRt.offsetMax = Vector2.zero;

                    _cards.Add(rt);
                    _cardGroups.Add(cg);
                    _cardDefs.Add(def);
                }

                float step = step0;
                _startScroll = UnityEngine.Random.Range(0f, 3f) * step;
                float offset = UnityEngine.Random.Range(-0.55f, 0.55f) * step;

                _targetScroll = CardX(target) + offset;
                _scroll = _startScroll;
                ApplyScroll();

                Debug.Log(FigurineMod.LogTag + $"[盲盒动画] 目标卡 #{target}（偏移 {offset:0.0}）");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒动画] 建卡片失败：" + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static Sprite SpriteOf(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id)) return null;
                // 先用按需高清卡片图（Big\ 256px），拿不到再退回 48px 物品贴图
                try { var hd = CardArt.Get(id); if (hd != null) return hd; } catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:834", __ex); }
                var mods = ModLoader.Mods;
                if (mods == null) return null;
                foreach (var m in mods)
                {
                    if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                    Sprite sp = null;
                    try { if (m.Sprites != null && m.Sprites.TryGetValue(id, out sp) && sp != null) return sp; } catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:841", __ex); }
                    if (m.Textures != null && m.Textures.TryGetValue(id, out var tex) && tex != null)
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
            catch (System.Exception __ex) { Safe.Warn("BoxAnimation.cs:846", __ex); }
            return null;
        }

        // ───────────────────────── 稀有度映射（beta 的 FigurineDef.Tier / TierName） ─────────────────────────

        /// <summary>星数 → beta 的档位串（1★~5★）。</summary>
        private static string TierOf(Catalog.Def def)
        {
            int star = def.Star;
            if (star >= 5) return TierCycle[4];
            if (star == 4) return TierCycle[3];
            if (star == 3) return TierCycle[2];
            if (star == 2) return TierCycle[1];
            return TierCycle[0];
        }

        /// <summary>档位名（统一走 Tier.cs 的五档：消费/军规/受限/隐秘/违禁）。</summary>
        private static string TierNameOf(Catalog.Def def)
        {
            return Tier.Of(def.Star);
        }
    }
}
