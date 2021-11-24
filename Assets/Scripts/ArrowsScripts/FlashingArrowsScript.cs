using KModkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using uernd = UnityEngine.Random;
public class FlashingArrowsScript : BaseArrowsScript {

    public KMBombInfo mBombInfo;
    public MeshRenderer[] arrowsRenderer;
    public TextMesh[] colorblindArrowRenderMesh;

    Dictionary<int, Color> idxConnectedColors = new Dictionary<int, Color>() {
        { 0, Color.red }, // Red
        { 1, new Color(.86764705f, .44878295f, 0) }, // Orange
        { 2, new Color(1, .841f, 0f) }, // Yellow
        { 3, new Color(0.18393165f, 0.5955882f, 0.22083879f) }, // Green
        { 4, new Color(.03529412f, .043137256f, 1) }, // Blue
        { 5, new Color(.41911763f, 0, .3555275f) }, // Purple
        { 6, Color.white }, // White
    };
    readonly int[][][] idxPressArray = {
        new int[][] { // Red Row
            new int[] { 0, 2, 3, 1 },
            new int[] { 1, 2, 3, 0 },
            new int[] { 2, 0, 1, 3 },
            new int[] { 1, 0, 2, 3 },
            new int[] { 2, 0, 1, 3 },
            new int[] { 3, 0, 1, 2 },
            new int[] { 3, 0, 2, 1 },
        },
        new int[][] { // Orange Row
            new int[] { 2, 1, 0, 3 },
            new int[] { 1, 0, 3, 2 },
            new int[] { 2, 0, 1, 3 },
            new int[] { 1, 2, 3, 0 },
            new int[] { 2, 0, 3, 1 },
            new int[] { 3, 2, 1, 0 },
            new int[] { 2, 0, 3, 1 },
        },
        new int[][] { // Yellow Row
            new int[] { 1, 0, 3, 2 },
            new int[] { 2, 1, 0, 3 },
            new int[] { 0, 2, 1, 3 },
            new int[] { 2, 0, 1, 3 },
            new int[] { 1, 3, 2, 0 },
            new int[] { 2, 3, 1, 0 },
            new int[] { 2, 3, 1, 0 },
        },
        new int[][] { // Green Row
            new int[] { 1, 3, 0, 2 },
            new int[] { 1, 0, 3, 2 },
            new int[] { 1, 2, 0, 3 },
            new int[] { 3, 1, 0, 2 },
            new int[] { 3, 0, 2, 1 },
            new int[] { 3, 2, 1, 0 },
            new int[] { 1, 0, 2, 3 },
        },
        new int[][] { // Blue Row
            new int[] { 3, 0, 1, 2 },
            new int[] { 1, 2, 0, 3 },
            new int[] { 2, 3, 0, 1 },
            new int[] { 2, 3, 1, 0 },
            new int[] { 1, 0, 3, 2 },
            new int[] { 2, 0, 1, 3 },
            new int[] { 1, 0, 3, 2 },
        },
        new int[][] { // Purple Row
            new int[] { 3, 0, 2, 1 },
            new int[] { 0, 3, 2, 1 },
            new int[] { 0, 2, 3, 1 },
            new int[] { 3, 2, 1, 0 },
            new int[] { 3, 0, 2, 1 },
            new int[] { 2, 1, 0, 3 },
            new int[] { 1, 0, 2, 3 },
        },
        new int[][] { // White Row
            new int[] { 2, 3, 1, 0 },
            new int[] { 1, 3, 2, 0 },
            new int[] { 2, 3, 1, 0 },
            new int[] { 1, 3, 2, 0 },
            new int[] { 2, 0, 1, 3 },
            new int[] { 2, 1, 3, 0 },
            new int[] { 1, 0, 2, 3 },
        },
    };

    int[][] idxColorFlashingArrows = new int[4][];

    int displayNumber, curPos, idxReferencedArrow;
    bool hasStruck = false;
    private static int moduleIdCounter = 1;

    string[] debugDirections = { "Up", "Right", "Down", "Left" },
        debugColors = { "Red", "Orange", "Yellow", "Green", "Blue", "Purple", "White" };

    int[] correctPresses;

    IEnumerator arrowFlasher;

    // Use this for initialization
    void Start () {
        moduleId = moduleIdCounter++;
        modSelf.OnActivate += delegate {
            StopAllCoroutines();
            colorblindArrowDisplay.gameObject.SetActive(colorblindActive);

            GenerateAnswer();
        };
        textDisplay.text = "";
        mBombInfo.OnBombExploded += delegate { StopAllCoroutines(); };
        for (var x = 0; x < colorblindArrowRenderMesh.Length; x++)
        {
            colorblindArrowRenderMesh[x].text = "";
        }
        for (var x = 0; x < arrowButtons.Length; x++)
        {
            int y = x;
            arrowButtons[x].OnInteract += delegate {
                if (!(isanimating || moduleSolved))
                {
                    arrowButtons[y].AddInteractionPunch();
                    MAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, arrowButtons[y].transform);
                    ProcessInput(y);
                }
                return false;
            };

        }
        StartCoroutine(MakeArrowsCycleRainbow());
    }
    void ProcessInput(int idxArrow)
    {
        if (correctPresses == null || isanimating || moduleSolved)
        {
            return;
        }
        if (correctPresses[curPos] == idxArrow)
        {
            QuickLogFormat("You pressed {0} which is correct on position {1}!", debugDirections[idxArrow], curPos + 1);
            curPos++;
            if (curPos >= 4)
            {
                moduleSolved = true;
                QuickLog("Module solved.");
                StopCoroutine(arrowFlasher);
                StartCoroutine(victory());
            }
        }
        else
        {
            QuickLogFormat("You pressed {0} which is wrong on position {1}!", debugDirections[idxArrow], curPos + 1);
            curPos = 0;
            hasStruck = true;
            modSelf.HandleStrike();
            ResetSoftly();
            //GenerateAnswer();
        }
    }
    void ResetSoftly()
    {
        StopCoroutine(arrowFlasher);
        StartCoroutine(TypeNumber());
        arrowFlasher = FlashArrows();
        // Shift the flashing arrows a bit upon a reset, disabled for the time being for consistency.
        /*
        for (int x = 0; x < idxColorFlashingArrows.Length; x++)
        {
            int repeatCount = uernd.Range(0, 3);
            for (int z = 0; z < repeatCount; z++)
            {
                Debug.Log(x);
                int firstIdx = idxColorFlashingArrows[x][0];
                for (int y = 1; y < idxColorFlashingArrows[x].Length; y++)
                {
                    idxColorFlashingArrows[x][y - 1] = idxColorFlashingArrows[x][y];
                }
                idxColorFlashingArrows[x][idxColorFlashingArrows[x].Length - 1] = firstIdx;
            }
        }
        */

        StartCoroutine(arrowFlasher);
    }

    void HandleColorblindToggle()
    {
        colorblindArrowDisplay.gameObject.SetActive(colorblindActive);
    }
    void GenerateAnswer()
    {
        // Prep the flashing arrows
        for (int x = 0; x < idxColorFlashingArrows.Length; x++)
        {
            idxColorFlashingArrows[x] = new int[] { uernd.Range(0, 7), uernd.Range(0, 7), -1 }.Shuffle();
        }
        // Generate a random 2 digit number.
        displayNumber = uernd.Range(0, 100);
        QuickLogFormat("The displayed number is {0}.", displayNumber);

        var serNoNumbers = mBombInfo.GetSerialNumberNumbers();
        int modifier = serNoNumbers.Any() ? serNoNumbers.First() : mBombInfo.GetPortPlateCount();

        int selectedNumber = (displayNumber + modifier) % 5;
        if (selectedNumber == 0) selectedNumber = 1;

        QuickLogFormat("The number that should be obtained is {0}.", selectedNumber);

        int[] idxArrowSet = { 3, 1, 2, 0 };

        idxReferencedArrow = idxArrowSet[selectedNumber - 1];

        QuickLogFormat("The arrow to reference is the {0} arrow.", debugDirections[idxReferencedArrow]);

        var arrowSet = idxColorFlashingArrows[idxReferencedArrow];

        QuickLogFormat("This arrow is flashing the following colors: [ {0} ]", arrowSet.Select(a => a >= 0 ? debugColors[a] : "Black").Join(", "));

        var idxBlack = Array.IndexOf(arrowSet, -1);
        var colorAfterBlack = arrowSet[(idxBlack + 1) % 3];
        var colorBeforeBlack = arrowSet[(idxBlack + 2) % 3];


        correctPresses = idxPressArray[arrowSet[(idxBlack + 1) % 3]][arrowSet[(idxBlack + 2) % 3]];
        QuickLogFormat("Correct order to press ( {1} row, {2} column ): [ {0} ]", correctPresses.Select(a => debugDirections[a]).Join(", "), debugColors[colorAfterBlack], debugColors[colorBeforeBlack]);

        arrowFlasher = FlashArrows();

        StartCoroutine(TypeNumber());
        StartCoroutine(arrowFlasher);
    }
    protected override void QuickLog(string toLog = "")
    {
        Debug.LogFormat("[Flashing Arrows #{0}]: {1}", moduleId, toLog);
    }
    protected override void QuickLogFormat(string toLog = "", params object[] args)
    {
        Debug.LogFormat("[Flashing Arrows #{0}]: {1}", moduleId, string.Format(toLog,args));
    }
    IEnumerator TypeNumber()
    {
        textDisplay.text = "";
        yield return new WaitForSeconds(0.2f);
        textDisplay.text = displayNumber.ToString("00").Substring(0, 1);
        yield return new WaitForSeconds(0.2f);
        textDisplay.text = displayNumber.ToString("00");
        isanimating = false;
        yield return null;
    }
    IEnumerator FlashArrows()
    {
        int curIdx = 0;
        while (!moduleSolved)
        {
            for (var x = 0; x < arrowsRenderer.Length; x++)
            {
                int referencedIdx = idxColorFlashingArrows[x][curIdx];
                arrowsRenderer[x].material.color = idxConnectedColors.ContainsKey(referencedIdx) ? idxConnectedColors[referencedIdx] : Color.black;
                colorblindArrowRenderMesh[x].text = colorblindActive && referencedIdx >= 0 ? "ROYGBPW"[referencedIdx].ToString() : "";
                colorblindArrowRenderMesh[x].color = new[] { 2, 6 }.Contains(referencedIdx) ? Color.black : Color.white;
            }
            yield return new WaitForSeconds(0.5f);
            curIdx = (curIdx + 1) % 3;
        }    
    }

    IEnumerator MakeArrowsCycleRainbow()
    {
        int curIdx = 0;
        while (true)
        {
            for (int x = 0; x < arrowsRenderer.Length; x++)
            {
                arrowsRenderer[x].material.color = idxConnectedColors.ContainsKey(curIdx) ? idxConnectedColors[curIdx] : Color.black;
                colorblindArrowRenderMesh[x].text = "";
            }
            curIdx = (curIdx + 1) % 8;
            yield return new WaitForSeconds(0.2f);
        }
    }

    protected override IEnumerator victory()
    {
        isanimating = true;
        StartCoroutine(MakeArrowsCycleRainbow());
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
        modSelf.HandlePass();
    }

    // Update is called once per frame
    void Update () {

	}
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Press the specified arrow button with \"!{0} up/right/down/left\" Words can be substituted as one letter (Ex. right as r). Multiple directions can be issued in one command by spacing them out or as 1 word when abbrevivabted, I.E \"!{0} udlrrrll\". Alternatively, when abbreviated, you may space out the presses in the command. I.E. \"!{0} lluur ddlr urd\" Toggle colorblind mode with \"!{0} colorblind\"";
#pragma warning restore 414
    protected override IEnumerator ProcessTwitchCommand(string command)
    {
        if (moduleSolved || isanimating)
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
        else if (Regex.IsMatch(command, @"^\s*[uldr\s]+\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var usableCommand = command.Trim().ToLowerInvariant();
            List<int> allPresses = new List<int>();
            foreach (string directionportion in usableCommand.Split())
            {
                foreach (char dir in directionportion)
                {
                    switch (dir)
                    {
                        case 'u':
                            allPresses.Add(0);
                            break;
                        case 'd':
                            allPresses.Add(2);
                            break;
                        case 'l':
                            allPresses.Add(3);
                            break;
                        case 'r':
                            allPresses.Add(1);
                            break;
                        default:
                            yield return string.Format("sendtochaterror I do not know what direction \"{0}\" is supposed to be.", dir);
                            yield break;
                    }
                }
            }
            if (allPresses.Any())
            {
                hasStruck = false;
                for (int x = 0; x < allPresses.Count && !hasStruck; x++)
                {
                    yield return null;
                    if (allPresses[x] != correctPresses[curPos] && allPresses.Count > 1)
                        yield return string.Format("strikemessage by incorrectly pressing {0} after {1} press(es) in the TP command!", debugDirections[allPresses[x]], x + 1);
                    arrowButtons[allPresses[x]].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    if (moduleSolved) yield return "solve";
                }
            }
        }
        else
        {
            string[] cmdSets = command.Trim().Split();
            List<KMSelectable> allPresses = new List<KMSelectable>();
            hasStruck = false;
            for (int x = 0; x < cmdSets.Length; x++)
            {
                if (Regex.IsMatch(cmdSets[x], @"^\s*u(p)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[0]);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*d(own)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[2]);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*l(eft)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[3]);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*r(ight)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[1]);
                }
                else
                {
                    yield return string.Format("sendtochaterror I do not know what direction \"{0}\" is supposed to be.", cmdSets[x]);
                    yield break;
                }
            }
            for (var x = 0; x < allPresses.Count; x++)
            {
                yield return null;
                var debugIdx = Array.IndexOf(arrowButtons, allPresses[x]);
                if (debugIdx != correctPresses[curPos] && allPresses.Count > 1)
                    yield return string.Format("strikemessage by incorrectly pressing {0} after {1} press(es) in the TP command!", debugDirections[debugIdx], x + 1);
                allPresses[x].OnInteract();
                yield return new WaitForSeconds(0.1f);
                if (moduleSolved) { yield return "solve"; }
            }
            yield break;
        }
    }
    protected override IEnumerator TwitchHandleForcedSolve()
    {
        while (isanimating) { yield return true; yield return new WaitForSeconds(0.1f); };
        while (curPos < 4)
        {
            arrowButtons[correctPresses[curPos]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        while (isanimating) { yield return true; yield return new WaitForSeconds(0.1f); };
    }

}
