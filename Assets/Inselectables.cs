using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;
using System.Text.RegularExpressions;

public class Inselectables : MonoBehaviour
{

    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMColorblindMode ColorblindMode;

    static int _moduleIdCounter = 1;
    int _moduleId;
    private bool _moduleSolved = false;

    private bool _colorblindMode;

    public KMSelectable[] letteredButtons;
    public KMSelectable goButton;
    public GameObject[] highlights;
    public GameObject[] coloredHighlights;
    public GameObject[] coloredSelections;
    public TextMesh[] lettersTextMesh;
    public TextMesh goTextMesh;
    public TextMesh[] colorblindTextMesh;
    public Color[] textColors;
    public Material[] highlightMats; // magenta is positive offset, yellow is negative offset

    private bool allowedToPress = true;

    private static readonly string[] startMsgs = {"DEFUSERS", "DEFUSING", "EXPLODED", "EXPLODER", "DONTLOOK", "BOMBBOOM", "CIRCULAR", "DISARMED", "GOODLUCK", "BESTPONY", "NOBUTTON", "HIGHLITE", "HEREWEGO", "ORGANIZE", "WATCHOUT", "SELECTED", "NOMANUAL"};

    private IEnumerator playStartingSound;
    private IEnumerator setLetters;
    private IEnumerator setSolvingLetters;
    private IEnumerator playSubmitSound;
    private IEnumerator strike;
    private bool goButtonHeld = false;
    private bool solvingPhase = false;
    private string[] ins = { "ins01", "ins02", "ins03", "ins04", "ins05", "ins06", "ins07", "ins08", "ins09", "ins10", "ins11", "ins12", "ins13", "ins14", "ins15", "ins16" };
    private string[] insSub = { "insSub01", "insSub02", "insSub03", "insSub04", "insSub05", "insSub06", "insSub07", "insSub08", "insSub09", "insSub10", "insSub11", "insSub12", "insSub13", "insSub14", "insSub15", "insSub16" };

    public string[] letters = new string[26];

    private List<int> chosenLetters = new List<int>();
    private List<int> decoyLetters = new List<int>();
    private List<int> indexLetters = new List<int>();
    private List<int> randomLetters = new List<int>();
    private List<int> offsets = new List<int>();
    private List<int> preOffsetLetters = new List<int>();
    private List<int> finalLetters = new List<int>();
    private List<int> offsetsHighlights = new List<int>();

    private List<int> solvingScreenLetters = new List<int>();
    private List<int> solvingTempList = new List<int>();
    private List<int> solutionSelections = new List<int>();
    private List<int> solutionSelectionLetters = new List<int>();

    private int removeFromList = 0;
    private List<int> chosenDecoyIxs = new List<int>();

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        SetColorblindMode(ColorblindMode.ColorblindModeActive);

        for (int i = 0; i < letteredButtons.Length; i++)
        {
            int j = i;
            letteredButtons[i].OnInteract += delegate ()
            {
                if (allowedToPress)
                {
                    LetteredButtonPress(j);
                }
                return false;
            };
            letteredButtons[i].OnHighlight += delegate ()
            {
                if (allowedToPress)
                {
                    DoHighlight(j);
                }
            };
            letteredButtons[i].OnHighlightEnded += delegate ()
            {
                ClearColoredHighlights();
            };
            ClearLetters();
        }
        goButton.OnInteract += delegate ()
        {
            if (allowedToPress)
            {
                GoButtonPress();
            }
            return false;
        };

        int randMsg = Rnd.Range(0, startMsgs.Length);
        for (int i = 0; i < 8; i++)
        {
            lettersTextMesh[i].text = startMsgs[randMsg].Substring(i, 1);
        }

        goButton.OnInteractEnded += OnRelease;

        solutionSelectionLetters.Clear();
        ClearColoredHighlights();
        ClearColoredSelections();
        solutionSelections.Clear();
        solvingPhase = false;

        GetComponent<KMBombModule>().OnActivate += ActivateModule;
    }

    void ActivateModule()
    {
        GenerateSolution();
    }

    void SetColorblindMode(bool active)
    {
        _colorblindMode = active;
    }

    void ClearColoredHighlights()
    {
        for (int i = 0; i < coloredHighlights.Length; i++)
        {
            coloredHighlights[i].SetActive(false);
            colorblindTextMesh[i].text = "";
        }
    }

    void ClearColoredSelections()
    {
        for (int i = 0; i < coloredSelections.Length; i++)
        {
            coloredSelections[i].SetActive(false);
        }
    }

    void LetteredButtonPress(int letteredButton)
    {
        if (!_moduleSolved)
        {
            if (solvingPhase)
            {
                ToggleSelection(solvingTempList[letteredButton]);
            }
        }
    }

    void GoButtonPress()
    {
        if (!_moduleSolved)
        {
            if (allowedToPress)
            {
                if (!solvingPhase)
                {
                    playStartingSound = PlayStartingSound();
                    StartCoroutine(playStartingSound);
                }

                else
                {
                    playSubmitSound = PlaySubmittingSound();
                    StartCoroutine(playSubmitSound);
                }
            }
        }
    }

    void OnRelease()
    {
        if (!_moduleSolved)
        {
            if (allowedToPress)
            {
                goButtonHeld = false;
                if (playStartingSound != null)
                {
                    StopCoroutine(playStartingSound);
                }
                if (!solvingPhase)
                {
                    for (int i = 0; i < lettersTextMesh.Length; i++)
                    {
                        lettersTextMesh[i].color = textColors[0];
                    }
                    GenerateOthers();
                }
                else
                {
                    for (int i = 0; i < lettersTextMesh.Length; i++)
                    {
                        lettersTextMesh[i].color = textColors[1];
                    }
                }
            }
        }
    }

    void DoHighlight(int highlight)
    {
        if (!solvingPhase)
        {
            coloredHighlights[(highlight + offsets[highlight] + 8) % 8].SetActive(true);
            if (offsets[highlight] > 0)
            {
                coloredHighlights[(highlight + offsets[highlight] + 8) % 8].GetComponent<MeshRenderer>().material = highlightMats[0];
                if (_colorblindMode)
                {
                    colorblindTextMesh[(highlight + offsets[highlight] + 8) % 8].text = "p";
                    colorblindTextMesh[(highlight + offsets[highlight] + 8) % 8].color = textColors[3];
                }
            }
            else
            {
                coloredHighlights[(highlight + offsets[highlight] + 8) % 8].GetComponent<MeshRenderer>().material = highlightMats[1];
                if (_colorblindMode)
                {
                    colorblindTextMesh[(highlight + offsets[highlight] + 8) % 8].text = "y";
                    colorblindTextMesh[(highlight + offsets[highlight] + 8) % 8].color = textColors[2];
                }
            }
        }
        else
        {
            coloredHighlights[solvingTempList[highlight]].SetActive(true);
            coloredHighlights[solvingTempList[highlight]].GetComponent<MeshRenderer>().material = highlightMats[2];
        }
    }

    void ToggleSelection(int selection)
    {
        if (coloredSelections[selection].activeInHierarchy)
        {
            coloredSelections[selection].SetActive(false);
            
            solutionSelections.Remove(selection);
            solutionSelectionLetters.Remove(solvingScreenLetters[selection]);
        }
        else
        {
            coloredSelections[selection].SetActive(true);
            coloredSelections[selection].GetComponent<MeshRenderer>().material = highlightMats[5];
            solutionSelections.Add(selection);
            solutionSelectionLetters.Add(solvingScreenLetters[selection]);
        }
    }

    IEnumerator PlayStartingSound()
    {
        goButtonHeld = true;
        yield return new WaitForSeconds(0.2f);
        for (int i = 0; i < 15; i++)
        // plays 16 different audio tracks (which when combined play a full tune). if button is released, tune stops mid way
        {
            if (goButtonHeld)
            {
                if (!solvingPhase)
                {
                    Audio.PlaySoundAtTransform(ins[i], transform);
                    lettersTextMesh[i / 2].color = textColors[1];
                    yield return new WaitForSeconds(0.2608125f);
                    if (i == 14)
                    {
                        solvingPhase = true;
                        goTextMesh.color = textColors[1];
                        GenerateSolvingScreen();
                        for (int j = 0; j < 8; j++)
                        {
                            //coloredSelections[j].GetComponent<MeshRenderer>().material = highlightMats[2];
                        }
                    }
                }
            }
        }
    }

    IEnumerator PlaySubmittingSound()
    {
        goButtonHeld = true;
        yield return new WaitForSeconds(0.2f);
        for (int i = 0; i < 16; i++)
        // plays 16 different audio tracks (which when combined play a full tune). if button is released, tune stops mid way
        {
            if (goButtonHeld)
            {
                Audio.PlaySoundAtTransform(insSub[i], transform);
                if (i < 13)
                    lettersTextMesh[i / 2].color = textColors[7];
                yield return new WaitForSeconds(0.2608125f);
                if (i == 13)
                {
                    TestAnswer();
                }
            }
            if (i == 15 && _moduleSolved)
            {
                Audio.PlaySoundAtTransform("insCorrect", transform);
            }
        }
    }

    void TestAnswer()
    {
        int testing = 0;
        if (solutionSelections.Count != 3)
        {
            strike = Strike();
            StartCoroutine(strike);
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                if (chosenLetters.Contains(solutionSelectionLetters[i]))
                {
                    testing++;
                }
            }
            if (testing == 3)
            {
                Solve();
            }
            else
            {
                strike = Strike();
                StartCoroutine(strike);
            }
        }
    }

    IEnumerator Strike()
    {
        StringBuilder submission = new StringBuilder();
        StringBuilder cLet = new StringBuilder();
        cLet.Append("Correct letters are: ");
        for (int i = 0; i < chosenLetters.Count; i++)
        {
            cLet.Append(letters[chosenLetters[i]]);
            if (i + 1 != chosenLetters.Count)
            {
                cLet.Append(", ");
            }
        }
        for (int i = 0; i < solutionSelectionLetters.Count; i++)
        {
            submission.Append(letters[solutionSelectionLetters[i]]);
            if (i != solutionSelectionLetters.Count - 1)
            {
                submission.Append(", ");
            }
        }

        Debug.LogFormat("[Inselectables #{0}] You submitted {1} when you should have submitted {2}. Strike.", _moduleId, submission, cLet);

        allowedToPress = false;

        GetComponent<KMBombModule>().HandleStrike();
        for (int i = 0; i < lettersTextMesh.Length; i++)
        {
            lettersTextMesh[i].color = textColors[4];
        }
        goTextMesh.color = textColors[4];

        for (int i = 0; i < coloredSelections.Length; i++)
        {
            if (!chosenLetters.Contains(solvingScreenLetters[solvingTempList[i]]))
            {
                coloredSelections[solvingTempList[i]].GetComponent<MeshRenderer>().material = highlightMats[4];
            }
        }
        for (int i = 0; i < 8; i++)
        {
            if (chosenLetters.Contains(solvingScreenLetters[solvingTempList[i]]))
            {
                coloredSelections[solvingTempList[i]].SetActive(true);
                coloredSelections[solvingTempList[i]].GetComponent<MeshRenderer>().material = highlightMats[3];
            }
        }

        yield return new WaitForSeconds(1.5f);

        solutionSelectionLetters.Clear();
        ClearColoredHighlights();
        ClearColoredSelections();
        solutionSelections.Clear();
        finalLetters.Clear();

        GenerateSolution();
    }

    void Solve()
    {
        GetComponent<KMBombModule>().HandlePass();
        StringBuilder submission = new StringBuilder();
        for (int i = 0; i < chosenLetters.Count; i++)
        {
            submission.Append(letters[chosenLetters[i]]);
            if (i + 1 != chosenLetters.Count)
            {
                submission.Append(", ");
            }
        }
        Debug.LogFormat("[Inselectables #{0}] You submitted {1}. Module solved.", _moduleId, submission);

        _moduleSolved = true;
        for (int i = 0; i < lettersTextMesh.Length; i++)
        {
            lettersTextMesh[i].color = textColors[5];

        }
        goTextMesh.color = textColors[5];
        for (int i = 0; i < coloredSelections.Length; i++)
        {
            coloredSelections[i].GetComponent<MeshRenderer>().material = highlightMats[3];
        }
    }

    void GenerateSolution()
    {
        if (!(strike == null))
            StopCoroutine(strike);
        chosenLetters.Clear();
        decoyLetters.Clear();

        for (int i = 0; i < 7;)
        {
            int temp = Rnd.Range(0, 26);
            // choose 7 random letters...
            if (!chosenLetters.Contains(temp))
            {
                if (i < 3)
                // 3 of which are part of the solution...
                {
                    chosenLetters.Add(temp);
                    i++;
                }
                else if (!chosenLetters.Contains(temp) && !decoyLetters.Contains(temp))
                // and 4 of which are decoys (show up every 3 of 4 searches, cycling)
                {
                    decoyLetters.Add(temp);
                    i++;
                }
            }
        }
        StringBuilder cLet = new StringBuilder();
        StringBuilder dLet = new StringBuilder();
        for (int i = 0; i < chosenLetters.Count; i++)
        {
            cLet.Append(letters[chosenLetters[i]]);
            if (i + 1 != chosenLetters.Count)
            {
                cLet.Append(", ");
            }
        }
        for (int j = 0; j < decoyLetters.Count; j++)
        {
            dLet.Append(letters[decoyLetters[j]]);
            if (j + 1 != decoyLetters.Count)
            {
                dLet.Append(", ");
            }
        }
        // MANUAL CHALLENGE Debug.LogFormat("[Inselectables #{0}] Correct letters are: {1}.", _moduleId, cLet);
        // MANUAL CHALLENGE Debug.LogFormat("[Inselectables #{0}] Decoys are: {1}.", _moduleId, dLet);
        GenerateOthers();
    }

    void GenerateOthers()
    {
        indexLetters.Clear();
        for (int i = 0; i < chosenLetters.Count; i++)
        {
            indexLetters.Add(chosenLetters[i]);
        }
        StringBuilder rLet = new StringBuilder();

        removeFromList++;
        chosenDecoyIxs.Clear();
        while (removeFromList > 3)
        {
            removeFromList %= 4;
        }

        for (int i = 0; i < 4; i++) // choose 3 of the 4 decoy letters (fixed shuffling)
        {
            if (removeFromList != i)
            {
                chosenDecoyIxs.Add(i);
            }
        }

        for (int i = 0; i < 3; i++) // add the decoys to indexLetters
        {
            indexLetters.Add(decoyLetters[chosenDecoyIxs[i]]);
        }

        randomLetters.Clear();

        for (int i = 0; i < 2;) // choose two random letters that don't repeat
        {
            int temp = Rnd.Range(0, 26);
            if (!indexLetters.Contains(temp) && !chosenLetters.Contains(temp) && !decoyLetters.Contains(temp))
            {
                indexLetters.Add(temp);
                randomLetters.Add(temp);

                i++;
            }
        }
        rLet.Append("Random letters are: ");
        for (int i = 0; i < randomLetters.Count; i++)
        {
            rLet.Append(letters[randomLetters[i]]);
            if (i + 1 != randomLetters.Count)
            {
                rLet.Append(", ");
            }
        }
        GenerateOffsets();
    }

    void GenerateOffsets()
    {
        StringBuilder offsetted = new StringBuilder();
        offsets.Clear();
        offsetsHighlights.Clear();
        preOffsetLetters.Clear();

        for (int i = 0; i < 8; i++) //generates random offsets
        {
            int PosNeg = Rnd.Range(0, 2); //decides whether offset is positive or negative
            int randOffset = Rnd.Range(1, 8); //creates offset between 1 and 7 (never offset of 0)
            if (PosNeg == 1)
            {
                randOffset *= -1;
            }
            offsets.Add(randOffset);
            offsetsHighlights.Add((offsets[i] + 8) % 8);
        }
        finalLetters = indexLetters;
        finalLetters.Shuffle();
        for (int i = 0; i < finalLetters.Count; i++)
        {
            preOffsetLetters.Add(finalLetters[i]);
            finalLetters[i] = (finalLetters[i] - offsets[i] + 26) % 26;
            offsetted.Append(letters[finalLetters[i]]);
            if (offsets[i] > 0)
                offsetted.Append("+");
            offsetted.Append(offsets[i]);
            offsetted.Append(" (" + letters[preOffsetLetters[i]] + ")");
            if (i != 7)
                offsetted.Append(", ");
        }
        // MANUAL CHALLENGE Debug.LogFormat("[Inselectables #{0}] Generated: {1}.", _moduleId, offsetted);
        setLetters = SetLetters();
        StartCoroutine(setLetters);
    }

    void GenerateSolvingScreen()
    {
        solvingScreenLetters.Clear();
        for (int i = 0; i < 8;)
        {
            if (i < 3)
            {
                solvingScreenLetters.Add(chosenLetters[i]);
                i++;
            }
            else if (i < 7)
            {
                solvingScreenLetters.Add(decoyLetters[i - 3]);
                i++;
            }
            else
            {
                int randNum = Rnd.Range(0, 26);
                if (!chosenLetters.Contains(randNum) && !decoyLetters.Contains(randNum))
                {
                    solvingScreenLetters.Add(randNum);
                    i++;
                }
            }
        }
        GetSolvingListsForHighlights();

        solvingScreenLetters.Shuffle();
        setSolvingLetters = SetSolvingLetters();
        StartCoroutine(setSolvingLetters);
    }

    void GetSolvingListsForHighlights()
    {
        tryAgain:
        int attempts = 0;
        solvingTempList.Clear();
        for (int i = 0; i < 8;)
        {
            int randNum = Rnd.Range(0, 8);
            if (!solvingTempList.Contains(randNum) && randNum != i)
            {
                solvingTempList.Add(randNum);
                i++;
            }
            else
            {
                attempts++;
            }
            if (attempts == 50)
            {
                goto tryAgain;
            }
        }
    }

    IEnumerator SetLetters()
    {
        allowedToPress = false;
        ClearColoredHighlights();
        Audio.PlaySoundAtTransform("insPress", transform);
        for (int i = 0; i < 16; i++)
        {
            if (i < 8)
            {
                lettersTextMesh[i].text = "";
            }
            else
            {

                goTextMesh.color = textColors[0];
                lettersTextMesh[i - 8].text = letters[finalLetters[i - 8]];
                lettersTextMesh[i - 8].color = textColors[0];
            }
            yield return new WaitForSeconds(0.1f);
        }
        allowedToPress = true;
        solvingPhase = false;

    }

    IEnumerator SetSolvingLetters()
    {
        allowedToPress = false;
        ClearColoredHighlights();
        Audio.PlaySoundAtTransform("insPress", transform);
        for (int i = 0; i < 16; i++)
        {
            if (i < 8)
            {
                lettersTextMesh[i].text = "";
            }
            else
            {
                lettersTextMesh[i - 8].text = letters[solvingScreenLetters[i - 8]];
            }
            yield return new WaitForSeconds(0.1f);
        }
        allowedToPress = true;
    }


    void ClearLetters()
    {
        for (int i = 0; i < 8; i++)
        {
            lettersTextMesh[i].text = "";
        }
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "Buttons are labeled 1-8, starting at the top, going clockwise. !{0} highlight # | !{0} cycle | !{0} press #/go | !{0} hold go | !{0} colorblind";
#pragma warning restore 0414

    private bool TwitchPlaysStrike;
    IEnumerator ProcessTwitchCommand(string command)
    {
        TwitchPlaysStrike = false;
        string[] split = command.ToLowerInvariant().Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);

        if (split[0] == "colorblind" && split.Length == 1)
        {
            SetColorblindMode(!_colorblindMode);

            yield return null;
            yield break;
        }

        if (split[0] == "cycle" && split.Length == 1)
        {
            yield return new WaitForSeconds(1.0f);
            for (int i = 0; i < 8; i++)
            {
                DoHighlight(i);
                lettersTextMesh[i].color = textColors[6];
                yield return new WaitForSeconds(2.0f);
                ClearColoredHighlights();
                if (!solvingPhase)
                    lettersTextMesh[i].color = textColors[0];
                else
                    lettersTextMesh[i].color = textColors[1];
                yield return new WaitForSeconds(0.1f);
            }
        }

        else if (split[0] == "highlight")
        {
            foreach (string str in split.Skip(1))
                foreach (char c in str)
                    if (!"12345678".Contains(c))
                        yield break;
            foreach (string str in split.Skip(1))
            {
                foreach (char c in str)
                {
                    int num = c - '1';
                    yield return new WaitForSeconds(1.0f);
                    DoHighlight(num);
                    lettersTextMesh[num].color = textColors[6];
                    yield return new WaitForSeconds(3.0f);
                    ClearColoredHighlights();
                    if (!solvingPhase)
                        lettersTextMesh[num].color = textColors[0];
                    else
                        lettersTextMesh[num].color = textColors[1];
                    yield return new WaitForSeconds(0.1f);
                }
            }
            if (_moduleSolved || TwitchPlaysStrike)
                yield break;
        }

        else if (split[0] == "hold" && split.Length == 2)
        {
            if (split[1] == "go")
            {
                yield return new WaitForSeconds(1.0f);
                GoButtonPress();
                yield return new WaitForSeconds(4.5f);
                OnRelease();
            }
            else
                yield break;
            if (_moduleSolved || TwitchPlaysStrike)
                yield break;
        }

        else if (split[0] == "press" && split.Length > 1)
        {
            if (split[1] == "go" && split.Length == 2)
            {
                yield return new WaitForSeconds(0.5f);
                GoButtonPress();
                yield return new WaitForSeconds(0.1f);
                OnRelease();
            }
            else
            {
                foreach (string str in split.Skip(1))
                    foreach (char c in str)
                        if (!"12345678".Contains(c))
                            yield break;
                foreach (string str in split.Skip(1))
                    foreach (char c in str)
                    {
                        int num = c - '1';
                        yield return new WaitForSeconds(0.5f);
                        LetteredButtonPress(num);
                    }
            }
            if (_moduleSolved || TwitchPlaysStrike)
                yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        yield return new WaitForSeconds(1.0f);
        GoButtonPress();
        yield return new WaitForSeconds(4.5f);
        OnRelease();
        yield return new WaitForSeconds(1.5f);
        for (int i = 0; i < 8; i++)
        {
            if (chosenLetters.Contains(solvingScreenLetters[solvingTempList[i]]))
            {
                LetteredButtonPress(i);
                yield return new WaitForSeconds(0.2f);
            }
        }
        GoButtonPress();
        yield return new WaitForSeconds(4.5f);
        OnRelease();
    }
}
