using KModkit;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using uernd = UnityEngine.Random;

public class GrayArrowsScript : MonoBehaviour {

    public KMAudio MAudio;
    public KMColorblindMode Colorblind;
    public KMSelectable[] arrowButtons;
    public KMNeedyModule needySelf;
    public KMBombInfo bombInfo;

    private readonly int[,] directionIDxTable = {
        { 0, 3, 2, 1, 2, 0, 3, 2, 3, 3 }, // 0
        { 1, 1, 0, 2, 0, 2, 1, 3, 2, 0 }, // 1
        { 1, 3, 2, 0, 1, 0, 2, 3, 1, 3 }, // 2
        { 1, 2, 0, 2, 0, 1, 3, 1, 3, 0 }, // 3
        { 0, 3, 0, 3, 1, 2, 1, 0, 3, 2 }, // 4
        { 2, 1, 3, 0, 2, 1, 2, 0, 0, 1 }, // 5
        { 3, 3, 3, 0, 0, 1, 1, 2, 2, 2 }, // 6
        { 1, 2, 0, 3, 2, 1, 0, 0, 3, 3 }, // 7
        { 1, 0, 1, 3, 1, 2, 2, 1, 1, 0 }, // 8
        { 0, 2, 2, 0, 1, 3, 3, 1, 2, 1 }, // 9
    };
    readonly string[] baseDirections = { "Up", "Right", "Down", "Left", };
    private static int moduleIdCounter = 1;
    readonly string displayDirections = "\u25B4\u25B8\u25BE\u25C2";
    bool isanimating = true, colorblindActive = false, isActive, hasStarted;
    public TextMesh textDisplay, colorblindArrowDisplay, streakDisplay;
    int moduleId, curRow, curCol, currentStreak;
    void Awake()
    {
        try
        {
            colorblindActive = Colorblind.ColorblindModeActive;
        }
        catch
        {
            colorblindActive = false;
        }
    }

    // Use this for initialization
    void Start() {
        moduleId = moduleIdCounter++;

        textDisplay.text = "";
        for (int x = 0; x < arrowButtons.Length; x++)
        {
            int y = x;
            arrowButtons[x].OnInteract += delegate {
                MAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, arrowButtons[y].transform);
                arrowButtons[y].AddInteractionPunch();
                if (!(isActive || isanimating))
                {
                    //ProcessInput(y);
                }
                return false;
            };
        }
        needySelf.OnActivate += delegate { hasStarted = true; };

        needySelf.OnNeedyActivation += AssignValue;

        needySelf.OnTimerExpired += delegate {
            QuickLog("Letting the needy timer run out is not a good idea after all.");
            currentStreak = 0;
            needySelf.HandleStrike();
            isActive = false;
        };

        for (int x = 0; x < arrowButtons.Length; x++)
        {
            int y = x;
            arrowButtons[x].OnInteract += delegate {
                MAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, arrowButtons[y].transform);
                arrowButtons[y].AddInteractionPunch();
                if (isActive && !isanimating)
                    CheckAnswer(y);

                return false;
            };
        }

        bombInfo.OnBombSolved += delegate {
            StartCoroutine(victory());
            hasStarted = false;
        };

        StartCoroutine(DelayRotation());
        
    }
    void Update()
    {
        if (hasStarted)
            streakDisplay.text = isActive ? "" : string.Format("{0} streak!", currentStreak);
        else
        {
            string curDisplay = streakDisplay.text;
            if (!string.IsNullOrEmpty(curDisplay))
                streakDisplay.text = streakDisplay.text.Substring(0, curDisplay.Length - 1);
        }
    }
    void AssignValue()
    {
        needySelf.SetNeedyTimeRemaining(Mathf.Max(needySelf.CountdownTime - 2 * currentStreak, 35));

        string serialNo = bombInfo.GetSerialNumber();
        if (currentStreak == 0)
        {
            curRow = char.IsDigit(serialNo[2]) ? int.Parse(serialNo[2].ToString()) : serialNo[2] - 'A' + 1;
            curCol = char.IsDigit(serialNo[5]) ? int.Parse(serialNo[5].ToString()) : serialNo[5] - 'A' + 1;
            QuickLog(string.Format("Starting position: Row {0}, Col {1}", curRow, curCol));
        }
        int randomDirection = uernd.Range(0, 4);
        switch (randomDirection)
        {
            case 0:
                curCol -= 1;
                goto default;
            case 1:
                curRow += 1;
                goto default;
            case 2:
                curCol += 1;
                goto default;
            case 3:
                curRow -= 1;
                goto default;
            default:
                curCol = (curCol + 10) % 10;
                curRow = (curRow + 10) % 10;
                break;
        }
        QuickLog(string.Format("The {0} arrow is shown at {1} streak.", baseDirections[randomDirection], currentStreak));
        QuickLog(string.Format("Current Position after moving based on display: Row {0}, Col {1}", curRow, curCol));
        QuickLog(string.Format("The desired arrow to press is the {0} arrow.", baseDirections[directionIDxTable[curRow, curCol]]));
        isActive = true;
        StartCoroutine(TypeText(displayDirections[randomDirection].ToString()));
        isanimating = true;
    }
    void CheckAnswer(int directionIdx)
    {
        if (directionIdx == directionIDxTable[curRow, curCol])
        {
            currentStreak++;
        }
        else
        {
            QuickLog(string.Format( "Strike! The {0} was incorrectly pressed at {1} streak!",
                baseDirections[directionIdx], currentStreak));
            currentStreak = 0;
            needySelf.HandleStrike();
        }
        textDisplay.text = "";
        needySelf.SetResetDelayTime(15 + 5 * currentStreak, 45 + 10 * currentStreak); // Increase reactivation time based on how long the defuser disarms it.
        needySelf.HandlePass();
        isActive = false;
    }
    IEnumerator DelayRotation()
    {
        yield return null;
        var needyTimer = gameObject.transform.Find("NeedyTimer(Clone)");
        if (needyTimer != null)
        {
            var allMeshFilters = needyTimer.GetComponentsInChildren<MeshFilter>(true);
            if (allMeshFilters != null)
            {
                foreach (MeshFilter oneMesh in allMeshFilters)
                {
                    oneMesh.gameObject.transform.localPosition = new Vector3(-.06f, .0225f, -.06f);
                    oneMesh.gameObject.transform.localEulerAngles += new Vector3(0, 45, 0);
                }
            }
        }
        else
            Debug.Log("needytimer = null");
    }
    IEnumerator TypeText(string value)
    {
        textDisplay.text = "";
        for (int x = 1; x < value.Length + 1; x++)
        {
            yield return new WaitForSeconds(0.2f);
            textDisplay.text = value.Substring(0, x);
        }
        yield return new WaitForSeconds(0.2f);
        isanimating = false;
    }
    void QuickLog(string value)
    {
        Debug.LogFormat("[Gray Arrows #{0}] {1}", moduleId,value);
    }
    protected virtual IEnumerator victory() // The default victory animation from eXish's Arrows bretherns
    {
        isanimating = true;
        for (int i = 0; i < 100; i++)
        {
            int rand1 = uernd.Range(0, 10);
            int rand2 = uernd.Range(0, 10);
            if (i < 50)
            {
                textDisplay.text = rand1 + "" + rand2;
            }
            else
            {
                textDisplay.text = "G" + rand2;
            }
            yield return new WaitForSeconds(0.025f);
        }
        textDisplay.text = "GG";
        isanimating = false;
    }
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Press the specified arrow button with \"!{0} up/right/down/left\" Words can be substituted as one letter (Ex. right as r) Toggle colorblind mode with \"!{0} colorblind\" (This is a needy module, so try not to confuse this with another module!)";
#pragma warning restore 414
    protected IEnumerator ProcessTwitchCommand(string command)
    {
        if (!isActive || isanimating)
        {
            yield return "sendtochaterror The module is not accepting any commands at this moment.";
            yield break;
        }
        if (Regex.IsMatch(command, @"^\s*colou?rblind\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            colorblindActive = !colorblindActive;
            //HandleColorblindToggle();
            yield break;
        }
        else if (Regex.IsMatch(command, @"^\s*u(p)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            arrowButtons[0].OnInteract();
        }
        else if (Regex.IsMatch(command, @"^\s*d(own)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            arrowButtons[2].OnInteract();
        }
        else if (Regex.IsMatch(command, @"^\s*l(eft)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            arrowButtons[3].OnInteract();
        }
        else if (Regex.IsMatch(command, @"^\s*r(ight)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            arrowButtons[1].OnInteract();
        }
        yield break;
    }

    protected IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
    }

}
