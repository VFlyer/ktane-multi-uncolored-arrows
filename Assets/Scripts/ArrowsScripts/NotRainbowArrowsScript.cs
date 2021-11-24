using KModkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using rnd = UnityEngine.Random;

public class NotRainbowArrowsScript : BaseArrowsScript
{

    public KMBombInfo bombInfo;
    public KMSelectable screenSelectable;
    public Renderer[] arrowRenders;
    public TextMesh[] colorblindTextMeshes;
    public Transform arrowsCenter;
    public TextMesh colorblindDisplayTextMesh;
    readonly static Dictionary<int, Color> idxConnectedColors = new Dictionary<int, Color>() {
        { 0, Color.red }, // Red
        { 1, new Color(.86764705f, .44878295f, 0) }, // Orange
        { 2, new Color(1, .841f, 0f) }, // Yellow
        { 3, new Color(0.18393165f, 0.5955882f, 0.22083879f) }, // Green
        { 4, new Color(.03529412f, .043137256f, 1) }, // Blue
        { 5, new Color(.41911763f, 0, .3555275f) }, // Purple
        { 6, Color.white }, // White
        { 7, Color.black }, // Black
    };
    readonly static byte[][] possibleValueQueriesPerColor = {
        new byte[] { 0, 4, 2, 5, 7, 6, 3, 1 }, // Color order ROYGBPWK represents the 8 values in that array
        new byte[] { 4, 1, 0, 3, 2, 7, 6, 5 },
        new byte[] { 3, 7, 1, 6, 0, 5, 4, 2 },
        new byte[] { 1, 3, 7, 0, 6, 2, 5, 4 },
        new byte[] { 5, 6, 4, 2, 1, 0, 7, 3 },
        new byte[] { 6, 2, 5, 7, 3, 4, 1, 0 },
        new byte[] { 2, 5, 6, 1, 4, 3, 0, 7 },
        new byte[] { 7, 0, 3, 4, 5, 1, 2, 6 }
    }, thirdQueryGrid = {
        new byte[] { 0, 1, 7, 3, 5, 4, 2, 6 },
        new byte[] { 1, 3, 0, 4, 7, 6, 5, 2 },
        new byte[] { 3, 4, 1, 6, 0, 2, 7, 5 },
        new byte[] { 4, 6, 3, 2, 1, 5, 0, 7 },
        new byte[] { 6, 2, 4, 5, 3, 7, 1, 0 },
        new byte[] { 2, 5, 6, 7, 4, 0, 3, 1 },
        new byte[] { 5, 7, 2, 0, 6, 1, 4, 3 },
        new byte[] { 7, 0, 5, 1, 2, 3, 6, 4 }
    };
    readonly static string[] debugDirections = { "Up", "Up-Right", "Right", "Down-Right", "Down", "Down-Left", "Left", "Up-Left" },
        debugColors = { "Red", "Orange", "Yellow", "Green", "Blue", "Purple", "White", "Black" };
    public Material[] flashingMats;
    byte[] trueOctValues,
        twoBitsTable = {
        30, 2, 56, 60, 47, 26, 20, 18,
        32, 24, 6, 15, 54, 28, 41, 39,
        51, 43, 37, 44, 8, 49, 48, 33,
        55, 7, 0, 34, 4, 46, 38, 53,
        14, 50, 63, 22, 25, 10, 54, 21,
        45, 42, 9, 12, 23, 5, 29, 62,
        13, 40, 31, 19, 36, 27, 35, 59,
        61, 58, 3, 17, 16, 11, 57, 1 };
    int[] colorIdxes, idxColorsSubmit, responseValues;
    private static int moduleIdCounter = 1;
    int displayedValue = 0, textColorIdx = 0;
    bool isSubmitting = false, processing = false;

    List<int> flashingColorIdxes, colorIdxPressed, selectedResponseValues;
    List<bool> flashingInverted;


    IEnumerator currentFlasher;

    // Use this for initialization
    void Start()
    {
        moduleId = moduleIdCounter++;
        var anchorIdx = Enumerable.Range(0, 8).PickRandom();
        var idxColorList = Enumerable.Range(0, 6).ToArray().Shuffle();
        colorIdxes = new int[8];
        colorIdxPressed = new List<int>();
        responseValues = Enumerable.Range(0, 64).ToArray().Shuffle();
        var curStepIdx = 0;
        for (var x = 0; x < colorIdxes.Length; x++)
        {
            if (x == anchorIdx)
                colorIdxes[x] = 6;
            else if (x == (anchorIdx + 4) % 8)
                colorIdxes[x] = 7;
            else
            {
                colorIdxes[x] = idxColorList[curStepIdx];
                curStepIdx++;
            }

        }
        for (var x = 0; x < arrowRenders.Length; x++)
        {
            arrowRenders[x].material.color = idxConnectedColors[colorIdxes[x]];
            colorblindTextMeshes[x].text = "";
        }
        modSelf.OnActivate += PrepModule;
        textDisplay.text = "";
        colorblindActive = Colorblind.ColorblindModeActive;
        for (var x = 0; x < arrowButtons.Length; x++)
        {
            int y = x;
            arrowButtons[x].OnInteract += delegate
            {
                if (!(moduleSolved || isanimating))
                {
                    MAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, arrowButtons[y].transform);
                    arrowButtons[y].AddInteractionPunch(0.25f);
                    ProcessArrowPress(y);
                }
                return false;
            };
        }
        screenSelectable.OnInteract += delegate
        {
            if (!(moduleSolved || isanimating))
                HandleSubmit();
            return false;
        };

    }
    void ProcessArrowPress(int idx)
    {
        colorIdxPressed.Add(colorIdxes[idx]);
        textDisplay.text = colorIdxPressed.Select(a => "ROYGBPWK"[a]).Join("");
        if (colorIdxPressed.Count > 2 || processing)
        {
            modSelf.HandleStrike();
            OverrideCoroutine(Error());
            if (processing)
                QuickLog("Oops! Should have waited until it was in its initial phase again.");
            else
                QuickLog("Oops! Should have queried/submitted the given two at this point.");
        }
    }

    void OverrideCoroutine(IEnumerator handler = null)
    {
        if (currentFlasher != null)
            StopCoroutine(currentFlasher);
        for (var x = 0; x < arrowRenders.Length; x++)
        {
            arrowRenders[x].material = flashingMats[0];
            arrowRenders[x].material.color = idxConnectedColors[colorIdxes[x]];
        }
        textDisplay.color = Color.white;
        textDisplay.text = "";
        colorblindDisplayTextMesh.text = "";
        currentFlasher = handler;
        if (handler != null)
            StartCoroutine(currentFlasher);
    }

    void HandleSubmit()
    {
        if (colorIdxPressed.Any())
        {
            if (processing)
            {
                modSelf.HandleStrike();
                OverrideCoroutine(Error());
                QuickLog("Oops! Should have waited until it was in its initial phase again.");
            }
            else
                OverrideCoroutine(isSubmitting ? DelaySubmit() : DelayQuery());
        }
        else
        {
            isSubmitting = !isSubmitting;
            if (!isSubmitting)
            {
                OverrideCoroutine(FlashCombinationSets());
                StartCoroutine(TypeText(Enumerable.Range(0, 2).Reverse().Select(a => "01234567"[(int)(displayedValue / Mathf.Pow(8, a) % 8)]).Join("")));
            }
            else
                OverrideCoroutine();

        }
    }
    string ToOctal(int value, int padLength = -1)
    {
        string output = "";
        do
        {
            output += "01234567"[value % 8];
            value /= 8;
        }
        while (value > 0 && (output.Length < padLength || padLength < 0));
        for (var x = output.Length; x < padLength; x++)
        {
            output += '0';
        }
        return output.Reverse().Join("");
    }
    void PrepModule()
    {
        HandleColorblindModeToggle();
        displayedValue = rnd.Range(0, 64);
        flashingColorIdxes = new List<int>();
        flashingInverted = new List<bool>();
        for (var x = 0; x < 6; x++)
        {
            flashingColorIdxes.Add(rnd.Range(0, 6));
            flashingInverted.Add(rnd.value < 0.5f);
        }
        textColorIdx = rnd.Range(0, 6);
        QuickLog(string.Format("Colors around the module (Starting at North and going clockwise): {0}", colorIdxes.Select(a => debugColors[a]).Join(", ")));
        QuickLog(string.Format("Flashing Arrow Combinations: [{0}]",
            Enumerable.Range(0, Mathf.Min(flashingColorIdxes.Count, flashingInverted.Count)).Select(a => (flashingInverted[a] ? "Inverted " : "Normal ") + debugColors[flashingColorIdxes[a]]).Join("] , [")));
        QuickLog(string.Format("The text is flashing this color: {0}", debugColors[textColorIdx]));
        StartCoroutine(TypeText(ToOctal(displayedValue, 2)));
        QuickLog("All possible queries:");
        for (var x = 0; x < responseValues.Length; x++)
        {
            QuickLog(string.Format("{0}: {1}", ToOctal(x, 2), ToOctal(responseValues[x], 2)));
        }
        QuickLog();
        currentFlasher = FlashCombinationSets();
        StartCoroutine(currentFlasher);
        CalculateQueries();
    }
    void CalculateQueries()
    {

        selectedResponseValues = new List<int>();
        // First Query
        QuickLog("--------------------- First Query ---------------------");
        var serialNo = bombInfo.GetSerialNumber();
        var oct8Values = Enumerable.Range(0, 3).Select(a => ((char.IsDigit(serialNo[2 * a]) ? serialNo[2 * a] - '0' : serialNo[2 * a] - 'A' + 1) + (char.IsDigit(serialNo[2 * a + 1]) ? serialNo[2 * a + 1] - '0' : serialNo[2 * a + 1] - 'A' + 1)) % 8);
        QuickLogFormat("Z, Y, X: {0}", oct8Values.Join(","));
        trueOctValues = possibleValueQueriesPerColor[oct8Values.First()];
        QuickLogFormat("These colors are represented by their octal values: [{0}]", Enumerable.Range(0, 8).Select(a => debugColors[a] + ':' + trueOctValues[a].ToString()).Join("],["));
        var curQuery = 0;
        // First Query
        if (bombInfo.GetPortPlates().Any(a => a.Length == 0))
        {
            curQuery = oct8Values.Last() * 8 + oct8Values.ElementAt(1);
            //Debug.Log(Array.IndexOf(trueOctValues, (byte)oct8Values.ElementAt(1)));
            //Debug.Log(Array.IndexOf(trueOctValues, (byte)oct8Values.Last()));
            QuickLogFormat("Expected color combination for first query: {0}, {1}",
                debugColors[Array.IndexOf(trueOctValues, (byte)oct8Values.Last())],
                debugColors[Array.IndexOf(trueOctValues, (byte)oct8Values.ElementAt(1))]);
        }
        else
        {
            curQuery = oct8Values.Last() + oct8Values.ElementAt(1) * 8;
            //Debug.Log(Array.IndexOf(trueOctValues, (byte)oct8Values.ElementAt(1)));
            //Debug.Log(Array.IndexOf(trueOctValues, (byte)oct8Values.Last()));
            QuickLogFormat("Expected color combination for first query: {0}, {1}",
                debugColors[Array.IndexOf(trueOctValues, (byte)oct8Values.ElementAt(1))],
                debugColors[Array.IndexOf(trueOctValues, (byte)oct8Values.Last())]);
        }
        selectedResponseValues.Add(responseValues[curQuery]);
        QuickLogFormat("Which gives the response: {0}", ToOctal(selectedResponseValues.Last(), 2));
        // Second Query
        QuickLog();
        QuickLog("--------------------- Second Query ---------------------");
        var resultingQuery1 = selectedResponseValues.ElementAt(0);
        var invertedSets = flashingInverted.ToArray();
        var sumFlashes = 0;
        for (var x = 0; x < invertedSets.Length; x++)
        {
            sumFlashes *= 2;
            if (invertedSets[x])
                sumFlashes += 1;
        }
        QuickLogFormat("The octal representation of the flashes shown is {0}.", ToOctal(sumFlashes, 2));
        var xoredResult = resultingQuery1 ^ sumFlashes;
        QuickLogFormat("XORing with the result of the first query results in {0}.", ToOctal(xoredResult, 2));
        var arrayBoolStates = new List<bool>();
        for (var x = 0; x < 6; x++)
        {
            arrayBoolStates.Add(xoredResult % 2 == 1);
            xoredResult /= 2;
        }
        arrayBoolStates.Reverse();
        QuickLogFormat("The binary representation of this, ordered from most to least significant, is {0}.", arrayBoolStates.Select(a => a ? '1' : '0').Join(""));
        var digitsConcatIdxes = new[] { 1, 3, 5, 0, 2, 4 };
        var newValue = 0;
        for (var x = 0; x < 6; x++)
        {
            newValue *= 2;
            if (arrayBoolStates[digitsConcatIdxes[x]])
                newValue += 1;
        }
        QuickLogFormat("The new octal value should be {0}.", ToOctal(newValue, 2));
        curQuery = twoBitsTable[newValue];
        QuickLogFormat("Cross referencing the second query's table should give you {0}.", ToOctal(curQuery, 2));
        selectedResponseValues.Add(responseValues[curQuery]);
        QuickLogFormat("Expected color combination for second query: {0}, {1}", debugColors[Array.IndexOf(trueOctValues, (byte)(curQuery / 8))], debugColors[Array.IndexOf(trueOctValues, (byte)(curQuery % 8))]);
        QuickLogFormat("Which gives this response: {0}", ToOctal(responseValues[curQuery], 2));
        QuickLog();
        // Third Query
        QuickLog("--------------------- Third Query ---------------------");
        var resultingQuery2 = selectedResponseValues.ElementAt(1);
        var curCol = resultingQuery2 % 8;
        var curRow = resultingQuery2 / 8;
        var curDirectionIdx = Array.IndexOf(colorIdxes, 6);
        var rotateDirectionIdx = 0;
        var resultingBHDigits = new List<byte>();
        for (var x = 0; x < 8 && colorIdxes[rotateDirectionIdx] != textColorIdx; x++)
            rotateDirectionIdx++;
        QuickLogFormat("Starting on row {0}, col {1} on the grid where top-left of the grid is row 0, col 0 and facing {2}", curRow, curCol, debugDirections[curDirectionIdx]);
        for (var p = 0; p < 4; p++)
        {
            List<int> curBHDigits = new List<int>();
            for (var step = 0; step < p + 1; step++)
            {
                curBHDigits.Add(thirdQueryGrid[curRow][curCol]);
                switch (curDirectionIdx)
                {
                    case 0: // North
                        curRow = (curRow + 7) % 8;
                        break;
                    case 1: // North - East
                        curRow = (curRow + 7) % 8;
                        curCol = (curCol + 1) % 8;
                        break;
                    case 2: // East
                        curCol = (curCol + 1) % 8;
                        break;
                    case 3: // South - East
                        curRow = (curRow + 1) % 8;
                        curCol = (curCol + 1) % 8;
                        break;
                    case 4: // South
                        curRow = (curRow + 1) % 8;
                        break;
                    case 5: // South - West
                        curRow = (curRow + 1) % 8;
                        curCol = (curCol + 7) % 8;
                        break;
                    case 6: // West
                        curCol = (curCol + 7) % 8;
                        break;
                    case 7: // North - West
                        curRow = (curRow + 7) % 8;
                        curCol = (curCol + 7) % 8;
                        break;
                }
            }
            QuickLogFormat("Digits noted for movement #{0}: {1} ", p + 1, curBHDigits.Join());
            resultingBHDigits.Add((byte)(curBHDigits.Sum() % 8));
            curDirectionIdx = (curDirectionIdx + rotateDirectionIdx) % 8;
            if (p < 3)
                QuickLogFormat("Next direction: {0} on row {1}, col {2}", debugDirections[curDirectionIdx], curRow, curCol);
        }
        QuickLogFormat("All digits, summed up, after modulo 8: {0}", resultingBHDigits.Join());
        var twoDigits = Enumerable.Range(0, 2).Select(a => resultingBHDigits[2 * a] ^ resultingBHDigits[2 * a + 1]);
        QuickLogFormat("After XORing the pairs: {0}", twoDigits.Join());
        newValue = twoDigits.First() * 8 + twoDigits.Last();
        curQuery = twoBitsTable[newValue];
        QuickLogFormat("Cross referencing the second query's table should give you {0}.", ToOctal(curQuery, 2));
        selectedResponseValues.Add(responseValues[curQuery]);
        QuickLogFormat("Expected color combination for third query: {0}, {1}", debugColors[Array.IndexOf(trueOctValues, (byte)(curQuery / 8))], debugColors[Array.IndexOf(trueOctValues, (byte)(curQuery % 8))]);
        QuickLogFormat("Which gives this response: {0}", ToOctal(responseValues[curQuery], 2));
        QuickLog();
        // Submission
        QuickLog("---------------------- Submission ----------------------");
        var portTypesVanilla = new[] { Port.DVI, Port.Parallel, Port.PS2, Port.RJ45, Port.StereoRCA, Port.Serial, };
        var portCountSpecific = portTypesVanilla.Select(pt => bombInfo.GetPortCount(pt));
        var selectedIdxes = new List<int>();
        var debugPositions = new[] { "1st", "2nd", "3rd", "4th", "5th", "6th" };
        QuickLogFormat("Port counts for relevant port types: {0}", Enumerable.Range(0, 6).Select(a => '[' + portTypesVanilla[a].ToString() + ": " + portCountSpecific.ElementAt(a) + ']').Join(", "));
        QuickLogFormat("Relevant port types that occurred the most: {0}", Enumerable.Range(0, 6).Where(a => portCountSpecific.ElementAt(a) >= portCountSpecific.Max()).Select(b => portTypesVanilla[b].ToString()).Join(", "));
        if (portCountSpecific.Count(a => a >= portCountSpecific.Max()) > 3)
        {
            selectedIdxes.AddRange(new[] { 1, 3, 5 });
            QuickLogFormat("There is a {0}-way tie for the most amount of ports. Using the 2nd, 4th, and 6th flashes in the flashing sequence.", portCountSpecific.Count(a => a >= portCountSpecific.Max()));
        }
        else
        {
            selectedIdxes.AddRange(Enumerable.Range(0, 6).Where(a => portCountSpecific.ElementAt(a) < portCountSpecific.Max()));
            QuickLogFormat("Using the {0} flashes in the flashing sequence.", selectedIdxes.Select(a => debugPositions[a]).Join(", "));
        }
        var finalCalculatedValue = displayedValue;
        var sumResponses = selectedResponseValues.Sum();
        
        QuickLogFormat("Sum of responses up to this point: {0} (In decimal, from octal responses {1})", sumResponses, selectedResponseValues.Select(a => ToOctal(a, 2)).Join(", "));
        QuickLog("The rest of this section will be calculated in decimal.");
        QuickLogFormat("Initially displayed value in base 10: {0}", displayedValue);
        for (int i = 0; i < selectedIdxes.Count; i++)
        {
            int oneIdx = selectedIdxes[i];
            switch (flashingColorIdxes[oneIdx])
            {
                case 0: // Red
                    QuickLogFormat("Using equation\"R(x) = x + A\u2080\", solving for {0}.", flashingInverted[oneIdx] ? "x" : "R(x)");
                    finalCalculatedValue += (flashingInverted[oneIdx] ? -1 : 1) * selectedResponseValues[0];
                    break;
                case 1: // Orange
                    QuickLogFormat("Using equation\"O(x) + A\u2081 = x\", solving for {0}.", flashingInverted[oneIdx] ? "x" : "O(x)");
                    finalCalculatedValue -= (flashingInverted[oneIdx] ? -1 : 1) * selectedResponseValues[1];
                    break;
                case 2: // Yellow
                    QuickLogFormat("Using equation\"Y(x) + (2\u207F - 1) = A\u2082 - x\", solving for {0}.", flashingInverted[oneIdx] ? "x" : "Y(x)");
                    finalCalculatedValue = selectedResponseValues[2] - finalCalculatedValue - ((1 << i) - 1);
                    break;
                case 3: // Green
                    QuickLogFormat("Using equation\"G(x) + x = 2(x + \u2211A / 6)\", solving for {0}.", flashingInverted[oneIdx] ? "x" : "G(x)");
                    finalCalculatedValue += (flashingInverted[oneIdx] ? -1 : 1) * sumResponses / 3;
                    break;
                case 4: // Blue
                    QuickLogFormat("Using equation\"x - 2 * B(x) - 2 * n = min(A) - B(x)\", solving for {0}.", flashingInverted[oneIdx] ? "x" : "B(x)");
                    finalCalculatedValue += (flashingInverted[oneIdx] ? 1 : -1) * (selectedResponseValues.Min() + 2 * i);
                    break;
                case 5: // Purple
                    QuickLogFormat("Using equation\"3 * P(x) - 2 * x = max(A) - x + 4 * P(x)\", solving for {0}.", flashingInverted[oneIdx] ? "x" : "P(x)");
                    finalCalculatedValue = - (finalCalculatedValue + selectedResponseValues.Max());
                    break;
            }
            QuickLogFormat("After flash index {0}: {1}", i, finalCalculatedValue);
            finalCalculatedValue = ((finalCalculatedValue % 64) + 64) % 64;
            QuickLogFormat("Kept within 0 - 63 inclusive: {0}", finalCalculatedValue);
        }
        curQuery = twoBitsTable[finalCalculatedValue];
        QuickLogFormat("Cross referencing the second query's table should give you {0}.", ToOctal(curQuery, 2));
        idxColorsSubmit = new[] { Array.IndexOf(trueOctValues, (byte)(curQuery / 8)), Array.IndexOf(trueOctValues, (byte)(curQuery % 8)) };
        QuickLogFormat("Expected color combination to submit: {0}, {1}", debugColors[Array.IndexOf(trueOctValues, (byte)(curQuery / 8))], debugColors[Array.IndexOf(trueOctValues, (byte)(curQuery % 8))]);
        QuickLog();
        QuickLog("------------------ User Interactions ------------------");
    }

    void HandleColorblindModeToggle()
    {
        for (var x = 0; x < colorblindTextMeshes.Length; x++)
        {
            var curColorIdx = colorIdxes[x];
            colorblindTextMeshes[x].text = colorblindActive && !(curColorIdx == 7 || curColorIdx == 6) ? debugColors[curColorIdx].Substring(0, 1) : "";
            colorblindTextMeshes[x].color = colorblindActive && !(curColorIdx == 7 || curColorIdx == 2) ? Color.white : Color.black;
        }
        colorblindArrowDisplay.gameObject.SetActive(colorblindActive);
    }
    IEnumerator TypeText(string value)
    {
        textDisplay.text = "";
        for (int x = 1; x < value.Length + 1; x++)
        {
            yield return new WaitForSeconds(0.1f);
            textDisplay.text = value.Substring(0, x);
        }
        yield return new WaitForSeconds(0.2f);
        isanimating = false;
    }
    IEnumerator FlashCombinationSets()
    {
        while (!isSubmitting)
        {
            for (var y = 0; y < Mathf.Min(flashingColorIdxes.Count, flashingInverted.Count) && !isSubmitting; y++)
            {
                var curColorIdx = flashingColorIdxes[y];
                var requireInvert = flashingInverted[y];
                for (var x = 0; x < arrowRenders.Length; x++)
                {
                    var canFlash = !new[] { 6, 7 }.Contains(colorIdxes[x]) &&
                        (curColorIdx == colorIdxes[x] ^ requireInvert);

                    arrowRenders[x].material = canFlash ? flashingMats[1] : flashingMats[0];
                    arrowRenders[x].material.color = canFlash ? ((idxConnectedColors[colorIdxes[x]] * 0.5f) + Color.gray) : idxConnectedColors[colorIdxes[x]];
                }
                yield return new WaitForSeconds(0.25f);
                for (var x = 0; x < arrowRenders.Length; x++)
                {
                    arrowRenders[x].material = flashingMats[0];
                    arrowRenders[x].material.color = idxConnectedColors[colorIdxes[x]];
                }
                yield return new WaitForSeconds(0.25f);
            }
            textDisplay.color = idxConnectedColors[textColorIdx];
            if (colorblindActive)
                colorblindDisplayTextMesh.text = debugColors[textColorIdx].Substring(0, 1);
            yield return new WaitForSeconds(0.25f);
            textDisplay.color = Color.white;
            colorblindDisplayTextMesh.text = "";
            yield return new WaitForSeconds(0.25f);
        }
    }
    protected override IEnumerator victory() // The default victory animation from eXish's Arrows bretherns
    {
        isanimating = true;
        for (int i = 0; i < 100; i++)
        {
            int rand1 = rnd.Range(0, 10);
            if (i < 50)
            {
                textDisplay.text = rand1.ToString();
            }
            else
            {
                textDisplay.text = "G" + rand1;
            }
            yield return new WaitForSeconds(0.025f);
        }
        textDisplay.text = "GG";
        isanimating = false;
        modSelf.HandlePass();
    }
    protected IEnumerator GrayOutArrows()
    {
        for (var x = 0; x < 3; x++)
        {
            var expectedColor = Color.white * (x + 1) / 4f;

            var curIdxScanCW = (Array.IndexOf(colorIdxes, 7) + (1 + x) * 1) % 8;
            var curIdxScanCCW = (Array.IndexOf(colorIdxes, 7) + (1 + x) * 7) % 8;
            colorblindTextMeshes[curIdxScanCCW].text = "";
            colorblindTextMeshes[curIdxScanCCW].color = Color.white;
            colorblindTextMeshes[curIdxScanCW].text = "";
            colorblindTextMeshes[curIdxScanCW].color = Color.white;
            for (float t = 0; t < 1f; t += Time.deltaTime * 1f)
            {
                yield return null;
                arrowRenders[curIdxScanCW].material.color = idxConnectedColors[colorIdxes[curIdxScanCW]] * (1f - t) + expectedColor * t;
                arrowRenders[curIdxScanCCW].material.color = idxConnectedColors[colorIdxes[curIdxScanCCW]] * (1f - t) + expectedColor * t;
            }
            arrowRenders[curIdxScanCW].material.color = expectedColor;
            arrowRenders[curIdxScanCCW].material.color = expectedColor;
        }
    }
    protected IEnumerator UngrayOutArrows()
    {
        for (var x = 2; x >= 0; x--)
        {
            var expectedColor = Color.white * (x + 1) / 4f;

            var curIdxScanCW = (Array.IndexOf(colorIdxes, 7) + (1 + x) * 1) % 8;
            var curIdxScanCCW = (Array.IndexOf(colorIdxes, 7) + (1 + x) * 7) % 8;
            if (colorblindActive)
            {
                var curColorIdx = colorIdxes[curIdxScanCCW];
                colorblindTextMeshes[curIdxScanCCW].text = colorblindActive && !(curColorIdx == 7 || curColorIdx == 6) ? debugColors[curColorIdx].Substring(0, 1) : "";
                colorblindTextMeshes[curIdxScanCCW].color = colorblindActive && !(curColorIdx == 7 || curColorIdx == 2) ? Color.white : Color.black;
                curColorIdx = colorIdxes[curIdxScanCW];
                colorblindTextMeshes[curIdxScanCW].text = colorblindActive && !(curColorIdx == 7 || curColorIdx == 6) ? debugColors[curColorIdx].Substring(0, 1) : "";
                colorblindTextMeshes[curIdxScanCW].color = colorblindActive && !(curColorIdx == 7 || curColorIdx == 2) ? Color.white : Color.black;
            }
            for (float t = 0; t < 1f; t += Time.deltaTime * 3f)
            {
                yield return null;
                arrowRenders[curIdxScanCW].material.color = idxConnectedColors[colorIdxes[curIdxScanCW]] * t + expectedColor * (1f - t);
                arrowRenders[curIdxScanCCW].material.color = idxConnectedColors[colorIdxes[curIdxScanCCW]] * t + expectedColor * (1f - t);
            }
            arrowRenders[curIdxScanCW].material.color = idxConnectedColors[colorIdxes[curIdxScanCW]];
            arrowRenders[curIdxScanCCW].material.color = idxConnectedColors[colorIdxes[curIdxScanCCW]];
        }
    }

    protected IEnumerator DelaySubmit()
    {
        isanimating = true;
        MAudio.PlaySoundAtTransform("Two_Bits_processing", transform);
        StartCoroutine(GrayOutArrows());
        var rand1 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToArray().Shuffle().Join("");
        for (int i = 0; i < 100; i++)
        {
            textDisplay.text = rand1.Substring(i % 25, 2);
            yield return new WaitForSeconds(0.05f);
        }
        if (colorIdxPressed.Count != 2)
        {
            QuickLog("Oops! Should have tried to submit 2 colors. Submitted with 1 color instead.");
            modSelf.HandleStrike();
            StartCoroutine(UngrayOutArrows());
            yield return Error();
            yield break;
        }
        else if (idxColorsSubmit == null || !idxColorsSubmit.Any() || idxColorsSubmit.SequenceEqual(colorIdxPressed))
        {
            QuickLogFormat("Submitted correct sequence of colors: {0}", colorIdxPressed.Select(a => debugColors[a]).Join(", "));
            modSelf.HandlePass();
            textDisplay.text = "GG";
            moduleSolved = true;
            var idxWhite = Array.IndexOf(colorIdxes, 6);
            var resultingRotationRequested = Quaternion.Euler(0, -45 * idxWhite, 0);
            var curAngle = arrowsCenter.localRotation;
            for (float x = 0; x <= 1f; x += Time.deltaTime * 4 / Mathf.Min(Math.Abs(idxWhite - 8), idxWhite))
            {
                yield return null;
                arrowsCenter.localRotation = Quaternion.Lerp(curAngle, resultingRotationRequested, x);
            }
            arrowsCenter.localRotation = resultingRotationRequested;
            /*
            var excludingBlack = Enumerable.Range(0, 8).Where(a => colorIdxes[a] != 7);
            var nonBlackMats = excludingBlack.Select(a => arrowRenders[a].material.color);
            for (float x = 0; x < 1f; x += Time.deltaTime / 8)
            {
                yield return null;
                for (int idx = 0; idx < excludingBlack.Count(); idx++)
                {
                    var curIdx = excludingBlack.ElementAt(idx);
                    arrowRenders[curIdx].material.color = nonBlackMats.ElementAt(idx) * (1f - x);
                }
            }
            for (int idx = 0; idx < excludingBlack.Count(); idx++)
            {
                var curIdx = excludingBlack.ElementAt(idx);
                arrowRenders[curIdx].material.color = Color.black;
            }
            */
            isanimating = false;
        }
        else
        {
            QuickLogFormat("Submitted incorrect sequence of colors: {0}", colorIdxPressed.Select(a => debugColors[a]).Join(", "));
            modSelf.HandleStrike();
            StartCoroutine(UngrayOutArrows());
            for (var x = 0; x < 5; x++)
            {
                textDisplay.text = "NO";
                yield return new WaitForSeconds(0.5f);
                textDisplay.text = "";
                yield return new WaitForSeconds(0.5f);
            }
            isSubmitting = false;
            colorIdxPressed.Clear();
            StartCoroutine(TypeText(ToOctal(displayedValue, 2)));
            OverrideCoroutine(FlashCombinationSets());
        }
    }
    protected IEnumerator Error()
    {
        for (var x = 0; x < 5f; x++)
        {
            textDisplay.text = "ERR";
            yield return new WaitForSeconds(0.5f);
            textDisplay.text = "";
            yield return new WaitForSeconds(0.5f);
        }
        processing = false;
        isSubmitting = false;
        colorIdxPressed.Clear();
        StartCoroutine(TypeText(ToOctal(displayedValue, 2)));
        OverrideCoroutine(FlashCombinationSets());
    }
    protected IEnumerator DelayQuery()
    {
        processing = true;
        StartCoroutine(TypeText("WAIT"));
        yield return new WaitForSeconds(bombInfo.GetTime() > 90f ? 5f : 2.5f);
        if (colorIdxPressed.Count != 2)
        {
            QuickLog("Oops! Should have queried 2 colors. Queried with 1 color instead.");
            modSelf.HandleStrike();
            yield return Error();
            yield break;
        }
        var y = 0;
        for (var x = 0; x < 2; x++)
        {
            y *= 8;
            y += trueOctValues[colorIdxPressed[x]];
        }
        y = responseValues[y];

        StartCoroutine(TypeText("R:" + ToOctal(y, 2)));
        yield return new WaitForSeconds(bombInfo.GetTime() > 90f ? 5f : 2.5f);
        processing = false;
        colorIdxPressed.Clear();
        StartCoroutine(TypeText(ToOctal(displayedValue, 2)));
        OverrideCoroutine(FlashCombinationSets());

    }
    protected override void QuickLogFormat(string toLog = "", params object[] args)
    {
        Debug.LogFormat("[Not Rainbow Arrows #{0}]: {1}", moduleId, string.Format(toLog, args));
    }
    protected override void QuickLog(string toLog = "")
    {
        Debug.LogFormat("[Not Rainbow Arrows #{0}]: {1}", moduleId, toLog);
    }
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Press the specified arrow button with \"!{0} up/right/down/left\" by cardinal (N, NE, E, ...) or by direction (upright, upleft, ...).  Words can be substituted as an abbreviated form (Ex. right as r). Press the display with \"!{0} display/screen/scn\". " +
            "Multiple directions + buttons can be issued in one command by spacing them out. Toggle colorblind mode with \"!{0} colorblind\"";
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
            HandleColorblindModeToggle();
            yield break;
        }
        else
        {
            string[] cmdSets = command.Trim().Split();
            List<KMSelectable> allPresses = new List<KMSelectable>();
            for (int x = 0; x < cmdSets.Length; x++)
            {
                if (Regex.IsMatch(cmdSets[x], @"^\s*(u(p)?|n(orth)?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[0]);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*(d(own)?|s(outh)?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[4]);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*(l(eft)?|w(est)?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[6]);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*(r(ight)?|e(ast)?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[2]);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*(u(p)?[-\s]?r(ight)?|n(orth)?[-\s]?e(ast)?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[1]);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*l(eft)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[3]);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*r(ight)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[5]);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*r(ight)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(arrowButtons[7]);
                }
                else if (Regex.IsMatch(cmdSets[x], @"^\s*(screen|display|scn)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    allPresses.Add(screenSelectable);
                }
                else
                {
                    yield return string.Format("sendtochaterror I do not know what \"{0}\" is supposed to be. Check your command again for typos.", cmdSets[x]);
                    yield break;
                }
            }
            for (var x = 0; x < allPresses.Count; x++)
            {
                yield return null;
                allPresses[x].OnInteract();
                if (allPresses[x] == screenSelectable) { yield return "solve"; yield return "strike"; }
                yield return new WaitForSeconds(0.1f);
                if (isanimating) yield break;
            }
        }
    }
}
