using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UI_DialougeSystem : MonoBehaviour
{
    public Queue<string> Sentences = new Queue<string>();
    public string currentSentence;

    public TextMeshPro text;

    private NPCSentence _sentence;

    public void Ondialogue(string[] lines,NPCSentence sentence)
    {
        _sentence = sentence;

        Sentences.Clear();
        foreach (string line in lines)
        {
            Sentences.Enqueue(line);
        }

        StartCoroutine(DialougeFlow());
    }


    IEnumerator DialougeFlow()
    {
        yield return null;
        while (Sentences.Count > 0)
        {
            currentSentence = Sentences.Dequeue();

            text.text = currentSentence;

            yield return new WaitForSeconds(2f);
        }
        this.gameObject.SetActive(false);
        _sentence.TalkNPC();
    }
}
