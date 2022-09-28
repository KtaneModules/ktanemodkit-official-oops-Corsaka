using UnityEngine;
using KModkit;
using KeepCoding;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class badbonesScript : ModuleScript {
	//system
	private bool _isSolved = false;
	//module
	private int[] correctSeq; //correct sequence of notes
	private List<int> sequence = new List<int>(); //player's input sequence
	private int seqLength,badBone,goodBone,midBone,highBone,lowNoteCount=0; //length, note values, variations on low note
	private bool _skullHeld = false; //whether the user is currently moving the skull
	private Dictionary<GameObject,int> boneNotes; //dictionary assigning object to note
	private Dictionary<Vector3,GameObject> bonesPos; //dictionary assigning position to object
	private Dictionary<GameObject,int> boneConverter; //dictionary assigning object to value
	private Vector3 posNorth,posEast,posSouth,posWest; //positions of sprites
	private Vector2 mouseStartPos; //position of mouse, to control skull
	private Quaternion skullStartRot; //initial rotation of skull
	//list of primes for use in one specific function
	//yes, this will break if you make a bomb with more than 500 modules on it and the sequence length is 0, but that's such a rare edge case that i don't care.
	private int[] primes = {2,3,5,7,11,13,17,19,23,29,31,37,41,43,47,53,59,61,67,71,73,79,83,89,97,101,103,107,109,113,127,131,137,139,149,151,157,163,167,173,179,181,191,193,197,199,211,223,227,229,233,239,241,251,257,263,269,271,277,281,283,293,307,311,313,317,331,337,347,349,353,359,367,373,379,383,389,397,401,409,419,421,431,433,439,443,449,457,461,463,467,479,487,491,499};
	//eyes
	[SerializeField]
	internal KMSelectable submit,reset;
	//skull
	[SerializeField]
	internal KMSelectable skull;
	public GameObject skullPivot;
	//sprites
	public GameObject one,two,three,four;
	//lights
	public Light topBlue,bottomBlue,topRed,bottomRed;
	//sounds
	public AudioClip[] audioClips; //order: end, high, low1, low2, low3, mid, bad1, bad2, bad3, bad4

	//bombgen
	private void Start () {
		//fix lighting bug
		float scalar = transform.lossyScale.x;
		topBlue.range *= scalar;
		bottomBlue.range *= scalar;
		topRed.range *= scalar;
		bottomRed.range *= scalar;

		skullStartRot = skullPivot.transform.localRotation;
		reset.Assign(onInteract: resetSeq);
		submit.Assign(onInteract: submitSeq);
		skull.Assign(onInteract: skullHold);
		skull.Assign(onInteractEnded: skullRelease);
		assignBones();
		createSeq();
	}

	private void assignBones()
	{
		//actual value of each bone
		boneConverter = new Dictionary<GameObject,int>(){{one,1},{two,2},{three,3},{four,4}};
		//position of each bone - defined below
		bonesPos = new Dictionary<Vector3,GameObject>();
		//note of each bone - defined below
		boneNotes = new Dictionary<GameObject,int>();

		//list of possible positions
		posNorth = new Vector3(0,0,0.06f);
		posEast = new Vector3(0.06f,0,0);
		posSouth = new Vector3(0,0,-0.06f);
		posWest = new Vector3(-0.06f,0,0);
		Vector3[] positions = {posNorth,posEast,posSouth,posWest};

		System.Random rnd = new System.Random(); //creates the randomization
		var order = Enumerable.Range(0,4).OrderBy(r => rnd.Next()).ToArray(); //some code i stole that creates a range of numbers and orders them randomly
		//positions of the sprites
		one.transform.localPosition = positions[order[0]];
		two.transform.localPosition = positions[order[1]];
		three.transform.localPosition = positions[order[2]];
		four.transform.localPosition = positions[order[3]];
		
		GameObject[] boneList = {one,two,three,four};
		foreach(GameObject bone in boneList)
		{
			//store them in a dictionary so they can be accurately referred to later
			//this dictionary links positions with values
			if(bone.transform.localPosition == posNorth)
			{
				bonesPos[posNorth] = bone;
			}
			if(bone.transform.localPosition == posEast)
			{
				bonesPos[posEast] = bone;
			}	
			if(bone.transform.localPosition == posSouth)
			{
				bonesPos[posSouth] = bone;
			}
			if(bone.transform.localPosition == posWest)
			{
				bonesPos[posWest] = bone;
			}
		}

		//which bones each note is assigned to
		var rndRange = Enumerable.Range(1,4).OrderBy(r => rnd.Next()).ToArray(); //Range is (startPos,numbers)
		badBone = rndRange[0]; //both the same note
		goodBone = rndRange[1]; //both the same note
		midBone = rndRange[2];
		highBone = rndRange[3];
		Log("Bad Bone: {0}; Good Bone: {1};",badBone,goodBone);

		int[] notes = {badBone,goodBone,midBone,highBone}; //to iterate over
		foreach(int note in notes)
		{
			//store them in a dictionary so they can be accurately referred to later
			//this dictionary links values with notes
			switch(note)
			{
				case 1:
					boneNotes[one] = note;
					break;
				case 2:
					boneNotes[two] = note;
					break;
				case 3:
					boneNotes[three] = note;
					break;
				case 4:
					boneNotes[four] = note;
					break;
			}
		}
	}

	//determine sequence
	private void createSeq()
	{
		string nums = "";
		var bombInfo = Get<KMBombInfo>();
		foreach(int num in bombInfo.GetSerialNumberNumbers()) //for every digit in serial number
		{
			seqLength += num; //add value of digit to seqLength
			nums += String.Format("{0}+",num);
		}
		Log("Sequence Length: [{0}]={1}",nums.Remove(nums.Length-1,1),seqLength);
		if(seqLength == 0) //if the sum of these digits is 0
		{
			seqLength++;
			foreach(string _ in bombInfo.GetSolvedModuleNames()) //instead iterate over solved modules
			{
				seqLength++; //for every one, add 1 to seqLength
			}
			Log("Sequence Length 0! Backup Sequence Length: 1 + {0} solved modules: {1}",--seqLength,++seqLength);
		}
		Log("Sequence Length: {0}",seqLength);

		if(seqLength == 3 && bombInfo.IsIndicatorPresent(Indicator.BOB)) //special case
		{
			correctSeq = new int[3]{goodBone,midBone,highBone};
			Log("Something tells me this guitar riff isn't like the others. Hey, what's BOB doing here?");
		}
		else
		{
			correctSeq = seqRules(); //run the big ol rules determinator
		}
		Log("Correct Sequence: {0}",correctSeq);
	}

	private void resetSeq()
	{
		if(_isSolved){return;} //if solved, end function immediately
		ButtonEffect(reset,1.0f,Sound.ButtonPress);
		Log("Sequence reset.");
		sequence = new List<int>(); //otherwise, clear sequence
		PlaySound(Sound.ButtonRelease);
	}

	private void submitSeq()
    {
        if (_isSolved) { return; } //if solved, end function immediately

        ButtonEffect(reset, 1.0f, Sound.ButtonPress);
        Log("Inputted Sequence: {0}", sequence);
        Log("Correct Sequence: {0}", correctSeq);
        bool match = true;
		if(sequence.Count != seqLength)
		{
			match = false;
		}
		else
		{
			for (int i = 0; i < sequence.Count; i++)
			{
				if (sequence[i] != correctSeq[i])
				{
					match = false;
					break;
				}
			}
		}
        StartCoroutine(PlayFinal(match));
    }

    private void answerCheck(bool match)
    {
        if (match) //if they match
        {
            Solve("SOLVE! Correct sequence!");
            _isSolved = true; //stop any further interactions
        }
        else
        {
            Strike("STRIKE! Incorrect sequence!");
            PlayBad();
            sequence = new List<int>(); //reset sequence after strike
        }
    }

    private void skullHold()
	{
		//no _isSolved check as moving is fun :) (and doesn't affect anything!)
		_skullHeld = true;
		//mouseStartPos = Input.mousePosition;
		StartCoroutine(MoveSkull());
	}

	private void skullRelease()
	{
		Quaternion skullRot = skullPivot.transform.localRotation;
		_skullHeld = false;
		if(_isSolved){return;} //if solved, end function immediately
		Vector3 eulerSkullRot = skullRot.eulerAngles;

		int bone = 0;
		int note = 0;
		if(eulerSkullRot.x>20)
		{
			GameObject boneObj = bonesPos[posNorth]; //bonesPos converts position to object
			bone = boneConverter[boneObj]; //boneConverter converts object to integer
			sequence.Add(bone); //add this integer to the main sequence
			note = boneNotes[boneObj]; //boneNotes converts object to note
			PlayNote(note); //plays the note
		}
		if(eulerSkullRot.x<-20)
		{
            GameObject boneObj = bonesPos[posSouth];
            bone = boneConverter[boneObj];
            sequence.Add(bone);
			note = boneNotes[boneObj];
			PlayNote(note);
		}
		if(eulerSkullRot.z>20)
		{
            GameObject boneObj = bonesPos[posEast];
            bone = boneConverter[boneObj];
            sequence.Add(bone);
			note = boneNotes[boneObj];
			PlayNote(note);
		}
		if(eulerSkullRot.z<-20)
		{
            GameObject boneObj = bonesPos[posWest];
            bone = boneConverter[boneObj];
            sequence.Add(bone);
			note = boneNotes[boneObj];
			PlayNote(note);
		}
		if(bone!=0)
		{
			Log("{0} inputted. Current input: {1}",bone,sequence);
		}
	}

	private int[] seqRules()
	{
		var bombInfo = Get<KMBombInfo>(); //get cached bomb info
		int[] buildSeq = new int[seqLength]; //create a build sequence for use later
		int bbCount = 0; //to count bad bones modules
		bool multiRuleBool=false,badFourRuleBool=false,serialRuleBool=false,goodPlateRuleBool=false,containTwoRuleBool=false,notContainOneRuleBool=false; //bools for each rule
		bool replaceTwos=false,replaceThrees=false; //in case we are updating all future 2s/3s
		string badFourRuleLog,serialRuleLog,goodPlateRuleLog,containTwoRuleLog,notContainOneRuleLog,otherwiseLog; //logs for each rule
		badFourRuleLog=serialRuleLog=goodPlateRuleLog=containTwoRuleLog=notContainOneRuleLog=otherwiseLog="DEFAULT TEXT - THIS SHOULD NOT BE VISIBLE";
		
		//pre for multiRule
		foreach(string module in bombInfo.GetModuleNames()) //iterate over all modules
		{
			if(module == "badbones") //if their name is "badbones"
			{
				bbCount += 1; //add 1 to bad bone counter
			}
		}
		//pre for serialRule
		bool vowel = false;
		string serial = bombInfo.GetSerialNumberLetters().ToArray().Join("");
		Log(serial);
		var res = serial.Where(c => "AEIOU".Contains(c));
		if(res.Any()) //check for vowels in serial number
		{
			vowel = true; //if there are, set the vowel bool
		}
		Log(vowel);
		for(int priority=0;priority<4;priority++) //we have 4 priority layers
		{

			//multiple bad bones modules
			if((bbCount > 1) && !multiRuleBool) //if there's 2+ bad bones modules and this rule hasn't been completed before
			{
				for(int i=2;i<seqLength;i+=3) //find every 3rd digit
				{
					buildSeq[i] = 3; //replace with a 3
				}
				Log("Multiple Bad Bones Modules found. Priority: 1. Every 3rd digit set to 3");
				multiRuleBool = true; //set rule as completed
			}

			//bad bone is a 4
			else if((badBone == 4) && !badFourRuleBool) //if badBone is a 4 and this rule hasn't been completed before
			{
				switch(priority)
				{
					case 0:
						buildSeq[0] = 4; //first
						buildSeq[seqLength-1] = 4; //final
						badFourRuleLog = "First/Last digit of sequence set to 4.";
						break;
					case 1:
						for(int i=1;i<seqLength;i+=2) //find every 2nd digit
						{
							if(buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 2; //replace with a 2
							}
						}
						badFourRuleLog = "Every 2nd digit set to 2.";
						break;
				}
				Log("Bad Bone is a 4. Priority: {0}. " + badFourRuleLog,priority+1);
				badFourRuleBool = true; //set rule as completed
			}

			//serial number contains a vowel
			else if(vowel && !serialRuleBool) //if we have a vowel and this rule hasn't been completed before
			{
				switch(priority)
				{
					case 0:
						replaceTwos = true; //to be replaced later
						serialRuleLog = "Every future 2 will be set to 3.";
						break;
					case 1:
						for(int i=4;i<seqLength;i+=5) //find every 2nd digit
						{
							if(buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 4; //replace with a 4
							}
						}
						serialRuleLog = "Every 2nd digit set to 3.";
						break;
					case 2:
						for(int i=1;i<seqLength;i+=2) //we can ignore i=2 because the only way to access this rule is for every 2nd digit to already be set to 2
						{
							if(primes.Contains(i))
							{
								if(buildSeq[i] == 0) //check that it's not already assigned
								{
									buildSeq[i] = 1; //replace with a 1
								}
							}
						}
						serialRuleLog = "All prime digits set to 1.";
						break;
				}
				Log("Serial contains a vowel. Priority: {0}. " + serialRuleLog,priority+1);
				serialRuleBool = true; //set rule as completed
			}

			//good bone exceed number of port plates
			else if((goodBone > bombInfo.GetPortPlateCount()) && !goodPlateRuleBool)
			{
				switch(priority)
				{
					case 0:
						for(int i=0;i<seqLength;i++) //iterate over entire thing
						{
							switch(i%4)
							{
								//for each digit, set correctly
								case 0:
									buildSeq[i] = 1;
									break;
								case 1:
									buildSeq[i] = 2;
									break;
								case 2:
									buildSeq[i] = 3;
									break;
								case 3:
									buildSeq[i] = 4;
									break;
							}
						}
						goodPlateRuleLog = "Repeating '1234' until end of sequence.";
						break;
					case 1:
						for(int i=0;i<seqLength;i+=2)
						{
							if(buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 1; //replace with a 1
							}
						}
						goodPlateRuleLog = "Every odd digit set to 1.";
						break;
					case 2:
						for(int i=3;i<seqLength;i+=4) //find every 4th digit
						{
							if(buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 2; //replace with a 2
							}
						}
						goodPlateRuleLog = "Every 4th digit set to 2.";
						break;
					case 3:
						for(int i=0;i<seqLength;i++) //find every remaining digit
						{
							if(buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = goodBone; //replace with the good bone
							}
						}
						goodPlateRuleLog = String.Format("Every remaining digit set to {0}.",goodBone);
						break;
				}
				Log("Good Bone value ({0}) exceeds number of port plates ({1}). Priority: {2}. " + goodPlateRuleLog,goodBone,bombInfo.GetPortPlateCount(),priority+1);
				goodPlateRuleBool = true; //set rule as completed
			}

			//sequence contains a 2
			else if(buildSeq.Contains(2) && !containTwoRuleBool)
			{
				switch(priority)
				{
					case 0:
						for(int i=2;i<seqLength;i+=3) //find every 3rd digit
						{
							buildSeq[i] = 4; //replace with a 4
						}
						containTwoRuleLog = "Every 3rd digit set to 4.";
						break;
					case 1:
						replaceThrees = true; //replace all future 3s
						containTwoRuleLog = "Every future 3 will be set to 4.";
						break;
					case 2:
						buildSeq[seqLength-1] = 1; //replace final digit with 1
						containTwoRuleLog = "Final digit replaced with 1.";
						break;
					case 3:
						for(int i=0;i<seqLength;i++)
						{
							if(buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 4; //replace with a 4
							}
						}
						containTwoRuleLog = "Every remaining digit set to 4.";
						break;
				}
				Log("Sequence contains a 2. Priority: {0}. " + containTwoRuleLog,priority+1);
				containTwoRuleBool = true; //set rule as completed
			}

			//sequence does not contain a 1
			else if(!buildSeq.Contains(1) && !notContainOneRuleBool)
			{
				switch(priority)
				{
					case 0:
						for(int i=1;i<seqLength;i+=2) //find every 2nd digit
						{
							buildSeq[i] = 4; //set to 4
						}
						notContainOneRuleLog = "Every 2nd digit set to 4.";
						break;
					case 1:
						for(int i=0;i<seqLength;i++)
						{
							if(buildSeq[i] == 3) //replace all 3s
							{
								buildSeq[i] = 4; //with 4s
							}
						}
						notContainOneRuleLog = "Every 3 replaced with 4.";
						break;
					case 2:
						for(int i=0;i<seqLength;i++)
						{
							if(i<4) //replace the first 4 digits
							{
								buildSeq[i] = 2; //with a 2
							}
						}
						notContainOneRuleLog = "First 4 digits replaced with a 2.";
						break;
					case 3:
						for(int i=0;i<seqLength;i++) //iterate over remaining digits
						{
							if(buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 1; //replace with a 1
							}
						}
						notContainOneRuleLog = "Every remaining digit set to 1.";
						break;
				}
				Log("Sequence does not contain a 1. Priority: {0}. " + notContainOneRuleLog,priority+1);
				notContainOneRuleBool = true; //set rule as completed
			}

			//otherwise
			else
			{
				switch(priority)
				{
					case 0:
						for(int i=3;i<seqLength;i+=4) //find every 4th digit
						{
							buildSeq[i] = 4; //set to 4
						}
						otherwiseLog = "Every 4th digit set to 4.";
						break;
					case 1:
						for(int i=2;i<seqLength;i+=3) //find every 3rd digit
						{
							if(buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 3; //set to 3
							}
						}
						otherwiseLog = "Every 3rd digit set to 3.";
						break;
					case 2:
						for(int i=1;i<seqLength;i+=2) //find every 2nd digit
						{
							if(buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 2; //set to 2
							}
						}
						otherwiseLog = "Every 2nd digit set to 2.";
						break;
					case 3:
						for(int i=0;i<seqLength;i++) //find every remaining digit
						{
							if(buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 1; //set to 1
							}
						}
						otherwiseLog = "Every remaining digit set to 1.";
						break;
				}
				Log("No other rules apply. Priority: {0}. " + otherwiseLog,priority+1);
			}

			//log on every iteration
			Log("Current sequence: {0}",buildSeq.Join(""));
		}
		
		//replacements
		Log("Replacing all digits matching 'Replace future values of X with Y' rules:");
		for(int i=0;i<seqLength;i++)
		{
			if(replaceTwos) //if we're replacing twos
			{
				if(buildSeq[i] == 2)
				{
					buildSeq[i] = 3;
					Log("Replaced 2 (digit {0}) with 3",i+1);
					Log("Current sequence: {0}",buildSeq.Join(""));
				}
			}
			if(replaceThrees) //if we're replacing threes
			{
				if(buildSeq[i] == 3) //yes, this can happen straight after a 2 is replaced with a 3 - 3 -> 4 takes priority
				{
					buildSeq[i] = 4;
					Log("Replaced 3 (digit {0}) with 4",i+1);
					Log("Current sequence: {0}",buildSeq.Join(""));
				}
			}
			if(!(replaceTwos||replaceThrees))
			{
				Log("None! Current sequence: {0}",buildSeq.Join(""));
				break;
			}
		}

		Log("Replacing the Bad Bone values with the Good Bone value:");
		for(int i=0;i<seqLength;i++)
		{
			if(buildSeq[i] == badBone) //if value is the bad bone
			{
				buildSeq[i] = goodBone; //replace with the good bone
				Log("Replaced {0} (digit {1}) with {2}",badBone,i+1,goodBone);
			}
		}

		return buildSeq;
	}

	private void PlayLow()
	{
		switch(lowNoteCount++%3)
        {
			case 0:
				PlaySound(skullPivot.transform,"boneLow1");
				break;
			case 1:
				PlaySound(skullPivot.transform,"boneLow2");
				break;
			case 2:
				PlaySound(skullPivot.transform,"boneLow3");
				break;
        }
	}

	private void PlayMiddle()
	{
		PlaySound(skullPivot.transform,"boneMid");
	}

	private void PlayHigh()
	{
		PlaySound(skullPivot.transform,"boneHigh");
	}

	private IEnumerator PlayFinal(bool match)
	{
		foreach(int val in sequence)
		{
			if(val == goodBone||val == badBone)
			{
				PlayLow();
				switch(lowNoteCount++%3)
				{
					case 0:
						yield return new WaitForSecondsRealtime(audioClips[2].length);
						break;
					case 1:
						yield return new WaitForSecondsRealtime(audioClips[3].length);
						break;
					case 2:
						yield return new WaitForSecondsRealtime(audioClips[4].length);
						break;
				}
			}
			if(val == midBone)
			{
				PlayMiddle();
				yield return new WaitForSecondsRealtime(audioClips[5].length);
			}
			if(val == highBone)
			{
				PlayHigh();
				yield return new WaitForSecondsRealtime(audioClips[1].length);
			}
		}
		PlaySound(skullPivot.transform,"boneEnd");
		answerCheck(match);
	}

	private void PlayNote(int note)
	{
		if(note == goodBone||note == badBone)
		{
			PlayLow();
		}
		if(note == midBone)
		{
			PlayMiddle();
		}
		if(note == highBone)
		{
			PlayHigh();
		}
		if(note == 0)
		{
			Log("Default note value accessed. This is a bug.");
			throw new Exception("DEFAULT ACCESSED");
		}
	}

	private void PlayBad()
	{
		System.Random rnd = new System.Random(); //creates the randomization
		int[] badRange = Enumerable.Range(0,4).OrderBy(r => rnd.Next()).ToArray();
		int bad = badRange[0];
		switch(bad)
		{
			case 0:
				PlaySound("bad1");
				break;
			case 1:
				PlaySound("bad2");
				break;
			case 2:
				PlaySound("bad3");
				break;
			case 3:
				PlaySound("bad4");
				break;
		}
	}

	private IEnumerator MoveSkull()
	{
		skullPivot.transform.localRotation = Quaternion.Euler(22.5f,0,0);
		yield return null;
	}

	// Update is called once per frame
	void Update () {
		Quaternion skullRot = skullPivot.transform.localRotation;
		if(!_skullHeld && !(skullRot == skullStartRot))
		{
			//return skull to center
			skullPivot.transform.localRotation = Quaternion.Lerp(skullRot,skullStartRot,20.0f*Time.deltaTime);
		}
	}
}
