using KModkit;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using uernd = UnityEngine.Random;

public class BlackArrowsScript : BaseArrowsScript {

    public MeshRenderer[] arrowRenderers;
    public KMBossModule bossModule;
    public KMBombInfo bombInfo;
    public GameObject[] timerDisplayer;
    public Material[] setMats;

    string[] ignoreList = {
        "14",
        "42",
        "501",
        "A>N<D",
        "Bamboozling Time Keeper",
        "Black Arrows",
        "Brainf---",
        "Busy Beaver",
        "Don't Touch Anything",
        "Forget Any Color",
        "Forget Enigma",
        "Forget Everything",
        "Forget It Not",
        "Forget Me Later",
        "Forget Me Not",
        "Forget Perspective",
        "Forget The Colors",
        "Forget Them All",
        "Forget This",
        "Forget Us Not",
        "Iconic",
        "Kugelblitz",
        "Multitask",
        "OmegaDestroyer",
        "OmegaForget",
        "Organization",
        "Password Destroyer",
        "Purgatory",
        "RPS Judging",
        "Simon Forgets",
        "Simon's Stages",
        "Souvenir",
        "Tallordered Keys",
        "The Time Keeper",
        "The Troll",
        "The Heart",
        "The Twin",
        "The Very Annoying Button",
        "Timing is Everything",
        "Turn The Key",
        "Ultimate Custom Night",
        "Übermodule",
        "Whiteout",
    };
    readonly Dictionary<int, int> goalIdxPressesByValue = new Dictionary<int, int>() {
        { 1, 0 },
        { 2, 2 },
        { 3, 3 },
        { 4, 1 },
        { 5, 2 },
        { 6, 1 },
        { 7, 3 },
        { 8, 0 },
        { 9, 1 },
        { 10, 0 },
        { 11, 3 },
        { 12, 2 },
    };

    readonly Dictionary<int, string> idxToDirections = new Dictionary<int, string>()
    {
        { 0, "Up"},
        { 1, "Right"},
        { 2, "Down"},
        { 3, "Left"},
    };

    private readonly int[,] digitTable = {
        { 5, 7, 11, 8, 6, 7, 10, 12, 12, 1, },
        { 6, 11, 4, 9, 4, 8, 6, 6, 1, 10, },
        { 10, 9, 9, 12, 10, 10, 9, 9, 6, 1, },
        { 2, 6, 9, 1, 1, 11, 11, 3, 5, 1, },
        { 8, 6, 7, 1, 12, 4, 1, 8, 4, 5, },
        { 3, 8, 1, 4, 5, 9, 4, 9, 7, 7, },
        { 2, 11, 6, 3, 2, 8, 11, 5, 7, 6, },
        { 4, 9, 9, 10, 2, 8, 9, 3, 4, 2, },
        { 5, 5, 9, 9, 3, 8, 9, 5, 3, 6, },
        { 9, 9, 4, 4, 2, 11, 7, 10, 9, 8, },
    };

    private static int moduleIdCounter = 1;
    bool readyToSolve = false, hasStarted = false, hasStruck, isFlashing = false;
    int currentStageNum = -1, totalStagesGeneratable, currentInputPos;
    IEnumerator currentFlashingDirection;
    Color firstTextColor;
    List<int> allDirectionIdxs, allRepeatCounts;
    List<int> finalDirectionIdxPresses = new List<int>();
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
        ignoreList = bossModule.GetIgnoredModules(modSelf, ignoreList);
        modSelf.OnActivate += StartBossModule;

        for (int x = 0; x < arrowRenderers.Length; x++)
        {
            arrowRenderers[x].material.color = Color.black;
        }
        textDisplay.text = "";
        firstTextColor = textDisplay.color;
        for (int x = 0; x < arrowButtons.Length; x++)
        {
            int y = x;
            arrowButtons[x].OnInteract += delegate {
                MAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, arrowButtons[y].transform);
                arrowButtons[y].AddInteractionPunch();
                if (!(moduleSolved || isanimating))
                {
                    ProcessInput(y);
                }
                return false;
            };
        }
    }
    void ProcessInput(int directionIdxInput)
    {
        if (!readyToSolve)
        {
            QuickLog(string.Format("Strike! The module is not ready to solve."));
            hasStruck = true;
            modSelf.HandleStrike();
            return;
        }
        if (currentFlashingDirection != null)
        {
            StopCoroutine(currentFlashingDirection);
            isFlashing = false;
            arrowRenderers[0].material = setMats[0];
            arrowRenderers[1].material = setMats[0];
            arrowRenderers[2].material = setMats[0];
            arrowRenderers[3].material = setMats[0];
        }
        if (finalDirectionIdxPresses[currentInputPos] == directionIdxInput)
        {
            currentInputPos++;
            textDisplay.text = "";
            if (currentInputPos >= finalDirectionIdxPresses.Count)
            {
                QuickLog(string.Format("Directions inputted successfully. Module solved."));
                moduleSolved = true;
                StartCoroutine(victory());
            }
        }
        else
        {
            QuickLog(string.Format("Strike! Direction {1} was incorrectly pressed for stage {0}!", currentInputPos, idxToDirections[directionIdxInput]));
            hasStruck = true;
            modSelf.HandleStrike();
            textDisplay.color = Color.red * 0.5f;
            if (currentInputPos > 0)
            {
                currentFlashingDirection = HandleMercy();
                StartCoroutine(currentFlashingDirection);
            }
            else
            {
                StartCoroutine(TypeText(currentInputPos.ToString("00")));
            }
        }
    }

    IEnumerator HandleMercy()
    {
        yield return null;
        int lastCorrectInputPos = currentInputPos;
        yield return TypeText(currentInputPos.ToString("00"));
        while (lastCorrectInputPos == currentInputPos)
        {
            for (int x = 0; x < currentInputPos; x++)
            {
                yield return FlashingGivenDirection(allDirectionIdxs[x], allRepeatCounts[x]);
            }
            yield return new WaitForSeconds(5f);
        }
    }


    IEnumerator FlashingGivenDirection(int directionIdx, int repeatCount = 1)
    {
        for (int x = 0; x < repeatCount; x++)
        {
            switch (directionIdx)
            {
                case 0: // Flash all arrows once per cycle.
                    {
                        arrowRenderers[0].material = setMats[1];
                        arrowRenderers[1].material = setMats[1];
                        arrowRenderers[2].material = setMats[1];
                        arrowRenderers[3].material = setMats[1];
                        yield return new WaitForSeconds(0.5f);
                        arrowRenderers[0].material = setMats[0];
                        arrowRenderers[1].material = setMats[0];
                        arrowRenderers[2].material = setMats[0];
                        arrowRenderers[3].material = setMats[0];
                        yield return new WaitForSeconds(0.5f);
                        break;
                    }
                case 1: // Flash the Up arrow once per cycle.
                    {
                        arrowRenderers[0].material = setMats[1];
                        yield return new WaitForSeconds(0.5f);
                        arrowRenderers[0].material = setMats[0];
                        yield return new WaitForSeconds(0.5f);
                        break;
                    }
                case 2: // Flash the Up and Right arrows once per cycle.
                    {
                        arrowRenderers[0].material = setMats[1];
                        arrowRenderers[1].material = setMats[1];
                        yield return new WaitForSeconds(0.5f);
                        arrowRenderers[0].material = setMats[0];
                        arrowRenderers[1].material = setMats[0];
                        yield return new WaitForSeconds(0.5f);
                        break;
                    }
                case 3: // Flash the Right arrow once per cycle.
                    {
                        arrowRenderers[1].material = setMats[1];
                        yield return new WaitForSeconds(0.5f);
                        arrowRenderers[1].material = setMats[0];
                        yield return new WaitForSeconds(0.5f);
                        break;
                    }
                case 4: // Flash the Down and Right arrows once per cycle.
                    {
                        arrowRenderers[1].material = setMats[1];
                        arrowRenderers[2].material = setMats[1];
                        yield return new WaitForSeconds(0.5f);
                        arrowRenderers[1].material = setMats[0];
                        arrowRenderers[2].material = setMats[0];
                        yield return new WaitForSeconds(0.5f);
                        break;
                    }
                case 5: // Flash the Down arrow once per cycle.
                    {
                        arrowRenderers[2].material = setMats[1];
                        yield return new WaitForSeconds(0.5f);
                        arrowRenderers[2].material = setMats[0];
                        yield return new WaitForSeconds(0.5f);
                        break;
                    }
                case 6: // Flash the Down and Left arrows once per cycle.
                    {
                        arrowRenderers[2].material = setMats[1];
                        arrowRenderers[3].material = setMats[1];
                        yield return new WaitForSeconds(0.5f);
                        arrowRenderers[2].material = setMats[0];
                        arrowRenderers[3].material = setMats[0];
                        yield return new WaitForSeconds(0.5f);
                        break;
                    }
                case 7: // Flash the Left arrow once per cycle.
                    {
                        arrowRenderers[3].material = setMats[1];
                        yield return new WaitForSeconds(0.5f);
                        arrowRenderers[3].material = setMats[0];
                        yield return new WaitForSeconds(0.5f);
                        break;
                    }
                case 8: // Flash the Down and Left arrows once per cycle.
                    {
                        arrowRenderers[0].material = setMats[1];
                        arrowRenderers[3].material = setMats[1];
                        yield return new WaitForSeconds(0.5f);
                        arrowRenderers[0].material = setMats[0];
                        arrowRenderers[3].material = setMats[0];
                        yield return new WaitForSeconds(0.5f);
                        break;
                    }
                default:
                    yield return null;
                    break;
            }
        }
        yield return new WaitForSeconds(1.5f);
        isFlashing = false;
    }
    readonly Dictionary<int, string> intToDirections = new Dictionary<int, string>()
    {
        { 0, "Stay In Place"},
        // One step directions
        { 1, "Move Up"},
        { 2, "Move Up-Right"},
        { 3, "Move Right"},
        { 4, "Move Down-Right"},
        { 5, "Move Down"},
        { 6, "Move Down-Left"},
        { 7, "Move Left"},
        { 8, "Move Up-Left"},
    };
    void StartBossModule()
    {
        totalStagesGeneratable = bombInfo.GetSolvableModuleNames().Count(a => !ignoreList.Contains(a));
        QuickLog(string.Format("Total Extra Stages Generatable: {0}", totalStagesGeneratable));
        allDirectionIdxs = new List<int>();
        allRepeatCounts = new List<int>();

        string serialNo = bombInfo.GetSerialNumber();

        int rowIdx = char.IsDigit(serialNo[2]) ? int.Parse(serialNo[2].ToString()) : serialNo[2] - 'A' + 1;
        int colIdx = char.IsDigit(serialNo[5]) ? int.Parse(serialNo[5].ToString()) : serialNo[5] - 'A' + 1;

        int modifier = bombInfo.GetSerialNumberLetters().Select(a => a - 'A' + 1).Sum() % 5;
        QuickLog(string.Format("Starting Position: Row {0}, Col {1}", rowIdx, colIdx));
        QuickLog(string.Format("Sum of Alphabetical Positions of Serial Number Letters, Modulo 5: {0}", modifier));

        List<int> allFinalValuesVisited = new List<int>();
        // Stage 0's value
        allFinalValuesVisited.Add(digitTable[rowIdx, colIdx]);
        QuickLog(string.Format("Base Number from Stage 0: {0}", digitTable[rowIdx, colIdx]));

        for (int x = 0; x < totalStagesGeneratable; x++)
        {
            QuickLog(string.Format(""));
            QuickLog(string.Format("Stage {0}:", x + 1));
            int curDirectionIdx = uernd.Range(0, 9);
            int repeatCount = curDirectionIdx == 0 ? 1 : uernd.Range(1, 4);
            allRepeatCounts.Add(repeatCount);
            QuickLog(string.Format("Instruction performed on this stage: {0} {1} time(s)", intToDirections[curDirectionIdx], repeatCount));
            allDirectionIdxs.Add(curDirectionIdx);
            for (int t = 0; t < repeatCount; t++)
            {
                switch (curDirectionIdx)
                {
                    case 1: // Moving Up
                        rowIdx = (rowIdx + 9) % 10;
                        break;
                    case 2: // Moving Up-Right
                        rowIdx = (rowIdx + 9) % 10;
                        colIdx = (colIdx + 1) % 10;
                        break;
                    case 3: // Moving Right
                        colIdx = (colIdx + 1) % 10;
                        break;
                    case 4: // Moving Down-Right
                        rowIdx = (rowIdx + 1) % 10;
                        colIdx = (colIdx + 1) % 10;
                        break;
                    case 5: // Moving Down
                        rowIdx = (rowIdx + 1) % 10;
                        break;
                    case 6: // Moving Down-Left
                        rowIdx = (rowIdx + 1) % 10;
                        colIdx = (colIdx + 9) % 10;
                        break;
                    case 7: // Moving Left
                        colIdx = (colIdx + 9) % 10;
                        break;
                    case 8: // Moving Up-Left
                        rowIdx = (rowIdx + 9) % 10;
                        colIdx = (colIdx + 9) % 10;
                        break;
                    case 0:
                    default:
                        break;
                }
            }
            QuickLog(string.Format("Position after instruction: Row {0}, Col {1}", rowIdx, colIdx));
            int curVal = digitTable[rowIdx, colIdx];
            QuickLog(string.Format("Which lands on this number: {0}", curVal));
            curVal += 1 + x;
            allFinalValuesVisited.Add(curVal);
            QuickLog(string.Format("After adding \"n\": {0}", curVal));
            QuickLog(string.Format(""));
        }
        allFinalValuesVisited = allFinalValuesVisited.Select(a => (a + modifier - 1) % 12 + 1).ToList();
        QuickLog(string.Format("Final Values for all stages (including stage 0, within 1 - 12): {0}", allFinalValuesVisited.Join(", ")));
        finalDirectionIdxPresses = allFinalValuesVisited.Select(a => goalIdxPressesByValue[a]).ToList();
        QuickLog(string.Format("Presses required (From stage 0): {0}", finalDirectionIdxPresses.Select(x => idxToDirections[x]).Join(", ")));
        hasStarted = true;
        isanimating = false;
    }
    void HandleColorblindToggle() {
        colorblindArrowDisplay.gameObject.SetActive(colorblindActive);
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
    }
    float delayLeft = 0f;
    void Update()
    {
        for (int x = 0; x < timerDisplayer.Length; x++)
        {
            timerDisplayer[x].SetActive(delayLeft > 0f && !readyToSolve);
            timerDisplayer[x].transform.localScale = new Vector3(delayLeft / 5f, 1, .005f);
        }
        if (hasStarted && !readyToSolve)
        {
            int solveCount = bombInfo.GetSolvedModuleNames().Count(a => !ignoreList.Contains(a));
            if (delayLeft <= 0f)
            {
                if (currentStageNum < solveCount)
                {
                    currentStageNum++;
                    delayLeft = 5f;
                    if (currentStageNum < totalStagesGeneratable)
                        StartCoroutine(TypeText((currentStageNum + 1).ToString("00")));
                    if (currentFlashingDirection != null)
                    {
                        StopCoroutine(currentFlashingDirection);
                        isFlashing = false;
                        arrowRenderers[0].material = setMats[0];
                        arrowRenderers[1].material = setMats[0];
                        arrowRenderers[2].material = setMats[0];
                        arrowRenderers[3].material = setMats[0];
                    }
                }
                if (currentStageNum >= totalStagesGeneratable)
                {
                    readyToSolve = true;
                    textDisplay.text = "";
                    QuickLog(string.Format("The module is now ready to solve."));
                }
                else
                {
                    if (!isFlashing)
                    {
                        isFlashing = true;
                        currentFlashingDirection = FlashingGivenDirection(allDirectionIdxs[currentStageNum], allRepeatCounts[currentStageNum]);
                        StartCoroutine(currentFlashingDirection);
                    }
                }
            }
            else
                delayLeft -= Time.deltaTime;
        }
    }

    protected override void QuickLog(string toLog = "")
    {
        Debug.LogFormat("[Black Arrows #{0}]: {1}", moduleId, toLog);
    }

    private IEnumerator CycleArrowFlashes(float delay)
    {
        int curIdx = uernd.Range(0, 4);
        bool cycleCCW = uernd.value < 0.5f;
        while (moduleSolved)
        {
            for (int x = 0; x < arrowRenderers.Length; x++)
            {
                arrowRenderers[x].material = x == curIdx ? setMats[1] : setMats[0];
            }
            curIdx = cycleCCW ? (curIdx + 3) % 4 : (curIdx + 1) % 4;
            yield return new WaitForSeconds(delay);
        }
        yield return null;
    }

    protected override IEnumerator victory()
    {
        IEnumerator arrowFlasher = CycleArrowFlashes(0.1f);
        isanimating = true;
        StartCoroutine(arrowFlasher);
        for (int i = 0; i < 50; i++)
        {
            int rand1 = uernd.Range(0, 10);
            int rand2 = uernd.Range(0, 10);
            textDisplay.text = rand1 + "" + rand2;
            textDisplay.color = Color.white * i / 50f + firstTextColor * (1f - (i / 50f));
            yield return new WaitForSeconds(0.025f);
        }
        textDisplay.color = Color.white;
        //StopCoroutine(arrowFlasher);
        //arrowFlasher = CycleArrowFlashes(0.05f);
        //StartCoroutine(arrowFlasher);
        for (int i = 0; i < 50; i++)
        {
            int rand2 = uernd.Range(0, 10);
            textDisplay.text = "G" + rand2;
            textDisplay.color = Color.white * (1.0f - (i / 50f)) + firstTextColor * (i / 50f);
            yield return new WaitForSeconds(0.025f);
        }
        textDisplay.color = firstTextColor;
        textDisplay.text = "GG";
        StopCoroutine(arrowFlasher);
        isanimating = false;
        modSelf.HandlePass();

        Color[] lastColors = arrowRenderers.Select(a => a.material.color).ToArray();
        for (int i = 0; i <= 10; i++)
        {
            for (int x = 0; x < arrowRenderers.Length; x++)
            {
                arrowRenderers[x].material.color = lastColors[x] * (1.0f - (i / 10f));
            }
            yield return new WaitForSeconds(0.025f);
        }
        for (int x = 0; x < arrowRenderers.Length; x++)
        {
            arrowRenderers[x].material = setMats[0];
        }
    }
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Press the specified arrow button with \"!{0} up/right/down/left\" Words can be substituted as one letter (Ex. right as r). Multiple directions can be issued in one command by spacing them out. Toggle colorblind mode with \"!{0} colorblind\"";
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
        else
        {
            string[] cmdSets = command.Split();
            List<int> allPresses = new List<int>();
            
            for (int x = 0; x < cmdSets.Length; x++)
            {
                if (Regex.IsMatch(cmdSets[x], @"^\s*u(p)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(0);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*d(own)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(2);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*l(eft)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(3);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*r(ight)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(1);
                }
                else
                {
                    yield return string.Format("sendtochaterror I do not know what direction \"{0}\" is supposed to be.", cmdSets[x]);
                    yield break;
                }
                yield return new WaitForSeconds(0.1f);
                
            }
            if (allPresses.Any())
            {
                hasStruck = false;
                if (!readyToSolve)
                {
                    yield return "sendtochat Is it too early to submit now?";
                    yield return null;
                    arrowButtons[allPresses[0]].OnInteract();

                }
                for (int x = 0; x < allPresses.Count && !hasStruck; x++)
                {
                    yield return null;
                    arrowButtons[allPresses[x]].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    if (moduleSolved) yield return "solve";
                }
            }


        }
        yield break;
    }

    protected override IEnumerator TwitchHandleForcedSolve()
    {
        readyToSolve = true; // Enforce the module to be ready to solve, to bypass inputting before the module is ready to solve.
        currentStageNum = totalStagesGeneratable;
        if (currentFlashingDirection != null)
            StopCoroutine(currentFlashingDirection);
        textDisplay.text = "";
        arrowRenderers[0].material = setMats[0];
        arrowRenderers[1].material = setMats[0];
        arrowRenderers[2].material = setMats[0];
        arrowRenderers[3].material = setMats[0];
        
        for (int x = currentInputPos; x < finalDirectionIdxPresses.Count; x++)
        {
            yield return null;
            arrowButtons[finalDirectionIdxPresses[x]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        while (isanimating) { yield return true; yield return new WaitForSeconds(0.1f); };
    }

}
