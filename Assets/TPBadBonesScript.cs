using KeepCoding;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TPBadBonesScript : TPScript<BadBonesScript>
{
	private Dictionary<GameObject,Vector3> posBones; //dictionary assigning position to object
	private Dictionary<int,GameObject> convertBones; //dictionary assigning object to value
	public override IEnumerator ForceSolve()
	{
		Module.sequence = Module.correctSeq.ToList();
		Module.submit.OnInteract();
		while(!Module._isSolved) { yield return true; }
	}

	public override IEnumerator Process(string command)
	{
		string[] split = command.ToLowerInvariant().Split();

		if (split.Length > 1) //if there's more than one space in the whole thing
		{
			yield return SendToChatError("Too many arguments!"); //cry
			yield break;
		}

		if(IsMatch(split[0],"reset")) { yield return ResetCommand(); }
		else if(IsMatch(split[0],"submit")) { yield return SubmitCommand(); }
		else if(IsMatch(split[0],"cb")) { yield return ColorblindCommand(); }
		else if(IsMatch(split[0],"deaf")) { yield return DeafCommand(); }
		else //for anything that isn't these keywords
		{
			yield return InputParse(split[0]); //check it against the numbers-only parser
		}
	}

	private IEnumerator InputParse(string seq)
	{
		if (seq.Any(c => c - '0' < 1 || c - '0' > 4)) //verify input is a number between 1 and 4
		{
			{ yield return SendToChatError("Input must only contain numbers between 1 and 4."); yield break; }
		}
		yield return InputSequence(seq);
	}
	private IEnumerator ResetCommand()
	{
		yield return null;
		yield return new[] { Module.reset }; //click the reset button.
	}
	private IEnumerator SubmitCommand()
	{
		yield return null;
		yield return new[] { Module.submit }; //click the submit button.
	}
	private IEnumerator ColorblindCommand()
	{
		yield return null;
		Module.ColorBlindToggle();
	}
	private IEnumerator DeafCommand()
	{
		yield return null;
		yield return new[] { Module.reset };
		yield return new[] { Module.reset };
		yield return new[] { Module.reset };
		yield return new[] { Module.submit };
	}
	private IEnumerator InputSequence(string sequence)
	{
		yield return null;
		posBones = Module.bonesPos.ToDictionary(x => x.Value, x=> x.Key); //invert dictionary 1
		convertBones = Module.boneConverter.ToDictionary(x => x.Value, x=> x.Key); //invert dictionary 2
		foreach (char numChar in sequence)
		{
			Module.skull.OnInteract(); //pick up the skull
			int number = numChar - '0';
			Vector3 vPos = posBones[convertBones[number]]; //get vector position
			Module.skullPivot.transform.localEulerAngles = new Vector3(vPos.z,0,-vPos.x)*350; //has to be mangled like shit because rotation is stupid and has x = north
			Module.skull.OnInteractEnded(); //put down the skull
			yield return new WaitForSecondsRealtime(0.18f); //wait for sound to play
		}
	}
}