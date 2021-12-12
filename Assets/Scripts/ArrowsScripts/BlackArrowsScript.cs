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
    public MeshRenderer[] timerDisplayer;
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
    //BlackArrowsSettings KArrSettings = new BlackArrowsSettings();
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
            /*
            try
            {
                ModConfig<BlackArrowsSettings> modConfig = new ModConfig<BlackArrowsSettings>("BlackArrowsSettings");
                KArrSettings = modConfig.Settings;
                modConfig.Settings = KArrSettings;
            }
            catch
            {
                Debug.LogWarningFormat("<Black Arrows Settings> Settings do not work as intended! Using default settings.");
                KArrSettings = new BlackArrowsSettings();
            }
            */
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
                if (!(moduleSolved || isanimating))
                {
                    MAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, arrowButtons[y].transform);
                    arrowButtons[y].AddInteractionPunch();
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
            QuickLog("Strike! The module is not ready to solve.");
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
                QuickLog("Directions inputted successfully. Module solved.");
                moduleSolved = true;
                StartCoroutine(victory());
            }
        }
        else
        {
            QuickLogFormat("Strike! Direction {1} was incorrectly pressed for stage {0}!", currentInputPos, idxToDirections[directionIdxInput]);
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
            for (int x = 0; x < currentInputPos && lastCorrectInputPos == currentInputPos; x++)
            {
                yield return FlashingGivenDirection(allDirectionIdxs[x], allRepeatCounts[x]);
            }
            yield return new WaitForSeconds(5f);
        }
    }
    IEnumerator HandleFullMercy()
    {
        yield return null;
        int lastCorrectInputPos = currentInputPos;
        while (lastCorrectInputPos == currentInputPos)
        {
            for (int x = 0; x < currentInputPos && lastCorrectInputPos == currentInputPos; x++)
            {
                textDisplay.color = firstTextColor;
                StartCoroutine(TypeText((x + 1).ToString("00")));
                yield return FlashingGivenDirection(allDirectionIdxs[x], allRepeatCounts[x]);
            }
            textDisplay.color = Color.red * 0.5f;
            yield return TypeText(currentInputPos.ToString("00"));
            yield return new WaitForSeconds(5f);
        }
    }
    /*
    IEnumerator GlowGivenDirection(int directionIdx, int repeatCount = 1)
    {
        yield return new WaitForSeconds(0.5f);
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
    */
    IEnumerator FlashingGivenDirection(int directionIdx, int repeatCount = 1)
    {
        yield return new WaitForSeconds(0.5f);
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
        QuickLogFormat("Total Extra Stages Generatable: {0}", totalStagesGeneratable);
        allDirectionIdxs = new List<int>();
        allRepeatCounts = new List<int>();

        string serialNo = bombInfo.GetSerialNumber();

        int rowIdx = char.IsDigit(serialNo[2]) ? int.Parse(serialNo[2].ToString()) : serialNo[2] - 'A' + 1;
        int colIdx = char.IsDigit(serialNo[5]) ? int.Parse(serialNo[5].ToString()) : serialNo[5] - 'A' + 1;
        //QuickLogFormat("Using {0} from serial number letters.", KArrSettings.easyModeBlackArrows ? "no initial offset" : KArrSettings.extendSerialLetterInitialCalcs ? "mod 12 offset" : "mod 5 offset");
        //QuickLog("This is a test build! Do report issues with this.");
        int modifier = bombInfo.GetSerialNumberLetters().Select(a => a - 'A' + 1).Sum() % 12; //(KArrSettings.easyModeBlackArrows ? 1 : KArrSettings.extendSerialLetterInitialCalcs ? 12 : 5);
        QuickLogFormat("Starting Position: Row {0}, Col {1}", rowIdx, colIdx);
        QuickLogFormat("Sum of Alphabetical Positions of Serial Number Letters, Modulo {1}: {0}", modifier,
            12); //KArrSettings.easyModeBlackArrows ? 1 : KArrSettings.extendSerialLetterInitialCalcs ? 12 : 5);

        List<int> allFinalValuesVisited = new List<int>();
        // Stage 0's value
        allFinalValuesVisited.Add(digitTable[rowIdx, colIdx]);
        QuickLogFormat("Base Number from Stage 0: {0}", digitTable[rowIdx, colIdx]);

        for (int x = 0; x < totalStagesGeneratable; x++)
        {
            QuickLog("");
            QuickLogFormat("Stage {0}:", x + 1);
            int curDirectionIdx = uernd.Range(0, 9);
            int repeatCount = curDirectionIdx == 0 ? 1 : uernd.Range(1, 4);
            allRepeatCounts.Add(repeatCount);
            QuickLogFormat("Instruction performed on this stage: {0} {1} time{2}", intToDirections[curDirectionIdx], repeatCount, repeatCount == 1 ? "" : "s");
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
            QuickLogFormat("Position after instruction: Row {0}, Col {1}", rowIdx, colIdx);
            int curVal = digitTable[rowIdx, colIdx];
            QuickLogFormat("Which lands on this number: {0}", curVal);
            curVal += 1 + x;
            allFinalValuesVisited.Add(curVal);
            QuickLogFormat("After adding \"n\": {0}", curVal);
            // Logging for invididual stages
            var indivStageVal = curVal + modifier;
            QuickLogFormat("After adding sum of alphabetical positions in serial number, mod 5: {0}", indivStageVal);
            var indivStageDir = (indivStageVal - 1) % 12 + 1;
            QuickLogFormat("Result after keeping the number within 1 - 12 inclusive: {0} ({1})", indivStageDir, idxToDirections[goalIdxPressesByValue[indivStageDir]]);
            QuickLog("");
        }
        allFinalValuesVisited = allFinalValuesVisited.Select(a => (a + modifier - 1) % 12 + 1).ToList();
        QuickLogFormat("Final Values for all stages (including stage 0, after adding sum of alphabetical positions in serial no. mod 5, kept within 1 - 12 inclusive): {0}", allFinalValuesVisited.Join(", "));
        finalDirectionIdxPresses = allFinalValuesVisited.Select(a => goalIdxPressesByValue[a]).ToList();
        QuickLogFormat("Presses required (From stage 0): {0}", finalDirectionIdxPresses.Select(x => idxToDirections[x]).Join(", "));
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
    float delayLeft = 0f, maxTimeAllocatable = 10f, breatheDelay = 6f;
    void Update()
    {
        for (int x = 0; x < timerDisplayer.Length; x++)
        {
            timerDisplayer[x].gameObject.SetActive(delayLeft > 0f && !readyToSolve);
            timerDisplayer[x].transform.localScale = new Vector3(delayLeft / maxTimeAllocatable, 1, .005f);
        }
        if (hasStarted && !readyToSolve)
        {
            if (colorblindActive)
            {
                breatheDelay -= Time.deltaTime;
                if (breatheDelay < 0) breatheDelay = 6f;
                textDisplay.color = Color.white * (0.5f - Mathf.Abs((breatheDelay / 6f) - 0.5f)) * 2 + firstTextColor * Mathf.Abs((breatheDelay / 6f) - 0.5f) * 2;
                for (int x = 0; x < timerDisplayer.Length; x++)
                {
                    timerDisplayer[x].material.color = Color.white * (0.5f - Mathf.Abs((breatheDelay / 6f) - 0.5f)) * 2 + firstTextColor * Mathf.Abs((breatheDelay / 6f) - 0.5f) * 2;
                }
            }
            else
            {
                textDisplay.color = firstTextColor;
                breatheDelay = 6f;
            }
            int solveCount = bombInfo.GetSolvedModuleNames().Count(a => !ignoreList.Contains(a));
            if (delayLeft <= 0f)
            {
                if (currentStageNum < solveCount)
                {
                    currentStageNum++;
                    delayLeft = maxTimeAllocatable;
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
                    QuickLog("The module is now ready to solve.");
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
            {
                delayLeft -= Time.deltaTime;
                if (!isFlashing)
                {
                    isFlashing = true;
                    currentFlashingDirection = FlashingGivenDirection(allDirectionIdxs[currentStageNum], allRepeatCounts[currentStageNum]);
                    StartCoroutine(currentFlashingDirection);
                }
            }
        }
    }

    protected override void QuickLog(string toLog = "")
    {
        Debug.LogFormat("[Black Arrows #{0}]: {1}", moduleId, toLog);
    }
    protected override void QuickLogFormat(string toLog = "", params object[] args)
    {
        QuickLog(string.Format(toLog, args));
    }

    private IEnumerator BreatheArrowFlashes(float delay)
    {
        //int curIdx = uernd.Range(0, 4);
        //bool cycleCCW = uernd.value < 0.5f;
        for (int x = 0; x < arrowRenderers.Length; x++)
        {
            arrowRenderers[x].material = setMats[1];
            arrowRenderers[x].material.color = Color.black;
        }
        while (isanimating)
        {
            for (int x = 0; x < arrowRenderers.Length; x++)
            {
                for (float y = 0; y <= 1f; y += Time.deltaTime / delay * 4f)
                {
                    yield return null;
                    arrowRenderers[x].material.color = Color.white * y;
                }
                arrowRenderers[x].material.color = Color.white;
            }
            for (int x = 0; x < arrowRenderers.Length; x++)
            {
                for (float y = 0; y <= 1f ; y += Time.deltaTime / delay * 4f)
                {
                    yield return null;
                    arrowRenderers[x].material.color = Color.white * (1f - y);
                }
                arrowRenderers[x].material.color = Color.black;
            }
        }
        yield return null;
        for (int x = 0; x < arrowRenderers.Length; x++)
        {
            arrowRenderers[x].material = setMats[0];
        }
    }

    protected override IEnumerator victory()
    {
        IEnumerator arrowFlasher = BreatheArrowFlashes(2f);
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
            /*
            for (int x = 0; x < arrowRenderers.Length; x++)
            {
                arrowRenderers[x].material.color = Color.white * (1f - i / 50f);
            }
            */
            yield return new WaitForSeconds(0.025f);
        }
        textDisplay.color = firstTextColor;
        textDisplay.text = "GG";
        //StopCoroutine(arrowFlasher);
        isanimating = false;
        modSelf.HandlePass();
        /*
        Color[] lastColors = arrowRenderers.Select(a => a.material.color).ToArray();
        for (int i = 0; i <= 10; i++)
        {
            for (int x = 0; x < arrowRenderers.Length; x++)
            {
                arrowRenderers[x].material.color = lastColors[x] * (1.0f - (i / 10f));
            }
            yield return new WaitForSeconds(0.025f);
        }
        */
        /*
        for (int x = 0; x < arrowRenderers.Length; x++)
        {
            arrowRenderers[x].material = setMats[0];
        }
        */
    }
    /*
    public class BlackArrowsSettings
    {
        public bool easyModeBlackArrows = false;
        public bool extendSerialLetterInitialCalcs = true;
    }
    */
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Press the specified arrow button with \"!{0} up/right/down/left\" Words can be substituted as one letter (Ex. right as r). "+
        "Multiple directions can be issued in one command by spacing them out or as a 1 word when abbrevivabted, I.E. \"!{0} udlrrrll\". Alternatively, when abbreviated, you may space out the presses in the command. I.E. \"!{0} lluur ddlr urd\" Toggle colorblind mode with \"!{0} colorblind\"";
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
                if (!readyToSolve)
                {
                    yield return "sendtochat Is it too early to submit now?";
                    yield return null;
                    arrowButtons[allPresses[0]].OnInteract();

                }
                for (int x = 0; x < allPresses.Count && !hasStruck; x++)
                {
                    yield return null;
                    if (allPresses[x] != finalDirectionIdxPresses[currentInputPos] && allPresses.Count > 1)
                        yield return string.Format("strikemessage by incorrectly pressing {0} after {1} press(es) in the TP command!", idxToDirections[allPresses[x]], x + 1);
                    arrowButtons[allPresses[x]].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    if (moduleSolved) yield return "solve";
                }
            }
        }
        else
        {
            string[] cmdSets = command.Trim().Split();
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
                    if (allPresses[x] != finalDirectionIdxPresses[currentInputPos] && allPresses.Count > 1)
                        yield return string.Format("strikemessage by incorrectly pressing {0} after {1} press(es) in the TP command!", idxToDirections[allPresses[x]], x + 1);
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
        // Enforce the module to be ready to solve, to bypass inputting before the module is ready to solve.
        readyToSolve = true; 
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
