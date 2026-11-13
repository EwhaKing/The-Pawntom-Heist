using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pawntom.TutorialBridge
{
    /// <summary>
    /// 튜토리얼이 끝났을 때의 "MISSION COMPLETE" 연출.
    /// 글자가 하나씩 위에서 쿵쿵 떨어지며(노트가 떨어지듯) 바닥에 닿을 때마다 불꽃이 튀고 화면이 흔들린다.
    /// 마지막 글자가 떨어지면 번쩍임과 큰 흔들림, 불꽃 폭발, 글자 위를 훑는 빛줄기가 지나가고 부제목이 떠오른다.
    /// 타임라인은 Update 가 직접 돌린다(unscaled 시간 — 월드가 멈춰 있어도 진행). 불꽃은 미리 만든 풀을 재사용한다.
    /// </summary>
    public sealed class TutorialMissionCompleteView : MonoBehaviour
    {
        private const string Title = "MISSION COMPLETE";
        private const float LetterAdvance = 98f;       // 글자 간격(px, 기준 해상도)
        private const float SpaceAdvance = 58f;
        private const float FirstDelay = 0.3f;
        private const float LetterInterval = 0.075f;  // 글자가 이어서 떨어지는 간격
        private const float FallSeconds = 0.42f;
        private const float DropHeight = 1000f;
        private const float HoldAfterFinal = 2.2f;
        private const int SparkCount = 90;

        private static readonly Color GoldTop = new Color(1f, 0.97f, 0.7f, 1f);
        private static readonly Color GoldBottom = new Color(1f, 0.62f, 0.12f, 1f);

        private sealed class Letter
        {
            public RectTransform Rect;
            public TextMeshProUGUI Text;
            public float Start;
            public float RestX;
            public bool Landed;
            public float LandTime;
            public float Tilt;
        }

        private sealed class Spark
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Spin;
        }

        private RectTransform _root;
        private RectTransform _shake;
        private Image _flash;
        private Image _glint;
        private TextMeshProUGUI _subtitle;
        private Letter[] _letters;
        private Spark[] _sparks;
        private int _nextSpark;

        private Action<int> _onLetterLanded;
        private Action _onFinalSlam;

        private bool _playing;
        private float _time;
        private float _finalTime = -1f;
        private float _lastLetterLandTime = float.MaxValue;
        private float _shakeAmplitude;
        private float _flashAlpha;
        private bool _finalFired;

        public bool IsPlaying
        {
            get { return _playing; }
        }

        /// <summary>연출이 모두 끝났는가(마지막 글자가 떨어지고 유지 시간이 지났다).</summary>
        public bool IsDone
        {
            get { return _playing && _finalTime >= 0f && _time - _finalTime >= HoldAfterFinal; }
        }

        public void Build(TutorialUIFactory factory, Canvas overlay, Action<int> onLetterLanded, Action onFinalSlam)
        {
            _onLetterLanded = onLetterLanded;
            _onFinalSlam = onFinalSlam;

            _root = factory.CreateRect("MissionComplete", overlay.transform);
            TutorialUIFactory.Stretch(_root);

            _shake = factory.CreateRect("Shake", _root);
            TutorialUIFactory.Stretch(_shake);

            // 글자: 가운데 정렬로 한 줄. 공백은 간격만 둔다.
            float total = 0f;

            for (int i = 0; i < Title.Length; i++)
            {
                total += Title[i] == ' ' ? SpaceAdvance : LetterAdvance;
            }

            _letters = new Letter[CountLetters()];
            float x = -total * 0.5f;
            int index = 0;
            float delayClock = FirstDelay;

            for (int i = 0; i < Title.Length; i++)
            {
                if (Title[i] == ' ')
                {
                    x += SpaceAdvance;
                    delayClock += LetterInterval * 1.6f; // 단어 사이에서 한 박자 쉰다
                    continue;
                }

                RectTransform rect = factory.CreateRect("Letter" + index, _shake);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(LetterAdvance * 1.2f, 200f);

                TextMeshProUGUI text = factory.CreateText("T", rect, 150f, Color.white, TextAlignmentOptions.Center);
                text.fontStyle = FontStyles.Bold;
                text.text = Title[i].ToString();
                text.enableVertexGradient = true;
                text.colorGradient = new VertexGradient(GoldTop, GoldTop, GoldBottom, GoldBottom);
                text.outlineWidth = 0.22f;
                text.outlineColor = new Color32(50, 24, 0, 255);
                text.raycastTarget = false;
                TutorialUIFactory.Stretch(text.rectTransform);

                _letters[index] = new Letter
                {
                    Rect = rect,
                    Text = text,
                    Start = delayClock,
                    RestX = x + LetterAdvance * 0.5f,
                    Tilt = UnityEngine.Random.Range(-14f, 14f)
                };

                rect.gameObject.SetActive(false);
                x += LetterAdvance;
                delayClock += LetterInterval;
                index++;
            }

            // 글자 위를 훑고 지나가는 빛줄기
            _glint = factory.CreateImage("Glint", _shake, new Color(1f, 1f, 1f, 0f));
            _glint.rectTransform.anchorMin = _glint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _glint.rectTransform.sizeDelta = new Vector2(110f, 260f);
            _glint.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            _glint.raycastTarget = false;
            _glint.gameObject.SetActive(false);

            // 부제목
            _subtitle = factory.CreateText("Subtitle", _shake, 30f, new Color(0.95f, 0.9f, 0.75f, 1f), TextAlignmentOptions.Center);
            _subtitle.text = "교신 종료  ·  TUTORIAL COMPLETE";
            _subtitle.characterSpacing = 10f;
            _subtitle.raycastTarget = false;
            TutorialUIFactory.Place(_subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1400f, 50f));
            _subtitle.color = new Color(_subtitle.color.r, _subtitle.color.g, _subtitle.color.b, 0f);

            // 불꽃(미리 만들어 둔 풀)
            _sparks = new Spark[SparkCount];

            for (int i = 0; i < SparkCount; i++)
            {
                Image image = factory.CreateImage("Spark" + i, _shake, Color.white);
                image.raycastTarget = false;
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(10f, 10f);
                image.gameObject.SetActive(false);
                _sparks[i] = new Spark { Rect = rect, Image = image };
            }

            // 전체 번쩍임(맨 위)
            _flash = factory.CreateImage("Flash", _root, new Color(1f, 1f, 1f, 0f));
            TutorialUIFactory.Stretch(_flash.rectTransform);
            _flash.raycastTarget = false;
            _flash.gameObject.SetActive(false);

            _root.gameObject.SetActive(false);
        }

        private static int CountLetters()
        {
            int count = 0;

            for (int i = 0; i < Title.Length; i++)
            {
                if (Title[i] != ' ')
                {
                    count++;
                }
            }

            return count;
        }

        public void Play()
        {
            _playing = true;
            _time = 0f;
            _finalTime = -1f;
            _finalFired = false;
            _shakeAmplitude = 0f;
            _flashAlpha = 0f;
            _lastLetterLandTime = float.MaxValue;
            _root.gameObject.SetActive(true);

            for (int i = 0; i < _letters.Length; i++)
            {
                _letters[i].Landed = false;
                _letters[i].Rect.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            _time += dt;

            UpdateLetters();
            UpdateSparks(dt);

            // 흔들림은 빠르게 가라앉는다.
            _shakeAmplitude = Mathf.MoveTowards(_shakeAmplitude, 0f, dt * 90f);
            _shake.anchoredPosition = _shakeAmplitude > 0.1f
                ? new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * _shakeAmplitude
                : Vector2.zero;

            // 번쩍임
            _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, dt * 1.8f);
            bool flashOn = _flashAlpha > 0.003f;

            if (_flash.gameObject.activeSelf != flashOn)
            {
                _flash.gameObject.SetActive(flashOn);
            }

            if (flashOn)
            {
                _flash.color = new Color(1f, 0.96f, 0.85f, _flashAlpha);
            }

            UpdateFinale();
        }

        private void UpdateLetters()
        {
            float lastStart = 0f;

            for (int i = 0; i < _letters.Length; i++)
            {
                Letter letter = _letters[i];
                lastStart = Mathf.Max(lastStart, letter.Start);
                float p = (_time - letter.Start) / FallSeconds;

                if (p < 0f)
                {
                    continue;
                }

                if (!letter.Rect.gameObject.activeSelf)
                {
                    letter.Rect.gameObject.SetActive(true);
                }

                if (p < 1f)
                {
                    // 떨어지는 중: 가속하며 내려오고, 처음엔 크게 보였다가 제 크기로.
                    float y = Mathf.Lerp(DropHeight, 0f, p * p);
                    letter.Rect.anchoredPosition = new Vector2(letter.RestX, y);
                    letter.Rect.localScale = Vector3.one * Mathf.Lerp(1.9f, 1f, p);
                    letter.Rect.localRotation = Quaternion.Euler(0f, 0f, letter.Tilt * (1f - p));
                    SetLetterAlpha(letter, Mathf.Clamp01(p * 4f));
                    continue;
                }

                if (!letter.Landed)
                {
                    letter.Landed = true;
                    letter.LandTime = _time;
                    SpawnBurst(new Vector2(letter.RestX, -60f), 9, 520f);
                    _shakeAmplitude = Mathf.Max(_shakeAmplitude, 14f);

                    if (_onLetterLanded != null)
                    {
                        _onLetterLanded(i);
                    }

                    if (i == _letters.Length - 1)
                    {
                        _lastLetterLandTime = _time;
                    }
                }

                // 착지 뒤: 살짝 튀어 오르고(감쇠), 커졌다가 돌아온다.
                float since = _time - letter.LandTime;
                float hop = Mathf.Abs(Mathf.Sin(since * 15f)) * Mathf.Exp(-since * 7.5f) * 42f;
                float pop = 1f + 0.2f * Mathf.Exp(-since * 9f);
                letter.Rect.anchoredPosition = new Vector2(letter.RestX, hop);
                letter.Rect.localScale = Vector3.one * pop;
                letter.Rect.localRotation = Quaternion.identity;
                SetLetterAlpha(letter, 1f);
            }
        }

        private static void SetLetterAlpha(Letter letter, float alpha)
        {
            Color color = letter.Text.color;
            color.a = alpha;
            letter.Text.color = color;
        }

        // 마지막 글자가 닿은 뒤: 번쩍임·큰 흔들림·불꽃 폭발·빛줄기·부제목.
        private void UpdateFinale()
        {
            if (_lastLetterLandTime == float.MaxValue)
            {
                return;
            }

            float since = _time - _lastLetterLandTime;

            if (!_finalFired)
            {
                _finalFired = true;
                _finalTime = _time;
                _flashAlpha = 0.6f;
                _shakeAmplitude = 34f;
                SpawnBurst(new Vector2(0f, 20f), 46, 900f);

                if (_onFinalSlam != null)
                {
                    _onFinalSlam();
                }
            }

            // 빛줄기: 왼쪽에서 오른쪽으로 한 번 훑는다.
            float sweep = since / 0.75f;

            if (sweep >= 0f && sweep <= 1f)
            {
                if (!_glint.gameObject.activeSelf)
                {
                    _glint.gameObject.SetActive(true);
                }

                float eased = sweep * sweep * (3f - 2f * sweep);
                _glint.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-900f, 900f, eased), 0f);
                _glint.color = new Color(1f, 0.98f, 0.9f, Mathf.Sin(sweep * Mathf.PI) * 0.55f);
            }
            else if (_glint.gameObject.activeSelf)
            {
                _glint.gameObject.SetActive(false);
            }

            // 부제목은 조금 뒤에 떠오른다.
            float subtitleAlpha = Mathf.Clamp01((since - 0.5f) / 0.6f);
            Color color = _subtitle.color;
            color.a = subtitleAlpha;
            _subtitle.color = color;
        }

        private void SpawnBurst(Vector2 origin, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Spark spark = _sparks[_nextSpark];
                _nextSpark = (_nextSpark + 1) % _sparks.Length;

                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float velocity = UnityEngine.Random.Range(0.25f, 1f) * speed;
                spark.Position = origin;
                spark.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f + 0.5f) * velocity;
                spark.MaxLife = spark.Life = UnityEngine.Random.Range(0.45f, 1.0f);
                spark.Spin = UnityEngine.Random.Range(-360f, 360f);
                float size = UnityEngine.Random.Range(6f, 15f);
                spark.Rect.sizeDelta = new Vector2(size, size);
                spark.Image.color = Color.Lerp(GoldTop, GoldBottom, UnityEngine.Random.value);
                spark.Image.gameObject.SetActive(true);
            }
        }

        private void UpdateSparks(float dt)
        {
            for (int i = 0; i < _sparks.Length; i++)
            {
                Spark spark = _sparks[i];

                if (spark.Life <= 0f)
                {
                    continue;
                }

                spark.Life -= dt;

                if (spark.Life <= 0f)
                {
                    spark.Image.gameObject.SetActive(false);
                    continue;
                }

                spark.Velocity.y -= 1100f * dt;   // 중력
                spark.Velocity *= 1f - Mathf.Min(1f, dt * 1.2f);
                spark.Position += spark.Velocity * dt;
                spark.Rect.anchoredPosition = spark.Position;
                spark.Rect.localRotation = Quaternion.Euler(0f, 0f, 45f + spark.Spin * (spark.MaxLife - spark.Life));

                Color color = spark.Image.color;
                color.a = Mathf.Clamp01(spark.Life / spark.MaxLife * 1.4f);
                spark.Image.color = color;
            }
        }
    }
}
