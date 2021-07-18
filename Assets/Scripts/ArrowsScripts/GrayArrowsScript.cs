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
    bool isanimating = true, colorblindActive = false, isActive, forceDisable;
    public TextMesh textDisplay, colorblindArrowDisplay;
    int moduleId, curRow, curCol, consectutiveCorrectPresses;
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
        finally
        {
            HandleColorblindToggle();
        }
    }

    // Use this for initialization
    void Start() {
        moduleId = moduleIdCounter++;

        textDisplay.text = "";

        needySelf.OnNeedyActivation += delegate {
            if (forceDisable)
                needySelf.HandlePass();
            else
                AssignValue();
        };

        needySelf.OnTimerExpired += delegate {
            QuickLog("Letting the needy timer run out is not a good idea after all.");
            consectutiveCorrectPresses = 0;
            //needySelf.SetResetDelayTime(15 + 5 * consectutiveCorrectPresses, 45 + 10 * consectutiveCorrectPresses); // Modify reactivation time based on the streak the module is on.
            needySelf.HandleStrike();
            isActive = false;
            textDisplay.text = "";
        };

        for (int x = 0; x < arrowButtons.Length; x++)
        {
            int y = x;
            arrowButtons[x].OnInteract += delegate {
                if (isActive && !isanimating)
                {
                    MAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, arrowButtons[y].transform);
                    arrowButtons[y].AddInteractionPunch();
                    CheckAnswer(y);
                }
                return false;
            };
        }

        needySelf.OnNeedyDeactivation += delegate
        {
            isActive = false;
            StartCoroutine(victory());
        };

        //StartCoroutine(DelayRotation());
        
    }
    void AssignValue()
    {
        //needySelf.SetNeedyTimeRemaining(Mathf.Max(needySelf.CountdownTime - 2 * consectutiveCorrectPresses, 30));

        string serialNo = bombInfo.GetSerialNumber();
        if (consectutiveCorrectPresses == 0)
        {
            curCol = char.IsDigit(serialNo[2]) ? int.Parse(serialNo[2].ToString()) : serialNo[2] - 'A' + 1;
            curRow = char.IsDigit(serialNo[5]) ? int.Parse(serialNo[5].ToString()) : serialNo[5] - 'A' + 1;
            QuickLog(string.Format("Starting position: Row {0}, Col {1}", curRow, curCol));
        }
        int randomDirection = uernd.Range(0, 4);
        switch (randomDirection)
        {
            case 3:
                curCol --;
                goto default;
            case 2:
                curRow ++;
                goto default;
            case 1:
                curCol ++;
                goto default;
            case 0:
                curRow --;
                goto default;
            default:
                curCol = (curCol + 10) % 10;
                curRow = (curRow + 10) % 10;
                break;
        }
        QuickLog(string.Format("The {0} arrow is shown at {1} consectutive press(es).", baseDirections[randomDirection], consectutiveCorrectPresses));
        QuickLog(string.Format("Current Position after moving based on display: Row {0}, Col {1}", curRow, curCol));
        QuickLog(string.Format("The desired arrow to press is the {0} arrow for this instance.", baseDirections[directionIDxTable[curRow, curCol]]));
        isActive = true;
        StartCoroutine(TypeText(displayDirections[randomDirection].ToString()));
        isanimating = true;
    }
    void CheckAnswer(int directionIdx)
    {
        if (directionIdx == directionIDxTable[curRow, curCol])
        {
            consectutiveCorrectPresses++;
        }
        else
        {
            QuickLog(string.Format("Strike! The {0} arrow was incorrectly pressed at {1} consectutive correct press(es)! Resetting the number of consectutive correct presses to 0.",
                baseDirections[directionIdx], consectutiveCorrectPresses));
            consectutiveCorrectPresses = 0;
            needySelf.HandleStrike();
        }
        textDisplay.text = "";
        //needySelf.SetResetDelayTime(15 + 5 * consectutiveCorrectPresses, 45 + 10 * consectutiveCorrectPresses); // Modify reactivation time based on the streak the module is on.
        needySelf.HandlePass();
        isActive = false;
    }
    /*
    IEnumerator DelayRotation()
    {
        yield return null;
        var needyTimer = gameObject.transform.Find("NeedyTimer(Clone)");
        if (needyTimer != null)
        {
            needyTimer.transform.localEulerAngles += new Vector3(0, 45, 0);
            needyTimer.transform.localPosition = new Vector3(-.1f, -.005f, -.1f);
            /*
            var allMeshFilters = needyTimer.GetComponentsInChildren<MeshFilter>(true);
            if (allMeshFilters != null)
            {
                foreach (MeshFilter oneMesh in allMeshFilters)
                {
                    oneMesh.gameObject.transform.localPosition = new Vector3(-.1f, .0225f, -.1f);
                    oneMesh.gameObject.transform.localEulerAngles += new Vector3(0, 45, 0);
                }
            }
            */
    /*
        }
        else
            Debug.Log("needytimer = null");
    }
    */
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

    void HandleColorblindToggle()
    {
        colorblindArrowDisplay.gameObject.SetActive(colorblindActive);
    }

    void QuickLog(string value)
    {
        Debug.LogFormat("[Gray Arrows #{0}] {1}", moduleId,value);
    }
    protected virtual IEnumerator victory() // The default victory animation from eXish's Arrows bretherns
    {
        textDisplay.transform.localPosition += Vector3.left * .02f;
        isanimating = true;
        for (int i = 0; i < 100; i++)
        {
            int rand1 = uernd.Range(0, 10);
            if (i < 50)
            {
                textDisplay.text = rand1 + "";
            }
            else
            {
                textDisplay.text = "G" + rand1;
            }
            yield return new WaitForSeconds(0.025f);
        }
        textDisplay.text = "GG";
        isanimating = false;
    }
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Press the specified arrow button with \"!{0} up/right/down/left\" Words can be substituted as one letter (Ex. right as r) Toggle colorblind mode with \"!{0} colorblind\"";
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
            HandleColorblindToggle();
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
        textDisplay.text = "";
        needySelf.SetResetDelayTime(float.MaxValue, float.MaxValue); // Modify reactivation time to forcably disable the module.
        needySelf.HandlePass();
        isActive = false;
        forceDisable = true;
    }

}
