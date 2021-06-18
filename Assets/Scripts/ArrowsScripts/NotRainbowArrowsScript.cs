using KModkit;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NotRainbowArrowsScript : BaseArrowsScript {

    public KMBombInfo bombInfo;
    public KMSelectable screenSelectable;
    public Renderer[] arrowRenders;
    public TextMesh[] colorblindTextMeshes;
    Dictionary<int, Color> idxConnectedColors = new Dictionary<int, Color>() {
        { 0, Color.red }, // Red
        { 1, new Color(.86764705f, .44878295f, 0) }, // Orange
        { 2, new Color(1, .841f, 0f) }, // Yellow
        { 3, new Color(0.18393165f, 0.5955882f, 0.22083879f) }, // Green
        { 4, new Color(.03529412f, .043137256f, 1) }, // Blue
        { 5, new Color(.41911763f, 0, .3555275f) }, // Purple
        { 6, Color.white }, // White
        { 7, Color.black }, // Black
    };
    public Material[] flashingMats;
    
    int[] colorIdxes, assignedBaseValues;
    string[] debugDirections = { "Up", "Up-Right", "Right", "Down-Right", "Down", "Down-Left", "Left" , "Up-Left" },
        debugColors = { "Red", "Orange", "Yellow", "Green", "Blue", "Purple", "White", "Black" };
    private static int moduleIdCounter = 1;
    int displayedValue = 0, curStageIdx = 0, textColorIdx = 0;
    List<List<int>> calculatedValuesAll = new List<List<int>>();
    bool isSubmitting = false;

    List<int> flashingColorIdxes;
    List<bool> flashingInverted;

    Dictionary<int, int> submittedSequence = new Dictionary<int, int>(), solutionSequence = new Dictionary<int, int>();


    IEnumerator currentFlasher;

    // Use this for initialization
    void Start () {
        moduleId = moduleIdCounter++;
        var anchorIdx = Enumerable.Range(0, 8).PickRandom();
        var idxColorList = Enumerable.Range(0, 6).ToArray().Shuffle();
        colorIdxes = new int[8];

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
	}
    void HandleSubmit()
    {
        if (!isSubmitting)
        {
            StopCoroutine(currentFlasher);
            isSubmitting = true;
        }
        else
        {

        }
    }

    void PrepModule()
    {
        HandleColorblindModeToggle();
        displayedValue = Random.Range(-7812, 7813);
        flashingColorIdxes = new List<int>();
        flashingInverted = new List<bool>();
        curStageIdx += (Random.value < 0.1f ? 1 : 0) + (Random.value < 0.1f ? 1 : 0);
        for (var x = 0; x < 2 + curStageIdx; x++)
        {
            flashingColorIdxes.Add(Random.Range(0, 6));
            flashingInverted.Add(Random.value < 0.5f);
        }
        textColorIdx = Random.Range(0, 6);
        QuickLog(string.Format("Colors around the module (Starting at North and going clockwise): {0}", colorIdxes.Select(a => debugColors[a]).Join(", ")));
        QuickLog(string.Format("Flashing Arrow Combinations: [{0}]",
            Enumerable.Range(0, Mathf.Min(flashingColorIdxes.Count, flashingInverted.Count)).Select(a => (flashingInverted[a] ? "Inverted " : "Normal ") + debugColors[flashingColorIdxes[a]]).Join("] , [")));

        StartCoroutine(TypeText(displayedValue.ToString()));
        currentFlasher = FlashCombinationSets();
        StartCoroutine(currentFlasher);
        CalculateInitialStages();
    }
    void CalculateInitialStages()
    {
        // Stage 1
        var stage1InitValue = displayedValue;
        var stage1Values = new List<int>() { stage1InitValue };
        calculatedValuesAll.Add(stage1Values);
        // Stage 2
        var stage2InitValue = 0;
        var last3SerialNoChars = bombInfo.GetSerialNumber().TakeLast(3);
        var base36Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        for (var x = 0; x < last3SerialNoChars.Count(); x++)
        {
            stage2InitValue *= 36;
            var idx = base36Digits.IndexOf(last3SerialNoChars.ElementAtOrDefault(x));
            stage2InitValue += idx == -1 ? 18 : idx;
        }
        stage2InitValue = stage2InitValue % 15625 - 7812;
        var stage2Values = new List<int>() { stage2InitValue };
        calculatedValuesAll.Add(stage2Values);
        // Stage 3
        var first3SerialNoChars = bombInfo.GetSerialNumber().Take(3);
        var stage3InitValue = 0;
        for (var x = 0; x < first3SerialNoChars.Count(); x++)
        {
            stage3InitValue *= 36;
            var idx = base36Digits.IndexOf(first3SerialNoChars.ElementAtOrDefault(x));
            stage3InitValue += idx == -1 ? 18 : idx;
        }
        stage3InitValue = stage3InitValue % 15625 - 7812;
        var stage3Values = new List<int>() { stage3InitValue };
        calculatedValuesAll.Add(stage3Values);
        QuickLog(string.Format("Stage 1 Initial Value: {0} (From displayed value)", stage1InitValue));
        QuickLog(string.Format("Stage 2 Expression for Initial Value: {0}",
            Enumerable.Range(0, 3).Select(a => (base36Digits.IndexOf(last3SerialNoChars.ElementAtOrDefault(a)) == -1 ? 18 : base36Digits.IndexOf(last3SerialNoChars.ElementAtOrDefault(a))).ToString() + Enumerable.Repeat("*36", 2 - a).Join("")).Join("+")
            ));
        QuickLog(string.Format("Stage 2 Initial Value: {0} (From \"{1}\", after mod 15625 minus 7812)", stage2InitValue, last3SerialNoChars.Join("")));
        QuickLog(string.Format("Stage 3 Expression for Initial Value: {0}",
            Enumerable.Range(0, 3).Select(a => (base36Digits.IndexOf(first3SerialNoChars.ElementAtOrDefault(a)) == -1 ? 18 : base36Digits.IndexOf(first3SerialNoChars.ElementAtOrDefault(a))).ToString() + Enumerable.Repeat("*36", 2 - a).Join("")).Join("+")
            ));
        QuickLog(string.Format("Stage 3 Initial Value: {0} (From \"{1}\", after mod 15625 minus 7812)", stage3InitValue, first3SerialNoChars.Join("")));
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
            yield return new WaitForSeconds(0.25f);
            textDisplay.color = Color.white;
            yield return new WaitForSeconds(0.25f);
        }
    }

    int PerformStage1Calculations(int currentValue, int curStep, int curColorIdxOperation, bool invert = false)
    {
        var output = currentValue;
        if (invert)
        {

        }
        else
        {
            switch (curColorIdxOperation)
            {
                default:
                    break;
            }
        }
        return output % 7813;
    }
    protected override IEnumerator victory() // The default victory animation from eXish's Arrows bretherns
    {
        isanimating = true;
        for (int i = 0; i < 100; i++)
        {
            int rand1 = Random.Range(0, 10);
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
    protected override void QuickLog(string toLog = "")
    {
        Debug.LogFormat("[Not Rainbow Arrows #{0}]: {1}", moduleId, toLog);
    }
    // Update is called once per frame
    void Update () {
		
	}
}
