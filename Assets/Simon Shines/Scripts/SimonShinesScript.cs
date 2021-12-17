using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KModkit;
using UnityEngine;
using rnd = UnityEngine.Random;

public class SimonShinesScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;

    public KMSelectable OnOffSwitch;
    public Light[] Lights;
    public MeshRenderer Emitter;
    public GameObject LightParent;

    private bool offPressed = false;
    private bool onOff = true;
    private float pressedTime;
    private int currentStage = 0;

    struct Colors
    {
        public Color32 Color;
        public string ColorName;
        public Func<int, KMBombInfo, int> Modifier;
    }

    private Colors[] colors;

    private readonly int[][] stageColors = new int[4][];
    private readonly int[] stageSeconds = new int[5];

    static int moduleIdCounter = 1;
    int moduleId;
    bool moduleSolved;

    KMSelectable.OnInteractHandler OnOffPressed()
    {
        return delegate
        {
            if (onOff)
            {

                if (moduleSolved)
                    return false;

                onOff = false;
                OnOffSwitch.GetComponent<Transform>().localEulerAngles = new Vector3(0f, 180f, 0f);
                var digit = (int) (Bomb.GetTime() % 10);
                if (stageSeconds[currentStage] != digit)
                {
                    Debug.LogFormat(@"[Simon Shines #{0}] Defuser struck! Switch was pressed at digit {1} instead of {2}. Reset to beginning.", moduleId, digit, stageSeconds[currentStage]);
                    Module.HandleStrike();
                    currentStage = 0;
                    return false;
                }
                pressedTime = Time.time;

                offPressed = false;
                if (currentStage == 4)
                {
                    moduleSolved = true;
                    StartCoroutine(Counter(solved: true));
                }
                else
                    StartCoroutine(Counter());

                OnOffSwitch.AddInteractionPunch();
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, OnOffSwitch.transform);
                LightParent.gameObject.SetActive(true);
                return false;
            }
            else
            {
                onOff = true;
                OnOffSwitch.GetComponent<Transform>().localEulerAngles = new Vector3(0f, 0f, 0f);
                offPressed = true;
                OnOffSwitch.AddInteractionPunch();
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, OnOffSwitch.transform);
                LightParent.gameObject.SetActive(false);
                Emitter.material.color = Color.white;
                currentStage++;
                return false;
            };
        };
    }

        void Start()
        {
            moduleId = moduleIdCounter++;

            OnOffSwitch.OnInteract += OnOffPressed();

            stageSeconds[0] = DigitalRoot(Bomb.GetSerialNumberNumbers().Sum() * 2);
            Debug.LogFormat(@"[Simon Shines #{0}] Initial second is {1}.", moduleId, stageSeconds[0]);

            colors = newArray(
                new Colors { Color = new Color32(255, 0, 0, 120), ColorName = "Red", Modifier = (initialTime, bomb) => DigitalRoot(initialTime * 2) },
                new Colors { Color = new Color32(0, 255, 0, 120), ColorName = "Green", Modifier = (initialTime, bomb) => DigitalRoot(Math.Max(bomb.GetBatteryCount(), bomb.GetIndicators().Count()) + initialTime) },
                new Colors { Color = new Color32(0, 0, 255, 120), ColorName = "Blue", Modifier = (initialTime, bomb) => DigitalRoot(initialTime / 2) },
                new Colors { Color = new Color32(255, 255, 0, 120), ColorName = "Yellow", Modifier = (initialTime, bomb) => DigitalRoot(Math.Max(bomb.GetPortPlateCount(), bomb.GetBatteryHolderCount()) + initialTime) });
            for (int i = 0; i < stageColors.Length; i++)
                generateStages(i);
        }

        private static T[] newArray<T>(params T[] array) { return array; }

        private IEnumerator Counter(bool solved = false)
        {
            if (solved)
            {
                for (int i = 0; i < 4; i++)
                {
                    setColor(i);
                    yield return new WaitForSeconds(.2f);
                }
                for (int i = 0; i < 4; i++)
                {
                    setColor(i);
                    yield return new WaitForSeconds(.2f);
                }
                Debug.LogFormat(@"[Simon Shines #{0}] Module solved.", moduleId);
                Module.HandlePass();
                LightParent.gameObject.SetActive(false);
                Emitter.material.color = Color.white;

            }
            else
            {
                var stage = currentStage;
                var strikes = Bomb.GetStrikes();
                var multiplier = strikes == 0 ? 1 : strikes == 1 ? 1.25 : strikes == 2 ? 1.5 : strikes == 3 ? 1.75 : 2;
                if (stageColors[currentStage].Length > 1)
                {
                    while (Mathf.Abs(pressedTime - Time.time) < 2 / multiplier && !offPressed && currentStage == stage)
                    {
                        for (int i = 0; i < 2 && !offPressed && currentStage == stage; i++)
                        {
                            setColor(stageColors[currentStage][i]);
                            yield return new WaitForSeconds(.3f);
                        }
                    }
                }
                else
                {
                    setColor(stageColors[currentStage][0]);
                    while (Mathf.Abs(pressedTime - Time.time) < 2 / multiplier && !offPressed && currentStage == stage)
                        yield return null;
                }
                if (!offPressed)
                {
                    Module.HandleStrike();
                    Debug.LogFormat(@"[Simon Shines #{0}] Defuser struck! Reason: The light was shining for too long.", moduleId);
                    LightParent.gameObject.SetActive(false);
                    onOff = true;
                    OnOffSwitch.GetComponent<Transform>().localEulerAngles = new Vector3(0f, 0f, 0f);
                    Emitter.material.color = Color.white;
                }
            }
            yield return null;
        }

        void setColor(int color)
        {
            Emitter.material.color = colors[color].Color;
            for (int i = 0; i < Lights.Length; i++)
                Lights[i].color = colors[color].Color;
        }

        void generateStages(int stage)
        {
            stageColors[stage] = Enumerable.Range(0, 4).ToArray().Shuffle().Take(rnd.Range(1, 3)).ToArray();

            if (stageColors[stage].Length > 1)
            {
                var tmp = colors[Math.Min(stageColors[stage][0], stageColors[stage][1])].Modifier(stageSeconds[stage], Bomb);
                stageSeconds[stage + 1] = colors[Math.Max(stageColors[stage][0], stageColors[stage][1])].Modifier(tmp, Bomb);
            }
            else
                stageSeconds[stage + 1] = colors[stageColors[stage][0]].Modifier(stageSeconds[stage], Bomb);

            Debug.LogFormat(@"[Simon Shines #{0}] Stage {1}: Color(s) flashing = {2} - Correct time to press: {3}",
                moduleId,
                stage + 1,
                stageColors[stage].Select(color => colors[color].ColorName).Join(", "),
                stageSeconds[stage + 1]);
        }

        private static int DigitalRoot(int number)
        {
            return (number - 1) % 9 + 1;
        }
    }
