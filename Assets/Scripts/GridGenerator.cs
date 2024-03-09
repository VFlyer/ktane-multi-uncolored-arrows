using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridGenerator : MonoBehaviour {

	// Use this for initialization
	void Start () {
		var randomArray = Enumerable.Range(0, 100).Select(a => a % 4).ToArray().Shuffle();
		Debug.Log(Enumerable.Range(0, 10).Select(a => randomArray.Skip(10 * a).Take(10).Join("")).Join("\n"));
		Debug.Log(Enumerable.Repeat(new[] { "U", "R", "D", "L" }, 60).Select(a => a.Shuffle().Join()).Join("\n"));
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
