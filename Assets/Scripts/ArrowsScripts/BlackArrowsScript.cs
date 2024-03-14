using KModkit;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KeepCoding;
using uernd = UnityEngine.Random;

public class BlackArrowsScript : BaseArrowsScript {

    public MeshRenderer[] arrowRenderers;
    public KMBossModule bossModule;
    public KMBombInfo bombInfo;
    public MeshRenderer[] timerDisplayer;
    public Material[] setMats;

    string[] ignoreList = new[] {
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

    readonly Dictionary<int, string> idxToDirections = new Dictionary<int, string>()
    {
        { 0, "Up"},
        { 1, "Right"},
        { 2, "Down"},
        { 3, "Left"},
    };

    private static int moduleIdCounter = 1;
    bool readyToSolve = false, hasStarted = false, hasStruck, isFlashing = false, requestForceSolve, bossActive, enableLegacyBlackArrows;
    int currentStageNum = -1, totalStagesGeneratable, currentInputPos;
    IEnumerator currentFlashingDirection;
    Color firstTextColor;
    List<int> allDirectionIdxs, allRepeatCounts, idxRelevantDirection;
    List<List<int>> idxDirectionFlashes;
    List<int> finalDirectionIdxPresses = new List<int>();
    BlackArrowsSettings KArrSettings = new BlackArrowsSettings();
    [SerializeField]
    bool debugBossMode = false;
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
            
            try
            {
                ModConfig<BlackArrowsSettings> modConfig = new ModConfig<BlackArrowsSettings>("BlackArrowsSettings");
                KArrSettings = modConfig.Settings;
                modConfig.Settings = KArrSettings;
                bossActive = !KArrSettings.nonBossModeBlackArrows;
                enableLegacyBlackArrows = KArrSettings.legacyBlackArrows;
            }
            catch
            {
                Debug.LogWarningFormat("<Black Arrows Settings> Settings do not work as intended! Using default settings.");
                bossActive = true;
            }
            
        }
    }

    // Use this for initialization
    void Start() {
        moduleId = moduleIdCounter++;
        //enableLegacyBlackArrows = true;
        var obtainedIgnoreList = bossModule.GetIgnoredModules(modSelf);
        if (obtainedIgnoreList != null && obtainedIgnoreList.Any())
            ignoreList = obtainedIgnoreList;
        else
        {
            QuickLogDebug("Using default ignore list! This will cause issues when running this module on bombs.");
            //QuickLog("This module uses Boss Module Manager to enforce boss mode on this module. To prevent softlocks, this module will have altered functionality under normal circumstances.");
            //bossActive = false;
        }
        TryOverrideSettings();
        modSelf.OnActivate += () => {
            if (enableLegacyBlackArrows)
                StartBossModuleLegacy();
            else
                StartBossModule();
        };

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
            else if (!bossActive)
            {
                currentFlashingDirection = HandleNonBossArrowsReveal();
                StartCoroutine(currentFlashingDirection);
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
    IEnumerator HandleNonBossArrowsReveal()
    {
        int lastCorrectInputPos = currentInputPos;
        yield return TypeText(currentInputPos.ToString("00"));
        while (lastCorrectInputPos == currentInputPos)
        {
            if (enableLegacyBlackArrows)
            {
                var currentStageRepeat = allRepeatCounts[currentInputPos - 1];
                var currentStageDirection = allDirectionIdxs[currentInputPos - 1];
                yield return FlashingGivenDirection(currentStageDirection, currentStageRepeat);
            }
            else
                yield return HandleFlashCurStage(lastCorrectInputPos);
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
                yield return enableLegacyBlackArrows ? FlashingGivenDirection(allDirectionIdxs[x], allRepeatCounts[x]) : HandleFlashCurStage(x);
            }
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
    IEnumerator BreatheGivenDirection(int directionIdx, int repeatCount = 1, float endDelay = 1.5f, float startDelay = 0.5f, bool allowExtras = false)
    {
        if (startDelay > 0)
            yield return new WaitForSeconds(startDelay);
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
                        for (float t = 0; t < 1f; t += Time.deltaTime)
                        {
                            var curEase = Easing.InOutSine(t, 0f, 1f, 0.5f);
                            arrowRenderers[0].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            arrowRenderers[1].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            arrowRenderers[2].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            arrowRenderers[3].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            yield return null;
                        }
                        arrowRenderers[0].material = setMats[0];
                        arrowRenderers[1].material = setMats[0];
                        arrowRenderers[2].material = setMats[0];
                        arrowRenderers[3].material = setMats[0];
                        break;
                    }
                case 1: // Flash the Up arrow once per cycle.
                    {
                        arrowRenderers[0].material = setMats[1];
                        for (float t = 0; t < 1f; t += Time.deltaTime)
                        {
                            var curEase = Easing.InOutSine(t, 0f, 1f, 0.5f);
                            arrowRenderers[0].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            yield return null;
                        }
                        arrowRenderers[0].material = setMats[0];
                        break;
                    }
                case 2: // Flash the Up and Right arrows once per cycle.
                    {
                        arrowRenderers[0].material = setMats[1];
                        arrowRenderers[1].material = setMats[1];
                        for (float t = 0; t < 1f; t += Time.deltaTime)
                        {
                            var curEase = Easing.InOutSine(t, 0f, 1f, 0.5f);
                            arrowRenderers[0].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            arrowRenderers[1].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            yield return null;
                        }
                        arrowRenderers[0].material = setMats[0];
                        arrowRenderers[1].material = setMats[0];
                        break;
                    }
                case 3: // Flash the Right arrow once per cycle.
                    {
                        arrowRenderers[1].material = setMats[1];
                        for (float t = 0; t < 1f; t += Time.deltaTime)
                        {
                            var curEase = Easing.InOutSine(t, 0f, 1f, 0.5f);
                            arrowRenderers[1].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            yield return null;
                        }
                        arrowRenderers[1].material = setMats[0];
                        break;
                    }
                case 4: // Flash the Down and Right arrows once per cycle.
                    {
                        arrowRenderers[1].material = setMats[1];
                        arrowRenderers[2].material = setMats[1];
                        for (float t = 0; t < 1f; t += Time.deltaTime)
                        {
                            var curEase = Easing.InOutSine(t, 0f, 1f, 0.5f);
                            arrowRenderers[1].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            arrowRenderers[2].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            yield return null;
                        }
                        arrowRenderers[1].material = setMats[0];
                        arrowRenderers[2].material = setMats[0];
                        break;
                    }
                case 5: // Flash the Down arrow once per cycle.
                    {
                        arrowRenderers[2].material = setMats[1];
                        for (float t = 0; t < 1f; t += Time.deltaTime)
                        {
                            var curEase = Easing.InOutSine(t, 0f, 1f, 0.5f);
                            arrowRenderers[2].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            yield return null;
                        }
                        arrowRenderers[2].material = setMats[0];
                        break;
                    }
                case 6: // Flash the Down and Left arrows once per cycle.
                    {
                        arrowRenderers[2].material = setMats[1];
                        arrowRenderers[3].material = setMats[1];
                        for (float t = 0; t < 1f; t += Time.deltaTime)
                        {
                            var curEase = Easing.InOutSine(t, 0f, 1f, 0.5f);
                            arrowRenderers[2].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            arrowRenderers[3].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            yield return null;
                        }
                        arrowRenderers[2].material = setMats[0];
                        arrowRenderers[3].material = setMats[0];
                        break;
                    }
                case 7: // Flash the Left arrow once per cycle.
                    {
                        arrowRenderers[3].material = setMats[1];
                        for (float t = 0; t < 1f; t += Time.deltaTime)
                        {
                            var curEase = Easing.InOutSine(t, 0f, 1f, 0.5f);
                            arrowRenderers[3].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            yield return null;
                        }
                        arrowRenderers[3].material = setMats[0];
                        break;
                    }
                case 8: // Flash the Up and Left arrows once per cycle.
                    {
                        arrowRenderers[0].material = setMats[1];
                        arrowRenderers[3].material = setMats[1];
                        for (float t = 0; t < 1f; t += Time.deltaTime)
                        {
                            var curEase = Easing.InOutSine(t, 0f, 1f, 0.5f);
                            arrowRenderers[0].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            arrowRenderers[3].material.color = Color.Lerp(Color.black, Color.white, curEase);
                            yield return null;
                        }
                        arrowRenderers[0].material = setMats[0];
                        arrowRenderers[3].material = setMats[0];
                        break;
                    }
                default:
                    yield return null;
                    break;
            }
        }
        if (endDelay > 0f)
            yield return new WaitForSeconds(endDelay);
        isFlashing &= allowExtras;
    }
    IEnumerator HandleFlashCurStage(int curStageIdx)
    {
        if (curStageIdx < 0 || curStageIdx >= idxDirectionFlashes.Count) yield break;
        var curStageDirs = idxDirectionFlashes[curStageIdx];
        //Debug.Log(idxDirectionFlashes[curStageIdx].Join());
        for (var x = 0; x < curStageDirs.Count; x++)
            yield return x == idxRelevantDirection[curStageIdx] ?
                BreatheGivenDirection(curStageDirs[x], 1, x + 1 >= curStageDirs.Count ? 2.5f : 0.5f, 0f, x + 1 < curStageDirs.Count) :
                FlashingGivenDirection(curStageDirs[x], 1, x + 1 >= curStageDirs.Count ? 2.5f : 0f, 0f, x + 1 < curStageDirs.Count);
    }

    IEnumerator FlashingGivenDirection(int directionIdx, int repeatCount = 1, float extraDelay = 1.5f, float startDelay = 0.5f, bool allowExtras = false)
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);
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
                case 8: // Flash the Up and Left arrows once per cycle.
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
        if (extraDelay > 0f)
            yield return new WaitForSeconds(extraDelay);
        isFlashing &= allowExtras;
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
    #region ReworkKArrows
    void StartBossModule()
    {
        QuickLogFormat("This is the reworked version of Black Arrows. Some calculation procedures have been altered to make this less boring.");
        totalStagesGeneratable = bombInfo.GetSolvableModuleNames().Count(a => !ignoreList.Contains(a));
        if (bossActive)
            QuickLogFormat("Total Extra Stages Generatable: {0}", totalStagesGeneratable);
        else
            QuickLogFormat("Boss mode is inactive. Generating this many extra stages: {0}", totalStagesGeneratable);
        string serialNo = bombInfo.GetSerialNumber();
        int rowIdx = char.IsDigit(serialNo[2]) ? int.Parse(serialNo[2].ToString()) : serialNo[2] - 'A' + 1;
        int colIdx = char.IsDigit(serialNo[5]) ? int.Parse(serialNo[5].ToString()) : serialNo[5] - 'A' + 1;
        int modifier = bombInfo.GetSerialNumberLetters().Select(a => a - 'A' + 1).Sum() % 8;
        idxDirectionFlashes = new List<List<int>>();
        idxRelevantDirection = new List<int>();
        QuickLogFormat("Starting Position: Row {0}, Col {1}", rowIdx, colIdx);
        QuickLogFormat("Sum of Alphabetical Positions of Serial Number Letters, Modulo {1}: {0}", modifier,
            8);
        var gridA = new int[,] {
            { 0, 3, 0, 1, 2, 3, 0, 2, 0, 2 },
            { 2, 2, 3, 0, 1, 2, 3, 2, 3, 3 },
            { 3, 2, 0, 0, 2, 0, 0, 3, 1, 1 },
            { 2, 0, 3, 1, 1, 3, 3, 0, 3, 0 },
            { 1, 1, 3, 2, 3, 1, 1, 3, 2, 2 },
            { 3, 0, 0, 1, 3, 0, 2, 1, 2, 1 },
            { 3, 0, 1, 0, 2, 3, 1, 1, 2, 3 },
            { 2, 2, 1, 3, 0, 0, 2, 0, 3, 2 },
            { 0, 1, 1, 0, 3, 2, 1, 1, 3, 1 },
            { 2, 0, 1, 0, 0, 3, 2, 1, 2, 1 },
        };
        var gridB = new int[,] {
            { 1, 2, 2, 1, 2, 0, 0, 3, 3, 0 },
            { 3, 0, 0, 2, 2, 1, 0, 0, 3, 3 },
            { 1, 0, 0, 3, 2, 0, 1, 1, 3, 1 },
            { 2, 1, 3, 2, 3, 2, 0, 1, 3, 0 },
            { 2, 3, 3, 2, 1, 2, 1, 1, 1, 2 },
            { 0, 2, 1, 0, 1, 3, 3, 1, 2, 3 },
            { 1, 0, 3, 2, 3, 2, 3, 2, 2, 0 },
            { 0, 2, 0, 0, 3, 0, 1, 3, 1, 0 },
            { 0, 3, 2, 1, 3, 3, 2, 0, 1, 0 },
            { 2, 1, 1, 3, 1, 2, 2, 0, 3, 1 },
        };
        var roygbpArrowIds = new[] { "redArrowsModule", "greenArrowsModule", "yellowArrowsModule", "blueArrowsModule", "orangeArrowsModule", "purpleArrowsModule" };
        var con11Ids = new[] { "flashingArrowsModule", "colouredArrowsModule", "doubleArrows" };
        var allDirsFromConds = new int[][] {
            new[] { 1, 2, 0, 3 },
            new[] { 3, 0, 1, 2 },
            new[] { 0, 2, 3, 1 },
            new[] { 3, 1, 2, 0 },
            new[] { 1, 0, 3, 2 },
            new[] { 2, 3, 1, 0 },
            new[] { 0, 1, 2, 3 },
            new[] { 2, 0, 3, 1 },
            new[] { 1, 3, 2, 0 },
            new[] { 3, 2, 0, 1 },
            new[] { 0, 3, 1, 2 },
            new[] { 2, 1, 0, 3 },
        };
        var conditionsMetAll = new bool[] {
            bombInfo.GetSolvableModuleIDs().Any(a => roygbpArrowIds.Contains(a)), // Red, Orange, Yellow, Green, Blue, or Purple Arrows
            bombInfo.IsIndicatorOff(Indicator.TRN),
            bombInfo.IsIndicatorOn(Indicator.BOB),
            bombInfo.GetSerialNumberLetters().Any(a => "AEIOU".Contains(a)),
            bombInfo.GetPortPlates().Any(a => a.Length == 2),
            bombInfo.GetOffIndicators().Count() >= 2,
            true,
            bombInfo.GetBatteryCount() >= 4,
            bombInfo.GetOnIndicators().Count() >= 2,
            bombInfo.IsIndicatorOn(Indicator.CLR),
            bombInfo.GetSolvableModuleIDs().Any(a => con11Ids.Contains(a)), // Double, Flashing, or Coloured Arrows
            bombInfo.IsIndicatorOff(Indicator.FRQ),
        };
        var idxConditionsApplied = Enumerable.Range(0, 12).Where(a => conditionsMetAll[a]).ToArray();
        QuickLogFormat("Conditions Applied: [{0}]", idxConditionsApplied.Select(a => a + 1).Join(", "));
        List<int> allFinalValuesVisited = new List<int>();
        if (modifier >= 4)
        {
            QuickLog("Using Grid B throughout the entire module.");
            
            // Stage 0's value
            allFinalValuesVisited.Add(gridB[rowIdx, colIdx]);
            QuickLogFormat("Base Number from Stage 0: {0}", gridB[rowIdx, colIdx]);

            for (int x = 0; x < totalStagesGeneratable; x++)
            {
                QuickLog("");
                QuickLogFormat("----------------------- Stage {0} -----------------------", x + 1);
                
                int repeatCount = uernd.Range(1, 6);
                var idxUseStage = x + 1 >= totalStagesGeneratable ? repeatCount - 1 : uernd.Range(0, repeatCount);
                var curValObtained = -1;
                var curDirsAllCurStage = new List<int>();
                for (int t = 0; t < repeatCount; t++)
                {
                    int curDirectionIdx = new[] { 2, 3, 4, 6, 7, 8, 0 }.PickRandom();
                    switch (curDirectionIdx)
                    {
                        case 1: // Moving Up
                            rowIdx = (rowIdx + 8) % 10;
                            colIdx = (colIdx + 1) % 10;
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
                            break;
                        case 5: // Moving Down
                            rowIdx = (rowIdx + 2) % 10;
                            colIdx = (colIdx + 9) % 10;
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
                            break;
                        case 0:
                        default:
                            break;
                    }
                    curDirsAllCurStage.Add(curDirectionIdx);
                    if (t == idxUseStage)
                        curValObtained = gridB[rowIdx, colIdx];
                }
                idxDirectionFlashes.Add(curDirsAllCurStage);
                idxRelevantDirection.Add(idxUseStage);
                QuickLogFormat("Instructions performed on this stage: {0}", curDirsAllCurStage.Select(curDirectionIdx => intToDirections[curDirectionIdx]).Join(", "));
                QuickLogFormat("Position after instruction: Row {0}, Col {1}", rowIdx, colIdx);
                int curVal = curValObtained;
                QuickLogFormat("Number obtained from this stage after moving on direction #{1}: {0}", curVal, idxUseStage + 1);
                curVal += 1 + x;
                allFinalValuesVisited.Add(curVal);
                QuickLogFormat("After adding \"n\": {0}", curVal);
                // Logging for invididual stages
                var indivStageVal = curVal + modifier % 4;
                QuickLogFormat("After adding sum of alphabetical positions in serial number, mod 4: {0}", indivStageVal);
                var indivStageDir = indivStageVal % 4;
                QuickLogFormat("Result after keeping the number within 0 - 3 inclusive: {0} ({1})", indivStageDir, idxToDirections[allDirsFromConds[idxConditionsApplied.ElementAt(x % idxConditionsApplied.Count())][indivStageDir]]);
                QuickLog("--------------------------------------------------------");
            }
        }
        else
        {
            QuickLog("Using Grid A throughout the entire module.");
            // Stage 0's value
            var stage0Val = gridA[rowIdx, colIdx];
            allFinalValuesVisited.Add(stage0Val);
            QuickLogFormat("Base Number from Stage 0: {0}", gridA[rowIdx, colIdx]);
            var stage0AfterOffset = stage0Val + modifier % 4;
            QuickLogFormat("After adding sum of alphabetical positions in serial number, mod 4: {0}", stage0AfterOffset);
            var stage0Dir = stage0Val % 4;
            QuickLogFormat("Result after keeping the number within 0 - 3 inclusive: {0} ({1})", stage0Dir, idxToDirections[allDirsFromConds[idxConditionsApplied.Last()][stage0Dir]]);
            for (int x = 0; x < totalStagesGeneratable; x++)
            {
                QuickLog("");
                QuickLogFormat("----------------------- Stage {0} -----------------------", x + 1);

                int repeatCount = uernd.Range(1, 6);
                var idxUseStage = x + 1 >= totalStagesGeneratable ? repeatCount - 1 : uernd.Range(0, repeatCount);
                var curValObtained = -1;
                var curDirsAllCurStage = new List<int>();
                for (int t = 0; t < repeatCount; t++)
                {
                    int curDirectionIdx = new[] { 2, 3, 4, 6, 7, 8, 0 }.PickRandom();
                    switch (curDirectionIdx)
                    {
                        case 1: // Moving Up
                            rowIdx = (rowIdx + 8) % 10;
                            colIdx = (colIdx + 9) % 10;
                            break;
                        case 2: // Moving Up-Right
                            rowIdx = (rowIdx + 9) % 10;
                            break;
                        case 3: // Moving Right
                            colIdx = (colIdx + 1) % 10;
                            break;
                        case 4: // Moving Down-Right
                            rowIdx = (rowIdx + 1) % 10;
                            colIdx = (colIdx + 1) % 10;
                            break;
                        case 5: // Moving Down
                            rowIdx = (rowIdx + 2) % 10;
                            colIdx = (colIdx + 1) % 10;
                            break;
                        case 6: // Moving Down-Left
                            rowIdx = (rowIdx + 1) % 10;
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
                    curDirsAllCurStage.Add(curDirectionIdx);
                    if (t == idxUseStage)
                        curValObtained = gridA[rowIdx, colIdx];
                }
                idxDirectionFlashes.Add(curDirsAllCurStage);
                idxRelevantDirection.Add(idxUseStage);
                QuickLogFormat("Instructions performed on this stage: {0}", curDirsAllCurStage.Select(curDirectionIdx => intToDirections[curDirectionIdx]).Join(", "));
                QuickLogFormat("Position after instruction: Row {0}, Col {1}", rowIdx, colIdx);
                int curVal = curValObtained;
                QuickLogFormat("Number obtained from this stage after moving on direction #{1}: {0}", curVal, idxUseStage + 1);
                curVal += 1 + x;
                allFinalValuesVisited.Add(curVal);
                QuickLogFormat("After adding \"n\": {0}", curVal);
                // Logging for invididual stages
                var indivStageVal = curVal + modifier % 4;
                QuickLogFormat("After adding sum of alphabetical positions in serial number, mod 4: {0}", indivStageVal);
                var indivStageDir = indivStageVal % 4;
                QuickLogFormat("Result after keeping the number within 0 - 3 inclusive: {0} ({1})", indivStageDir, idxToDirections[allDirsFromConds[idxConditionsApplied.ElementAt(x % idxConditionsApplied.Count())][indivStageDir]]);
                QuickLog("--------------------------------------------------------");
            }
        }
        var finalValuesAfterOffset = allFinalValuesVisited.Select(a => (a + modifier) % 4).ToList();
        var _1BackShiftedIdxRulesApplied = idxConditionsApplied.Skip(idxConditionsApplied.Length - 1).Concat(idxConditionsApplied.Take(idxConditionsApplied.Length - 1));
        QuickLogDebugFormat("{0}", _1BackShiftedIdxRulesApplied.Join());
        QuickLogDebugFormat("{0}", idxConditionsApplied.Join());
        QuickLogFormat("All values obtained from all stages, including stage 0, modulo 4: {0}", finalValuesAfterOffset.Join(", "));
        for (var x = 0; x < finalValuesAfterOffset.Count; x++)
        {
            var curIdxTrueRule = _1BackShiftedIdxRulesApplied.ElementAt(x % _1BackShiftedIdxRulesApplied.Count());
            finalDirectionIdxPresses.Add(allDirsFromConds[curIdxTrueRule][finalValuesAfterOffset[x] % 4]);
        }
        QuickLogFormat("Expected directions to press, from stage 0: {0}", finalDirectionIdxPresses.Select(a => idxToDirections[a]).Join(", "));
        QuickLog("------------------------- User Interactions -------------------------------");
        hasStarted = true;
        isanimating = false;
        readyToSolve = !bossActive;
    }
    #endregion
    #region LegacyKArrows
    void StartBossModuleLegacy()
    {
        totalStagesGeneratable = bombInfo.GetSolvableModuleNames().Count(a => !ignoreList.Contains(a));
        if (bossActive)
            QuickLogFormat("Total Extra Stages Generatable: {0}", totalStagesGeneratable);
        else
            QuickLogFormat("Boss mode is inactive. Generating this many extra stages: {0}", totalStagesGeneratable);
        allDirectionIdxs = new List<int>();
        allRepeatCounts = new List<int>();

        string serialNo = bombInfo.GetSerialNumber();

        int rowIdx = char.IsDigit(serialNo[2]) ? int.Parse(serialNo[2].ToString()) : serialNo[2] - 'A' + 1;
        int colIdx = char.IsDigit(serialNo[5]) ? int.Parse(serialNo[5].ToString()) : serialNo[5] - 'A' + 1;
        int modifier = bombInfo.GetSerialNumberLetters().Select(a => a - 'A' + 1).Sum() % 12; //(KArrSettings.easyModeBlackArrows ? 1 : KArrSettings.extendSerialLetterInitialCalcs ? 12 : 5);
        QuickLogFormat("Starting Position: Row {0}, Col {1}", rowIdx, colIdx);
        QuickLogFormat("Sum of Alphabetical Positions of Serial Number Letters, Modulo {1}: {0}", modifier,
            12);
        var digitTable = new int[,] {
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
        Dictionary<int, int> goalIdxPressesByValue = new Dictionary<int, int>() {
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
        List<int> allFinalValuesVisited = new List<int>();
        // Stage 0's value
        allFinalValuesVisited.Add(digitTable[rowIdx, colIdx]);
        QuickLogFormat("Base Number from Stage 0: {0}", digitTable[rowIdx, colIdx]);

        for (int x = 0; x < totalStagesGeneratable; x++)
        {
            QuickLog("");
            QuickLogFormat("----------------------- Stage {0} -----------------------", x + 1);
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
            QuickLogFormat("After adding sum of alphabetical positions in serial number, mod 12: {0}", indivStageVal);
            var indivStageDir = (indivStageVal - 1) % 12 + 1;
            QuickLogFormat("Result after keeping the number within 1 - 12 inclusive: {0} ({1})", indivStageDir, idxToDirections[goalIdxPressesByValue[indivStageDir]]);
            QuickLog("--------------------------------------------------------");
        }
        allFinalValuesVisited = allFinalValuesVisited.Select(a => (a + modifier - 1) % 12 + 1).ToList();
        QuickLogFormat("Final Values for all stages (including stage 0, after adding sum of alphabetical positions in serial no. mod 12, kept within 1 - 12 inclusive): {0}", allFinalValuesVisited.Join(", "));
        finalDirectionIdxPresses = allFinalValuesVisited.Select(a => goalIdxPressesByValue[a]).ToList();
        QuickLogFormat("Presses required (From stage 0): {0}", finalDirectionIdxPresses.Select(x => idxToDirections[x]).Join(", "));
        QuickLog("--------------------------------------------------------");
        hasStarted = true;
        isanimating = false;
        readyToSolve = !bossActive;
    }
    #endregion
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
                        currentFlashingDirection = enableLegacyBlackArrows ? FlashingGivenDirection(allDirectionIdxs[currentStageNum], allRepeatCounts[currentStageNum]) : HandleFlashCurStage(currentStageNum);
                        StartCoroutine(currentFlashingDirection);
                    }
                }
            }
            else
            {
                delayLeft -= Time.deltaTime * (requestForceSolve ? 10 : 1);
                if (!isFlashing)
                {
                    isFlashing = true;
                    currentFlashingDirection = enableLegacyBlackArrows ? FlashingGivenDirection(allDirectionIdxs[currentStageNum], allRepeatCounts[currentStageNum]) : HandleFlashCurStage(currentStageNum);
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
    protected override void QuickLogDebugFormat(string toLog = "", params object[] args)
    {
        QuickLogDebug(string.Format(toLog, args));
    }
    protected override void QuickLogDebug(string toLog = "")
    {
        Debug.LogFormat("<Black Arrows #{0}>: {1}", moduleId, toLog);
    }

    private IEnumerator BreatheSequenceArrowFlashes(float delay, int idxStartGlow = 0)
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
                    arrowRenderers[(x + idxStartGlow) % arrowRenderers.Length].material.color = Color.white * y;
                }
                arrowRenderers[(x + idxStartGlow) % arrowRenderers.Length].material.color = Color.white;
            }
            for (int x = 0; x < arrowRenderers.Length; x++)
            {
                for (float y = 0; y <= 1f ; y += Time.deltaTime / delay * 4f)
                {
                    yield return null;
                    arrowRenderers[(x + idxStartGlow) % arrowRenderers.Length].material.color = Color.white * (1f - y);
                }
                arrowRenderers[(x + idxStartGlow) % arrowRenderers.Length].material.color = Color.black;
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
        IEnumerator arrowFlasher = BreatheSequenceArrowFlashes(2f, finalDirectionIdxPresses.Last());
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
        for (int i = 0; i < 50; i++)
        {
            int rand2 = uernd.Range(0, 10);
            textDisplay.text = "G" + rand2;
            textDisplay.color = Color.white * (1.0f - (i / 50f)) + firstTextColor * (i / 50f);
            yield return new WaitForSeconds(0.025f);
        }
        textDisplay.color = firstTextColor;
        textDisplay.text = "GG";
        isanimating = false;
        modSelf.HandlePass();
    }
    
    void TryOverrideSettings()
    {
        var missionID = Game.Mission.ID ?? "freeplay";
        //QuickLogDebugFormat("Detected mission ID: {0}", missionID);
        switch (missionID)
        {
            case "freeplay":
            case "custom":
                QuickLog("Mission being ran on a customized freeplay. Not allowed to override settings.");
                return;
            case "mod_madnessMissionPack_arrowsMadness":
                QuickLog("Specific mission detected: Arrow Madness from Darksly's Madness Pack.");
                bossActive = true;
                return;
        }
        var description = Game.Mission.Description ?? "";
        var regexOverrideBoss = Regex.Match(description, @"[BlackArrows]\s(true|false)(\s(true|false))?");
        var regexOverrideBossFlow = Regex.Match(description, @"((non)?boss\s)?(legacy|reworked)\sBlack\sArrows", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (regexOverrideBoss.Success)
        {
            var valueObtained = regexOverrideBoss.Value;
            var splittedVals = valueObtained.Split().Skip(1);
            var overrideState = false;
            if (bool.TryParse(splittedVals.Last(), out overrideState))
            {
                QuickLogDebugFormat("Settings overriden via descriptions.");
                bossActive = overrideState;
            }
        }
        else if (regexOverrideBossFlow.Success)
        {
            var valueObtained = regexOverrideBossFlow.Value;
            var splittedVals = valueObtained.Split();
            var filteredVals = splittedVals.Take(splittedVals.Length - 2);
            QuickLogDebugFormat("Settings overriden via descriptions. Maybe that flows?");
            if (filteredVals.Count() == 2)
                bossActive = filteredVals.First().ToLowerInvariant() == "nonboss";
            enableLegacyBlackArrows = filteredVals.Last().ToLowerInvariant() == "legacy";
        }
    }
    public class BlackArrowsSettings
    {
        public bool nonBossModeBlackArrows = false;
        public bool legacyBlackArrows = false;
    }
    
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Press the specified arrow button with \"!{0} up/right/down/left\" Words can be substituted as one letter (Ex. right as r). "+
        "Multiple directions can be issued in one command by spacing them out or as a 1 word when abbreviated, I.E. \"!{0} udlrrrll\". Alternatively, when abbreviated, you may space out the presses in the command. I.E. \"!{0} lluur ddlr urd\" Toggle colorblind mode with \"!{0} colorblind\"";
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
                        yield return string.Format("strikemessage incorrectly pressing {0} after {1} press(es) in the TP command!", idxToDirections[allPresses[x]], x + 1);
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
                        yield return string.Format("strikemessage incorrectly pressing {0} after {1} press(es) in the TP command!", idxToDirections[allPresses[x]], x + 1);
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
        /*
        readyToSolve = true; 
        currentStageNum = totalStagesGeneratable;
        if (currentFlashingDirection != null)
            StopCoroutine(currentFlashingDirection);
        textDisplay.text = "";
        arrowRenderers[0].material = setMats[0];
        arrowRenderers[1].material = setMats[0];
        arrowRenderers[2].material = setMats[0];
        arrowRenderers[3].material = setMats[0];
        */
        requestForceSolve = true;
        while (!readyToSolve)
            yield return true;

        for (int x = currentInputPos; x < finalDirectionIdxPresses.Count; x++)
        {
            yield return null;
            arrowButtons[finalDirectionIdxPresses[x]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        while (isanimating) { yield return true; yield return new WaitForSeconds(0.1f); };
    }

}
