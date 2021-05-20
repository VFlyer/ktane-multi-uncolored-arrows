using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using rnd = UnityEngine.Random;

public class ColouredArrowsScript : BaseArrowsScript {

    public KMBombInfo bomb;
    public KMRuleSeedable ruleSeedCore;
    public MeshRenderer[] arrowRenderers;

    public GameObject colorblindTextIndicator;
    public TextMesh[] colorblindTextArrows;

    private int targetButtonIdx = 0, idxColorDisplay, idxDirectionDisplay;
    private int streak = 0;

    private static int moduleIdCounter = 1;

    readonly string displayDirections = "\u25B4\u25B8\u25BE\u25C2";
    readonly string[] possibleColors = { "Red", "Yellow", "Green", "Blue", },
        baseDirections = { "Up", "Right", "Down", "Left", };

    int[] idxColorList = new int[] { 0, 1, 2, 3 };

    Dictionary<int, int[]> possibleIdxGoalColors = new Dictionary<int, int[]>() {
        // Example: {-1, new[] { 0, 1, 2, 3, } }
        // Key: color idx of the given display
        // Value: an idx array of possible goal colors for up, right, down, and left directions respectively
    };
    Dictionary<string, Color> connectedColors = new Dictionary<string, Color>() {
        { "Red", Color.red },
        { "Yellow", new Color(1, .841f, 0f) },
        { "Green", new Color(0.18393165f, 0.5955882f, 0.22083879f) },
        { "Blue", new Color(.03529412f, .043137256f, 1) },
    };
    void HandleRuleSeed()
    {
        
        if (ruleSeedCore != null)
        {
            var randomizer = ruleSeedCore.GetRNG();
            if (randomizer.Seed != 1)
            {
                int[] baseArrayList = randomizer.ShuffleFisherYates(new[] { 0, 1, 2, 3, });
                var combinedArrays = new List<int[]>();
                var modifiedArray = baseArrayList.ToList();
                for (var x = 0; x < 4; x++)
                {
                    combinedArrays.Add(modifiedArray.ToArray());
                    var firstValue = modifiedArray.First();
                    modifiedArray.RemoveAt(0);
                    modifiedArray.Add(firstValue);
                }
                
                randomizer.ShuffleFisherYates(combinedArrays);
                // Obtain the idxes to shuffle the columns.
                var shuffleIdxes = new int[4];
                for (var x = 0; x < shuffleIdxes.Length; x++)
                {
                    shuffleIdxes[x] = randomizer.Next(x, 3);
                }
                //Debug.Log(shuffleIdxes.Join());
                // Shuffle each cell in the array by swapping those respective values.
                for (var x = 0; x < shuffleIdxes.Length; x++)
                {
                    for (var y = 0; y < combinedArrays.Count; y++)
                    {
                        var temp = combinedArrays[y][x];
                        combinedArrays[y][x] = combinedArrays[y][shuffleIdxes[x]];
                        combinedArrays[y][shuffleIdxes[x]] = temp;
                    }
                }

                // Add their respective modifiers
                for (var x = 0; x < combinedArrays.Count; x++)
                {
                    possibleIdxGoalColors.Add(x, combinedArrays[x]);
                }
            }
            else
            {
                possibleIdxGoalColors.Add(0, new[] { 0, 1, 2, 3, });
                possibleIdxGoalColors.Add(1, new[] { 1, 2, 3, 0, });
                possibleIdxGoalColors.Add(2, new[] { 2, 3, 0, 1, });
                possibleIdxGoalColors.Add(3, new[] { 3, 0, 1, 2, });
            }
            Debug.LogFormat("[Coloured Arrows #{0}]: Rule seed for Coloured Arrows generated instructions with a seed of {1}.", moduleId, randomizer.Seed);
        }
        else
        {
            Debug.LogFormat("[Coloured Arrows #{0}]: Rule seed handler for Coloured Arrows does not exist. Using default instructions.", moduleId);
            possibleIdxGoalColors.Add(0, new[] { 0, 1, 2, 3, });
            possibleIdxGoalColors.Add(1, new[] { 1, 2, 3, 0, });
            possibleIdxGoalColors.Add(2, new[] { 2, 3, 0, 1, });
            possibleIdxGoalColors.Add(3, new[] { 3, 0, 1, 2, });
        }
        Debug.LogFormat("<Coloured Arrows #{0}>: Rule-Seed Generated Instructions: (Formatted as [Color displayed]: [ Goal Colors for Up, Right, Down, Left respectively ])", moduleId);
        foreach (var rsSet in possibleIdxGoalColors)
        {
            Debug.LogFormat("<Coloured Arrows #{0}>: {1}: [ {2} ]", moduleId,
                possibleColors.ElementAtOrDefault(rsSet.Key),
                rsSet.Value.Select(a => possibleColors.ElementAtOrDefault(a)).Join(", "));
        }
        
    }
    void Awake()
    {
        moduleId = moduleIdCounter++;
        moduleSolved = false;
        colorblindActive = Colorblind.ColorblindModeActive;
        for (int x = 0; x < arrowButtons.Length; x++)
        {
            int y = x;
            arrowButtons[x].OnInteract += delegate () {
                arrowButtons[y].AddInteractionPunch(0.25f);
                MAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, arrowButtons[y].transform);
                if (!moduleSolved && !isanimating)
                    CheckValidArrow(y);
                return false;
            };
        }
        modSelf.OnActivate += OnActivate;
    }
    protected override void QuickLog(string toLog = "")
    {
        Debug.LogFormat("[Coloured Arrows #{0}]: {1}", moduleId, toLog);
    }
    void Start () {
        HandleRuleSeed();
        GeneratePossibleArrows();
        textDisplay.text = "";
        for (int x = 0; x < arrowRenderers.Length; x++)
        {
            arrowRenderers[x].material.color = connectedColors[possibleColors[idxColorList[x]]];
            colorblindTextArrows[x].text = "";
        }
        colorblindArrowDisplay.text = "";
    }
    void CheckValidArrow(int idx)
    {
        if (idx < 0 || idx >= idxColorList.Length) return;
        if (idx == targetButtonIdx || targetButtonIdx == -1)
        {
            streak++;
            if (streak >= 7)
            {
                QuickLog(string.Format("You pressed {0}, which is correct! Streak is high enough to disarm!", baseDirections[idx]));
                moduleSolved = true;
                StartCoroutine(victory());
            }
            else
            {
                QuickLog(string.Format("You pressed {0}, which is correct! Current Streak: {1}.", baseDirections[idx], streak));
                GeneratePossibleArrows();
                StartCoroutine(ToNextArrow());
            }
        }
        else
        {
            streak = 0;
            QuickLog(string.Format("You pressed {0}, which is wrong! Streak reset to 0.", baseDirections[idx]));
            GeneratePossibleArrows();
            StartCoroutine(ToNextArrow());
            modSelf.HandleStrike();
        }

    }
    void GeneratePossibleArrows()
    {
        idxColorList.Shuffle();
        idxDirectionDisplay = rnd.Range(0, 4);
        idxColorDisplay = rnd.Range(0, 4);
        QuickLog(string.Format("The display is showing {1} in {0}.",
            possibleColors[idxColorDisplay],
            baseDirections[idxDirectionDisplay]
            ));

        var goalColor = possibleIdxGoalColors.ContainsKey(idxColorDisplay) ? possibleIdxGoalColors[idxColorDisplay][idxDirectionDisplay] : -1;
        targetButtonIdx = Array.IndexOf(idxColorList, goalColor);
        QuickLog(string.Format("The target arrow to press is the {0} arrow which is colored {1}.",
            baseDirections[targetButtonIdx],
            possibleColors[goalColor]
            ));
    }

    IEnumerator ToNextArrow()
    {
        isanimating = true;
        textDisplay.text = "";
        colorblindArrowDisplay.text = "";
        yield return new WaitForSeconds(1f);
        for (int x = 0; x < arrowRenderers.Length; x++)
        {
            arrowRenderers[x].material.color = new Color(1 / 3f, 1 / 3f, 1 / 3f);
            colorblindTextArrows[x].text = "";
            yield return new WaitForSeconds(0.1f);
        }
        yield return new WaitForSeconds(0.2f);
        yield return RevealArrows();
        
    }
    IEnumerator RevealArrows()
    {
        for (int x = 0; x < arrowRenderers.Length; x++)
        {
            arrowRenderers[x].material.color = connectedColors[possibleColors[idxColorList[x]]];
            colorblindTextArrows[x].text = colorblindActive ? possibleColors[idxColorList[x]].Substring(0, 1) : "";
            yield return new WaitForSeconds(0.1f);
        }
        textDisplay.text = displayDirections[idxDirectionDisplay].ToString();
        textDisplay.color = connectedColors[possibleColors[idxColorDisplay]];
        colorblindArrowDisplay.text = colorblindActive ? possibleColors[idxColorDisplay].Substring(0, 1) : "";
        yield return null;
        isanimating = false;
    }
    void HandleColorblindToggle()
    {
        colorblindTextIndicator.SetActive(colorblindActive);
        for (int x = 0; x < arrowRenderers.Length; x++)
        {
            colorblindTextArrows[x].text = colorblindActive ? possibleColors[idxColorList[x]].Substring(0, 1) : "";
        }
        colorblindArrowDisplay.text = colorblindActive ? possibleColors[idxColorDisplay].Substring(0, 1) : "";
    }

    void OnActivate()
    {
        if (colorblindActive)
            colorblindTextIndicator.SetActive(true);
        StartCoroutine(RevealArrows());
    }
    IEnumerator ContinuouslyShuffleColors()
    {
        while (isanimating)
        {
            int lastIDx = idxColorList.Last();
            for (int x = idxColorList.Length - 1; x > 0; x--)
                idxColorList[x] = idxColorList[x - 1];
            idxColorList[0] = lastIDx;
            for (int x = 0; x < arrowRenderers.Length; x++)
            {
                arrowRenderers[x].material.color = connectedColors[possibleColors[idxColorList[x]]];
                colorblindTextArrows[x].text = colorblindActive ? possibleColors[idxColorList[x]].Substring(0, 1) : "";
            }
            yield return new WaitForSeconds(0.25f);
        }
    }

    IEnumerator HandleRainbowTextAnim()
    {
        Color lastColor = textDisplay.color;
        for (int i = 0; i < 25; i++)
        {
            textDisplay.color = lastColor * (1.0f - i / 25f) + Color.white * (i / 25f);
            yield return new WaitForSeconds(0.025f);
        }
        /*do
        {*/
        for (int i = 0; i < 25; i++)
        {
            textDisplay.color = Color.white * (1.0f - i / 25f) + Color.red * (i / 25f);
            yield return new WaitForSeconds(0.025f);
        }
        for (int i = 0; i < 25; i++)
        {
            textDisplay.color = Color.red * (1.0f - i / 25f) + connectedColors["Green"] * (i / 25f);
            yield return new WaitForSeconds(0.025f);
        }
        for (int i = 0; i < 25; i++)
        {
            textDisplay.color = connectedColors["Green"] * (1.0f - i / 25f) + connectedColors["Yellow"] * (i / 25f);
            yield return new WaitForSeconds(0.025f);
        }
        for (int i = 0; i < 25; i++)
        {
            textDisplay.color = connectedColors["Yellow"] * (1.0f - i / 25f) + connectedColors["Blue"] * (i / 25f);
            yield return new WaitForSeconds(0.025f);
        }
        for (int i = 0; i < 25; i++)
        {
            textDisplay.color = connectedColors["Blue"] * (1.0f - i / 25f) + Color.white * (i / 25f);
            yield return new WaitForSeconds(0.025f);
        }
        /*}
        while (isanimating);*/
        textDisplay.color = Color.white;
    }

    protected override IEnumerator victory()
    {
        IEnumerator colorShuffle = ContinuouslyShuffleColors();
        isanimating = true;
        Color lastColor = textDisplay.color;
        colorblindArrowDisplay.text = "";
        StartCoroutine(colorShuffle);
        StartCoroutine(HandleRainbowTextAnim());
        for (int i = 0; i < 25; i++)
        {
            int rand1 = rnd.Range(0, 4);
            textDisplay.text = displayDirections[rand1].ToString();
            yield return new WaitForSeconds(0.025f);
        }
        textDisplay.transform.localPosition += Vector3.left * .04f;
        for (int i = 0; i < 25; i++)
        {
            int rand2 = rnd.Range(0, 10);
            textDisplay.text = rand2.ToString();
            yield return new WaitForSeconds(0.025f);
        }
        for (int i = 0; i < 50; i++)
        {
            int rand2 = rnd.Range(0, 10);
            textDisplay.text = "G" + rand2;
            yield return new WaitForSeconds(0.025f);
        }
        textDisplay.text = "GG";
        //StopCoroutine(colorShuffle);
        modSelf.HandlePass();
        /*
        IEnumerable<Color> lastColors = arrowRenderers.Select(a => a.material.color);
        for (int y = 0; y <= 50; y++)
        {
            for (int x = 0; x < arrowRenderers.Length; x++)
            {
                arrowRenderers[x].material.color = lastColors.ElementAt(x) * (1f - y / 50f) + new Color(1 / 3f, 1 / 3f, 1 / 3f) * (y / 50f);
                colorblindTextArrows[x].text = "";
            }
            yield return new WaitForSeconds(0.025f);
        }
        */
        isanimating = false;
    }

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = "Press the specified arrow button with \"!{0} up/right/down/left\" Words can be substituted as one letter (Ex. right as r) Toggle colorblind mode with \"!{0} colorblind\"";
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

    protected override IEnumerator TwitchHandleForcedSolve()
    {
        while (streak <= 6)
        {
            while (isanimating) { yield return true; yield return new WaitForSeconds(0.1f); };
            arrowButtons[targetButtonIdx].OnInteract();
            yield return null;
        }
        while (isanimating) { yield return true; yield return new WaitForSeconds(0.1f); };
    }
}
