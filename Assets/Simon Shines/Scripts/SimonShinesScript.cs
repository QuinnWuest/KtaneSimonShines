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

    public KMSelectable OnSwitch;
    public KMSelectable OffSwitch;
    public Light[] Lights;
    public Material Emitter;
    public GameObject LightParent;
    public KMSelectable SimonShines;

    private bool firstPress = true;
    private bool offPressed = false;
    private int newSecond;
    private int initialSecond;
    private int pressedSecond;
    private int currentStage = 0;

    struct Colors
    {
        public Color32 Color;
        public string ColorName;
        public Func<int, KMBombInfo, int> Modifier;
    }

    private Colors[] colors;

    private int[][] stageColors = new int[4][];
    private int[] stageSeconds = new int[4];

    static int moduleIdCounter = 1;
    int moduleId;
    bool moduleSolved;

    KMSelectable.OnInteractHandler OnPressed()
    {
        return delegate
        {
            if (moduleSolved)
                return false;

            if ((firstPress ? initialSecond : stageSeconds[currentStage]) != (int) (Bomb.GetTime() % 10))
            {
                Module.HandleStrike();
                Debug.LogFormat(@"[Simon Shines #{0}] Defuser struck! Reason: Switch was pressed at an incorrect time", moduleId);
                return false;
            }
            firstPress = false;
            pressedSecond = (int) Bomb.GetTime();

            if (stageColors[currentStage].Length > 1)
                StartCoroutine(Counter(flash: true));
            else
                StartCoroutine(Counter());

            if (currentStage == 3)
                StartCoroutine(Counter(solved:true));

            OffSwitch.AddInteractionPunch();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, OffSwitch.transform);
            OffSwitch.gameObject.SetActive(true);
            OnSwitch.gameObject.SetActive(false);
            LightParent.gameObject.SetActive(true);
            SimonShines.UpdateChildren();
            return false;
        };
    }

    KMSelectable.OnInteractHandler OffPressed()
    {
        return delegate
        {
            if (moduleSolved)
                return false;
            offPressed = true;
            OnSwitch.AddInteractionPunch();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, OnSwitch.transform);
            OffSwitch.gameObject.SetActive(false);
            OnSwitch.gameObject.SetActive(true);
            LightParent.gameObject.SetActive(false);
            SimonShines.UpdateChildren();
            currentStage++;
            return false;
        };
    }

    void Start()
    {
        moduleId = moduleIdCounter++;

        OnSwitch.OnInteract += OnPressed();
        OffSwitch.OnInteract += OffPressed();

        newSecond = initialSecond = DigitalRoot(Bomb.GetSerialNumberNumbers().Sum() * 2);
        Debug.LogFormat(@"[Simon Shines #{0}] Initial second is {1}.", moduleId, initialSecond);

        colors = new Colors[]
        {
        new Colors { Color = new Color32(255,0,0,120) , ColorName = "Red", Modifier = (initialTime, bomb) => DigitalRoot(initialTime * 2)},
        new Colors { Color = new Color32(0,255,0,120) , ColorName = "Green", Modifier = (initialTime, bomb) => DigitalRoot((bomb.GetBatteryCount() > bomb.GetIndicators().Count() ? bomb.GetBatteryCount() : bomb.GetIndicators().Count()) + initialTime)},
        new Colors { Color = new Color32(0,0,255,120) , ColorName = "Blue", Modifier = (initialTime, bomb) => DigitalRoot(initialTime / 2)},
        new Colors { Color = new Color32(0,255,255,120) , ColorName = "Yellow", Modifier = (initialTime, bomb) => DigitalRoot((bomb.GetPortPlateCount() > bomb.GetBatteryHolderCount() ? bomb.GetPortPlateCount() : bomb.GetBatteryHolderCount()) + initialTime)},
        };

        for (int i = 0; i < stageColors.Length; i++)
            generateStages(i);

    }

    private IEnumerator Counter(bool flash = false, bool solved = false)
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
            Module.HandlePass();
            Debug.LogFormat(@"[Simon Shines #{0}] Module solved.", moduleId);
            moduleSolved = true;
        }
        else
        {
            if (flash)
            {
                while (pressedSecond - (int) Bomb.GetTime() < 2)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        setColor(stageColors[currentStage][i]);
                        yield return new WaitForSeconds(.3f);
                    }
                    if (offPressed)
                        break;
                }
            }
            else
            {
                setColor(stageColors[currentStage][0]);
                while (pressedSecond - (int) Bomb.GetTime() < 2)
                {
                    yield return null;
                    if (offPressed)
                        break;
                }
            }
            if (!offPressed)
            {
                Module.HandleStrike();
                Debug.LogFormat(@"[Simon Shines #{0}] Defuser struck! Reason: The light was shining for too long.", moduleId);
                OffSwitch.gameObject.SetActive(false);
                OnSwitch.gameObject.SetActive(true);
                LightParent.gameObject.SetActive(false);
                SimonShines.UpdateChildren();
            }
        }
        yield return null;
    }

    void setColor(int color)
    {
        Emitter.color = colors[color].Color;
        for (int i = 0; i < Lights.Length; i++)
        {
            Lights[i].color = colors[color].Color;
        }
    }

    void generateStages(int stage)
    {
        if (rnd.Range(0, 2) == 0)
        {
            var c = Enumerable.Range(0, 4).ToList().Shuffle();
            stageColors[stage] = new int[] { c[0], c[1] };
        }
        else
            stageColors[stage] = new int[] { rnd.Range(0, 4) };

        if (stageColors[stage].Length > 1)
        {
            newSecond = stageColors[stage][0] < stageColors[stage][1] ? colors[stageColors[stage][0]].Modifier(newSecond, Bomb) : colors[stageColors[stage][1]].Modifier(newSecond, Bomb);
            newSecond = stageColors[stage][0] < stageColors[stage][1] ? colors[stageColors[stage][1]].Modifier(newSecond, Bomb) : colors[stageColors[stage][0]].Modifier(newSecond, Bomb);
            stageSeconds[stage] = newSecond;
        }
        else
        {
            newSecond = colors[stageColors[stage][0]].Modifier(newSecond, Bomb);
            stageSeconds[stage] = newSecond;
        }

        Debug.LogFormat(@"[Simon Shines #{0}] Stage {1}: Color(s) flashing = {2} - Correct time to press: {3}", moduleId, stage + 1, stageColors[stage].Length > 1 ? Enumerable.Range(0, 2).Select(i => colors[stageColors[stage][i]].ColorName).Join(", ") : colors[stageColors[stage][0]].ColorName, stageSeconds[stage]);

    }

    private static int DigitalRoot(int number)
    {
        while (number / 10 != 0)
        {
            int sum = 0;
            int i = 10;
            int j = 1;

            while (number / j >= 1)
            {
                sum += number % i / j;

                i *= 10;
                j *= 10;
            }

            number = sum;
        }

        return number;
    }
}