using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class BaseArrowsScript : MonoBehaviour {

    public KMAudio MAudio;
    public KMColorblindMode Colorblind;
    public KMBombModule modSelf;
    public KMSelectable[] arrowButtons;

    protected bool isanimating = true;
    protected bool colorblindActive = false;
    public TextMesh textDisplay, colorblindArrowDisplay;

    private static int moduleIdCounter = 1;
    protected int moduleId;

    protected bool moduleSolved;

    // Use this for initialization
    void Start () {
        moduleId = moduleIdCounter++;
	}

    protected virtual void QuickLogDebug(string toLog = "")
    {
        Debug.LogFormat("<? Arrows #{0}>: {1}", moduleId, toLog);
    }

    protected virtual void QuickLogDebugFormat(string toLog = "", params object[] misc)
    {
        Debug.LogFormat("<? Arrows #{0}>: {1}", moduleId, string.Format(toLog, misc));
    }
    protected virtual void QuickLog(string toLog = "")
    {
        Debug.LogFormat("[? Arrows #{0}]: {1}", moduleId, toLog);
    }

    protected virtual void QuickLogFormat(string toLog = "", params object[] misc)
    {
        Debug.LogFormat("[? Arrows #{0}]: {1}", moduleId, string.Format(toLog, misc));
    }

    protected virtual IEnumerator victory() // The default victory animation from eXish's Arrows bretherns
    {
        isanimating = true;
        for (int i = 0; i < 100; i++)
        {
            int rand1 = Random.Range(0, 10);
            int rand2 = Random.Range(0, 10);
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
        modSelf.HandlePass();
    }

    // Update is called once per frame
    void Update () {

	}

    protected virtual IEnumerator ProcessTwitchCommand(string command)
    {
        if (moduleSolved || isanimating)
        {
            yield return "sendtochaterror The module is not accepting any commands at this moment.";
            yield break;
        }
        /*
        if (Regex.IsMatch(command, @"^\s*colou?rblind\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            colorblindActive = !colorblindActive;
            HandleColorblindToggle();
            yield break;
        }
        */
        if (Regex.IsMatch(command, @"^\s*u(p)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
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
        if (moduleSolved) { yield return "solve"; }
        yield break;
    }

    protected virtual IEnumerator TwitchHandleForcedSolve()
    {
        StartCoroutine(victory());
        while (isanimating) { yield return true; yield return new WaitForSeconds(0.1f); };
    }

}
