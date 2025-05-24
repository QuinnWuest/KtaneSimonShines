using System;
using System.Collections;
using System.Linq;
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

    private int moduleId;
    private static int moduleIdCounter = 1;
    private bool moduleSolved;

    public KMColorblindMode ColorblindMode;
    private bool _colorblindMode;
    public TextMesh ColorblindText;

    private void Start()
    {
        moduleId = moduleIdCounter++;

        _colorblindMode = ColorblindMode.ColorblindModeActive;
        SetColorblindMode(_colorblindMode);
        OnOffSwitch.OnInteract += OnOffPressed();

        stageSeconds[0] = DigitalRoot(Bomb.GetSerialNumberNumbers().Sum() * 2);
        Debug.LogFormat(@"[Simon Shines #{0}] Initial second is {1}.", moduleId, stageSeconds[0]);

        colors = new Colors[] {
                new Colors { Color = new Color32(255, 0, 0, 120), ColorName = "Red", Modifier = (initialTime, bomb) => DigitalRoot(initialTime * 2) },
                new Colors { Color = new Color32(0, 255, 0, 120), ColorName = "Green", Modifier = (initialTime, bomb) => DigitalRoot(Math.Max(bomb.GetBatteryCount(), bomb.GetIndicators().Count()) + initialTime) },
                new Colors { Color = new Color32(0, 0, 255, 120), ColorName = "Blue", Modifier = (initialTime, bomb) => DigitalRoot(initialTime / 2) },
                new Colors { Color = new Color32(255, 255, 0, 120), ColorName = "Yellow", Modifier = (initialTime, bomb) => DigitalRoot(Math.Max(bomb.GetPortPlateCount(), bomb.GetBatteryHolderCount()) + initialTime) } };
        for (int i = 0; i < stageColors.Length; i++)
            generateStages(i);
    }

    private void SetColorblindMode(bool mode)
    {
        ColorblindText.gameObject.SetActive(mode);
    }

    private KMSelectable.OnInteractHandler OnOffPressed()
    {
        return delegate
        {
            if (moduleSolved)
                return false;
            if (onOff)
            {
                onOff = false;
                OnOffSwitch.GetComponent<Transform>().localEulerAngles = new Vector3(0f, 180f, 0f);
                var digit = (int)(Bomb.GetTime() % 10);
                if (stageSeconds[currentStage] != digit)
                {
                    Module.HandleStrike();
                    Debug.LogFormat(@"[Simon Shines #{0}] Defuser struck! Switch was pressed at digit {1} instead of {2}. Reset to beginning.", moduleId, digit, stageSeconds[currentStage]);
                    currentStage = 0;
                    LightParent.gameObject.SetActive(false);
                    ColorblindText.text = "";
                    onOff = true;
                    OnOffSwitch.GetComponent<Transform>().localEulerAngles = new Vector3(0f, 0f, 0f);
                    Emitter.material.color = Color.white;
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

                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, OnOffSwitch.transform);
                LightParent.gameObject.SetActive(true);
                return false;
            }
            else
            {
                onOff = true;
                OnOffSwitch.GetComponent<Transform>().localEulerAngles = new Vector3(0f, 0f, 0f);
                offPressed = true;
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, OnOffSwitch.transform);
                LightParent.gameObject.SetActive(false);
                ColorblindText.text = "";
                Emitter.material.color = Color.white;
                currentStage++;
                return false;
            };
        };
    }

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
            ColorblindText.text = "";
            Emitter.material.color = Color.white;

        }
        else
        {
            var stage = currentStage;
            if (stageColors[currentStage].Length > 1)
            {
                while (Mathf.Abs(pressedTime - Time.time) < GetSwitchTime() && !offPressed && currentStage == stage)
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
                while (Mathf.Abs(pressedTime - Time.time) < GetSwitchTime() && !offPressed && currentStage == stage)
                    yield return null;
            }
            if (!offPressed)
            {
                Module.HandleStrike();
                Debug.LogFormat(@"[Simon Shines #{0}] Defuser struck! Reason: The light was shining for too long. Reset to beginning.", moduleId);
                currentStage = 0;
                LightParent.gameObject.SetActive(false);
                ColorblindText.text = "";
                onOff = true;
                OnOffSwitch.GetComponent<Transform>().localEulerAngles = new Vector3(0f, 0f, 0f);
                Emitter.material.color = Color.white;
            }
        }
        yield return null;
    }

    private double GetStrikeMultiplier()
    {
        return Bomb.GetStrikes() == 0 ? 1 : Bomb.GetStrikes() == 1 ? 1.25 : Bomb.GetStrikes() == 2 ? 1.5 : Bomb.GetStrikes() == 3 ? 1.75 : 2;
    }

    private void setColor(int color)
    {
        Emitter.material.color = colors[color].Color;
        for (int i = 0; i < Lights.Length; i++)
        {
            Lights[i].color = colors[color].Color;
            ColorblindText.text = colors[color].ColorName;
            ColorblindText.color = new Color32[] { new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255), new Color32(0, 0, 255, 255), new Color32(255, 255, 0, 255) }[color];
        }
    }

    private void generateStages(int stage)
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

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} flip at 4 [Flip the switch when the last digit of the timer is 4.] | 'flip at' is optional. | Note: When the light is turned on, it will automatically be turned off after 2 timer ticks.";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        Match m;
        m = Regex.Match(command, @"^\s*(colou?rblind|cb)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            _colorblindMode = !_colorblindMode;
            SetColorblindMode(_colorblindMode);
            yield break;
        }
        m = Regex.Match(command, @"^\s*((flip|press|submit)\s+(at\s+)?)?(?<d>\d)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            yield break;
        yield return null;
        int num = int.Parse(m.Groups["d"].Value);
        while ((int)Bomb.GetTime() % 10 != num)
            yield return "trycancel";
        yield return "strike";
        yield return "solve";
        OnOffSwitch.OnInteract();
        yield return new WaitForSeconds(GetSwitchTime() - 0.2f);
        OnOffSwitch.OnInteract();
        yield break;
    }

    private float GetSwitchTime()
    {
        return 2 / (Bomb.GetStrikes() == 0 ? 1 : Bomb.GetStrikes() == 1 ? 1.25f : Bomb.GetStrikes() == 2 ? 1.5f : Bomb.GetStrikes() == 3 ? 1.75f : 2);
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (!moduleSolved)
        {
            int t = stageSeconds[currentStage];
            while ((int)Bomb.GetTime() % 10 != t)
                yield return true;
            OnOffSwitch.OnInteract();
            yield return new WaitForSeconds(GetSwitchTime() - 0.2f);
            OnOffSwitch.OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
    }
}
