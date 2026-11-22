using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Play.Ending
{
    /// <summary>
    /// 结局过场：
    ///   1. 小红原地播放 RedStand（循环）；
    ///   2. 小蓝从左侧走到小红附近（约屏幕 1/3 处），停下后冻结走路动画；
    ///   3. 小蓝依次说两句台词，每句 4 秒；
    ///   4. 小红播放 RedFianl 收尾。
    /// 台词文本在运行时创建，用系统中文字体，避免 TMP 默认字体没有中文字形。
    /// </summary>
    public class EndingSequence : MonoBehaviour
    {
        [Header("小红")]
        [SerializeField]
        private Animator red;

        [SerializeField]
        private string redStandState = "Red";

        [SerializeField]
        private string redFinalState = "RedFianl";

        [Header("小蓝")]
        [SerializeField]
        private Animator blue;

        [Tooltip("走路速度（世界单位/秒）")]
        [SerializeField]
        private float blueSpeed = 2f;

        [Tooltip("停下时距离小红多远（越小越近）")]
        [SerializeField]
        private float blueStopDistance = 3.5f;

        [Header("台词")]
        [SerializeField]
        private string[] lines = { "你做到了，小红", "你是我的骄傲" };

        [Tooltip("每句台词持续时长（秒）")]
        [SerializeField]
        private float lineDuration = 2.2f;

        [SerializeField]
        private int fontSize = 60;

        [Header("收尾")]
        [Tooltip("小红播完 Final 后停留多久")]
        [SerializeField]
        private float finalHold = 3f;

        [Header("结尾字幕")]
        [SerializeField]
        private string thanksText = "THANKS FOR PLAYING";

        [SerializeField]
        private int thanksFontSize = 56;

        [Tooltip("字幕淡入时长（秒）")]
        [SerializeField]
        private float thanksFadeDuration = 1.5f;

        private Text _dialogue;
        private Text _thanks;

        private void Start()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // 结局是独立的：进来先把 Main 和其它关卡卸掉（相机/AudioListener 只留结局场景的）
            yield return UnloadOtherScenes();

            CreateDialogue();

            yield return Play();
        }

        /// <summary>卸载除结局场景以外的所有已加载场景，并把结局设为活动场景。</summary>
        private IEnumerator UnloadOtherScenes()
        {
            var ending = gameObject.scene;
            var toUnload = new List<AsyncOperation>();

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene == ending) continue;

                toUnload.Add(SceneManager.UnloadSceneAsync(scene));
            }

            foreach (var op in toUnload)
            {
                while (op != null && !op.isDone) yield return null;
            }

            if (ending.IsValid() && ending.isLoaded) SceneManager.SetActiveScene(ending);
        }

        private IEnumerator Play()
        {
            if (red != null) red.Play(redStandState, 0, 0f);

            yield return WalkBlueIn();

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;

                _dialogue.text = line;
                yield return new WaitForSeconds(lineDuration);
                _dialogue.text = string.Empty;
            }

            if (red != null) red.Play(redFinalState, 0, 0f);

            yield return FadeInThanks();

            yield return new WaitForSeconds(finalHold);
        }

        /// <summary>Final 动画开始时，感谢字幕从透明渐渐浮现。</summary>
        private IEnumerator FadeInThanks()
        {
            if (_thanks == null) yield break;

            _thanks.text = thanksText;
            _thanks.color = new Color(1f, 1f, 1f, 0f);

            var time = 0f;
            while (time < thanksFadeDuration)
            {
                time += Time.deltaTime;

                var alpha = thanksFadeDuration <= 0f ? 1f : Mathf.Clamp01(time / thanksFadeDuration);
                _thanks.color = new Color(1f, 1f, 1f, alpha);

                yield return null;
            }

            _thanks.color = Color.white;
        }

        /// <summary>小蓝从当前位置走到小红左边，走完冻结在走路姿势。</summary>
        private IEnumerator WalkBlueIn()
        {
            if (blue == null) yield break;

            var start = blue.transform.position;
            var stopX = (red != null ? red.transform.position.x : 0f) - blueStopDistance;
            var target = new Vector3(stopX, start.y, start.z);

            blue.speed = 1f;

            while (Vector3.Distance(blue.transform.position, target) > 0.02f)
            {
                blue.transform.position = Vector3.MoveTowards(
                    blue.transform.position, target, blueSpeed * Time.deltaTime);

                yield return null;
            }

            blue.transform.position = target;
            blue.speed = 0f;   // 停下：冻结走路动画
        }

        /// <summary>创建屏幕下方的台词文本。</summary>
        private void CreateDialogue()
        {
            var canvasGo = new GameObject("DialogueCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var textGo = new GameObject("Dialogue");
            textGo.transform.SetParent(canvasGo.transform, false);

            var rect = textGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.08f);
            rect.anchorMax = new Vector2(0.9f, 0.26f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _dialogue = textGo.AddComponent<Text>();
            _dialogue.font = GetChineseFont();
            _dialogue.fontSize = fontSize;
            _dialogue.alignment = TextAnchor.MiddleCenter;
            _dialogue.color = Color.white;
            _dialogue.horizontalOverflow = HorizontalWrapMode.Overflow;
            _dialogue.verticalOverflow = VerticalWrapMode.Overflow;
            _dialogue.text = string.Empty;

            // 结尾的感谢字幕：屏幕中央，初始全透明，Final 开始时淡入
            var thanksGo = new GameObject("Thanks");
            thanksGo.transform.SetParent(canvasGo.transform, false);

            var thanksRect = thanksGo.AddComponent<RectTransform>();
            thanksRect.anchorMin = new Vector2(0.15f, 0.38f);
            thanksRect.anchorMax = new Vector2(0.85f, 0.62f);
            thanksRect.offsetMin = Vector2.zero;
            thanksRect.offsetMax = Vector2.zero;

            _thanks = thanksGo.AddComponent<Text>();
            _thanks.font = _dialogue.font;
            _thanks.fontSize = thanksFontSize;
            _thanks.alignment = TextAnchor.MiddleCenter;
            _thanks.color = new Color(1f, 1f, 1f, 0f);
            _thanks.text = string.Empty;
        }

        /// <summary>优先用系统中文字体，没有就退回内置字体。</summary>
        private Font GetChineseFont()
        {
            var names = new[]
            {
                "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun",
                "PingFang SC", "Noto Sans CJK SC", "Arial Unicode MS",
            };

            var font = Font.CreateDynamicFontFromOSFont(names, fontSize);
            if (font != null) return font;

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
