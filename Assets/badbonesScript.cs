using UnityEngine;
using KModkit;
using KeepCoding;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class BadBonesScript : ModuleScript {
	//system
	internal global::System.Boolean _isSolved = false;
	private global::System.Boolean _isPlayingAudio = false;
	
	//module
	internal int[] correctSeq; //correct sequence of notes
	internal List<int> sequence = new List<int>(); //player's input sequence
	private int seqLength,badBone,goodBone,midBone,highBone,lowNoteCount=0; //length, note values, variations on low note
	private bool _skullHeld = false; //whether the user is currently moving the skull
	private bool _deafMode = false; //whether deafmode is enabled
	private bool _sequenceZero = false; //whether sequence length equals zero
	private Dictionary<GameObject,int> boneNotes; //dictionary assigning object to note
	internal Dictionary<Vector3,GameObject> bonesPos; //dictionary assigning position to object
	internal Dictionary<GameObject,int> boneConverter; //dictionary assigning object to value
	private Vector3 posNorth,posEast,posSouth,posWest; //positions of sprites
	private Vector2 mouseStartPos; //position of mouse, to control skull
	private Quaternion skullStartRot; //initial rotation of skull
	private GameObject[] boneList; //list of bones
	private int deafCount; //for use in triggering deaf mode
	//list of primes for use in one specific function; max prime is 53 as 999999 is maximum serial number, which sums to 54
	private readonly int[] primes = {2,3,5,7,11,13,17,19,23,29,31,37,41,43,47,53};

	//component gets
	private KMBombInfo bombInfo; //we need to check bombInfo later
	public Material redCBMat; //red colorblind
	public Material blueCBMat; //blue colorblind
	//eyes
	[SerializeField]
	internal KMSelectable submit,reset; //eye selectables
	public GameObject red,blue; //eyes
	//skull
	[SerializeField]
	internal KMSelectable skull; //skull selectable
	public GameObject skullPivot; //thing that moves
	//sprites
	public GameObject one,two,three,four; //your bones
	//lights
	public Light topBlue,bottomBlue,topRed,bottomRed; //eye lights
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

		boneList = new GameObject[]{one,two,three,four};
		bombInfo = Get<KMBombInfo>();
		if (Get<KMColorblindMode>().ColorblindModeActive) { ColorBlindToggle(); }

		skullStartRot = skullPivot.transform.localRotation;
		reset.Assign(onInteract: ResetSeq);
		submit.Assign(onInteract: SubmitSeq);
		skull.Assign(onInteract: SkullHold);
		skull.Assign(onInteractEnded: SkullRelease);
		Log("Beginning setup:");
		AssignBones();
		MixEyes();
		CreateSeq();
	}

	//module generation
	private void AssignBones()
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

		int[] order = Enumerable.Range(0,4).ToArray().Shuffle().ToArray(); //creates a range of numbers and orders them randomly
		//positions of the sprites
		one.transform.localPosition = positions[order[0]];
		two.transform.localPosition = positions[order[1]];
		three.transform.localPosition = positions[order[2]];
		four.transform.localPosition = positions[order[3]];
		

		foreach(GameObject bone in boneList)
		{
			//store them in a dictionary so they can be accurately referred to later
			//this dictionary links positions with values
			if(bone.transform.localPosition == posNorth)
			{
				bonesPos[posNorth] = bone;
				Log("North bone: {0}",boneConverter[bone]);
			}
			if(bone.transform.localPosition == posEast)
			{
				bonesPos[posEast] = bone;
				Log("East bone: {0}",boneConverter[bone]);
			}	
			if(bone.transform.localPosition == posSouth)
			{
				bonesPos[posSouth] = bone;
				Log("South bone: {0}",boneConverter[bone]);
			}
			if(bone.transform.localPosition == posWest)
			{
				bonesPos[posWest] = bone;
				Log("West bone: {0}",boneConverter[bone]);
			}
		}

		//which bones each note is assigned to
		int[] rndRange = Enumerable.Range(1,4).ToArray().Shuffle().ToArray(); //Range is (startPos,numbers)
		badBone = rndRange[0]; //both the same note
		goodBone = rndRange[1]; //both the same note
		midBone = rndRange[2];
		highBone = rndRange[3];
		Log("Bad Bone: {0}; Good Bone: {1};",badBone,goodBone);
		Log("Mid bone: {0}; High bone {1};",midBone,highBone);

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

	private void MixEyes()
	{
		Vector3 posLeft = new Vector3(-0.00038132f,0.00075f,0);
		Vector3 posRight = new Vector3(0.00038132f,0.00075f,0);

		if(badBone > goodBone) //be cheeky. randomly generate the badbone and position red/blue according to that.
		{
			red.transform.localPosition = posLeft;
			blue.transform.localPosition = posRight;
		}
	}

	//glorified function that just triggers other functions
	private void CreateSeq()
	{
		string nums = "";
		foreach(int num in bombInfo.GetSerialNumberNumbers()) //for every digit in serial number
		{
			seqLength += num; //add value of digit to seqLength
			nums += String.Format("{0}+",num);
		}
		Log("Sequence Length: [{0}]={1}",nums.Remove(nums.Length-1,1),seqLength);
		if(seqLength == 0) //if the sum of these digits is 0
		{
			_sequenceZero = true;
			seqLength = 1;
			Log("Sequence Length 0! Backup Sequence Length: 1 + solved modules.");
		}
		else
		{
			Log("Sequence Length: {0}",seqLength);
		}

		correctSeq = SeqRules(); //run the big ol rules determinator
		Log("Correct Sequence: {0}",correctSeq.Join(""));
	}

	//buttons
	private void ResetSeq()
	{
		
		if (_isSolved || _isPlayingAudio) { return; } //if solved/playing audio, end function immediately
		if (_deafMode == false) { deafCount += 1; }
		ButtonEffect(reset,1.0f,Sound.ButtonPress);
		Log("Sequence reset.");
		sequence = new List<int>(); //otherwise, clear sequence
		PlaySound(reset.transform,Sound.ButtonRelease);
	}

	private void SubmitSeq()
	{
		if (_isSolved || _isPlayingAudio) { return; } //if solved/playing audio, end function immediately
		if (deafCount == 3 && _deafMode == false) //enable deafmode
		{
			Log("Deaf mode enabled.");
			_deafMode = true;
			DeafMode();
		}
		deafCount = 0;
		ButtonEffect(reset, 1.0f, Sound.ButtonPress); //play the sound of a button press (i don't think this exists)
		bool match = true; //assume match is true
		if(sequence.Count != seqLength) //if the sequence is the wrong length
		{
			match = false; //it's obviously false
		}
		else
		{
			for (int i = 0; i < sequence.Count; i++) //iterate over our sequence
			{
				if (sequence[i] != correctSeq[i]) //if even one of them isn't correct
				{
					match = false; //you're wrong and will be issued a strike
					break;
				}
			}
		}

		if(sequence.Count == 0) { return; } //please just stop throwing unhandled exceptions when i submit things

		Log("Inputted Sequence: {0}", sequence.Join(""));
		Log("Correct Sequence: {0}", correctSeq.Join(""));
		if(sequence[0] == goodBone && sequence[1] == midBone && sequence[2] == highBone && match && seqLength == 3) //special case
		{
			_isPlayingAudio = true; //don't let player interrupt
			PlaySound(skullPivot.transform,"badBonesSpecial"); //play the special noise :)
			Solve("SOLVE! Correct sequence!");
			_isSolved = true;
			_isPlayingAudio = false;
		}
		else //all other cases
		{
			_isPlayingAudio = true;
			StartCoroutine(PlayFinal(match));
		}
	}

	//answer validation
	private void AnswerCheck(bool match)
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
		_isPlayingAudio = false;
	}

	//skull control
	private void SkullHold()
	{
		//no _isSolved check as moving is fun :) (and doesn't affect anything!)
		_skullHeld = true;
		deafCount = 0;
		mouseStartPos = Input.mousePosition;
	}

	private void SkullRelease()
	{
		_skullHeld = false;
		if(_isSolved){return;} //if solved, end function immediately
		if(_isPlayingAudio){return;} //if playing audio, end function immediately
		Transform skullTransform = skullPivot.transform; //get transform
		Vector3 eulerSkullRot = skullTransform.localEulerAngles; //convert rotation to something that isn't bullshit difficult to understand

		int bone = 0;
		int note = 0;
		GameObject boneObj;
		if(22.6f >= eulerSkullRot.x && eulerSkullRot.x >= 17.5f) //bound is 22.6f because Clamp doesn't perfectly clamp to 22.5f
		{
			boneObj = bonesPos[posNorth]; //bonesPos converts position to object
		}
		else if(337.4f <= eulerSkullRot.x && eulerSkullRot.x <= 342.5f)
		{
			boneObj = bonesPos[posSouth];
		}
		else if(337.4f <= eulerSkullRot.z && eulerSkullRot.z <= 342.5f)
		{
			boneObj = bonesPos[posEast];
		}
		else if(22.6f >= eulerSkullRot.z && eulerSkullRot.z >= 17.5f)
		{
			boneObj = bonesPos[posWest];
		}
		else {return;}

		bone = boneConverter[boneObj]; //boneConverter converts object to integer
		sequence.Add(bone); //add this integer to the input sequence
		note = boneNotes[boneObj]; //boneNotes converts object to note
		PlayNote(note); //plays the note
		if (_deafMode) { boneObj.GetComponent<SpriteRenderer>().color = Color.cyan; } //if deaf change colour

		if(bone!=0) //if they've released it above a bone
		{
			Log("{0} inputted. Current input: {1}",bone,sequence.Join(""));
		}
	}

	//note players
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
		lowNoteCount = 0;
		foreach(int val in sequence)
		{
			if(val == goodBone||val == badBone)
			{
				PlayLow();
				switch(lowNoteCount++%3)
				{
					case 0:
						yield return new WaitForSecondsRealtime(audioClips[2].length*0.9f);
						break;
					case 1:
						yield return new WaitForSecondsRealtime(audioClips[3].length*0.9f);
						break;
					case 2:
						yield return new WaitForSecondsRealtime(audioClips[4].length*0.8f);
						break;
				}
			}
			if(val == midBone)
			{
				PlayMiddle();
				yield return new WaitForSecondsRealtime(audioClips[5].length*0.9f);
			}
			if(val == highBone)
			{
				PlayHigh();
				yield return new WaitForSecondsRealtime(audioClips[1].length*0.9f);
			}
		}
		PlaySound(skullPivot.transform,"boneEnd");
		AnswerCheck(match);
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
			Log("Default note value accessed.",LogType.Error);
			throw new Exception("DEFAULT ACCESSED");
		}
	}

	private void PlayBad()
	{
		int[] badRange = Enumerable.Range(0,4).ToArray().Shuffle().ToArray();
		int bad = badRange[0];
		switch(bad)
		{
			case 0:
				PlaySound(skullPivot.transform,"bad1");
				break;
			case 1:
				PlaySound(skullPivot.transform,"bad2");
				break;
			case 2:
				PlaySound(skullPivot.transform,"bad3");
				break;
			case 3:
				PlaySound(skullPivot.transform,"bad4");
				break;
		}
	}

	//accessibility functions
	internal void DeafMode()
	{
		bool alreadySet = true; //for aiding randomisation
		if (UnityEngine.Random.value < 0.5) { alreadySet = false; } //half of the time set it false
		foreach (GameObject bone in boneList) //iterate list
		{
			if (boneNotes[bone] == goodBone || boneNotes[bone] == badBone) //if it's a good or bad bone
			{
				if (alreadySet) { BonePosUpdate(bone,posNorth); alreadySet = false; } //update north and toggle alreadySet
				else { BonePosUpdate(bone,posSouth); alreadySet = true; } //update south and toggle alreadySet
			}
		}
	}

	private void BonePosUpdate(GameObject chosenBone,Vector3 vertPos) //for use in func above
	{
		GameObject vertBone = bonesPos[vertPos]; //find the north bone
		Vector3 posOrig = chosenBone.transform.localPosition; //get original bone's position
		if (vertBone != chosenBone) //if they're not the same
		{
			chosenBone.transform.localPosition = vertPos; //set original to north
			vertBone.transform.localPosition = posOrig; //set north to original
			bonesPos[vertPos] = chosenBone; //update them in the dictionary
			bonesPos[posOrig] = vertBone; //so things aren't fucked
			Log("Bone {0} ({1}) swapped position with bone {2}.",boneNotes[chosenBone],boneNotes[chosenBone]==goodBone?"Good Bone":"Bad Bone",boneNotes[vertBone]);
		} //if they are the same, ignore it
	}

	internal void ColorBlindToggle()
	{
		Renderer redRend = red.GetComponent<Renderer>();
		redRend.material = redCBMat;
		Renderer blueRend = blue.GetComponent<Renderer>();
		blueRend.material = blueCBMat;
		Log("Colorblind mode enabled.");
	}

	//sequence determination
	private int[] SeqRules()
	{
		KMBombInfo bombInfo = Get<KMBombInfo>(); //get cached bomb info
		int[] buildSeq = new int[seqLength]; //create a build sequence for use later
		int bbCount = 0; //to count bad bones modules
		bool multiRuleBool = false, badFourRuleBool = false, serialRuleBool = false, goodPlateRuleBool = false, containTwoRuleBool = false, notContainOneRuleBool = false; //bools for each rule
		bool replaceTwos = false, replaceThrees = false; //in case we are updating all future 2s/3s
		string badFourRuleLog, serialRuleLog, goodPlateRuleLog, containTwoRuleLog, notContainOneRuleLog, otherwiseLog; //logs for each rule
		badFourRuleLog = serialRuleLog = goodPlateRuleLog = containTwoRuleLog = notContainOneRuleLog = otherwiseLog = "DEFAULT TEXT - THIS SHOULD NOT BE VISIBLE";

		//pre for multiRule
		foreach (string module in bombInfo.GetModuleNames()) //iterate over all modules
		{
			if (module == "Bad Bones") //if their name is "badbones"
			{
				bbCount += 1; //add 1 to bad bone counter
			}
		}
		//pre for serialRule
		bool vowel = false;
		string serial = bombInfo.GetSerialNumberLetters().ToArray().Join("");
		var res = serial.Where(c => "AEIOU".Contains(c));
		if (res.Any()) //check for vowels in serial number
		{
			vowel = true; //if there are, set the vowel bool
		}
		for (int priority = 0; priority < 4; priority++) //we have 4 priority layers
		{
			//multiple bad bones modules
			if ((bbCount > 1) && !multiRuleBool) //if there's 2+ bad bones modules and this rule hasn't been completed before
			{
				for (int i = 2; i < seqLength; i += 3) //find every 3rd digit
				{
					buildSeq[i] = 3; //replace with a 3
				}
				Log("Multiple Bad Bones Modules found. Priority: 1. Every 3rd digit set to 3");
				multiRuleBool = true; //set rule as completed
			}

			//bad bone is a 4
			else if ((badBone == 4) && !badFourRuleBool) //if badBone is a 4 and this rule hasn't been completed before
			{
				switch (priority)
				{
					case 0:
						buildSeq[0] = 4; //first
						buildSeq[seqLength - 1] = 4; //final
						badFourRuleLog = "First/Last digit of sequence set to 4.";
						break;
					case 1:
						for (int i = 1; i < seqLength; i += 2) //find every 2nd digit
						{
							if (buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 2; //replace with a 2
							}
						}
						badFourRuleLog = "Every 2nd digit set to 2.";
						break;
				}
				Log("Bad Bone is a 4. Priority: {0}. " + badFourRuleLog, priority + 1);
				badFourRuleBool = true; //set rule as completed
			}

			//serial number contains a vowel
			else if (vowel && !serialRuleBool) //if we have a vowel and this rule hasn't been completed before
			{
				switch (priority)
				{
					case 0:
						replaceTwos = true; //to be replaced later
						serialRuleLog = "Every future 2 will be set to 3.";
						break;
					case 1:
						for (int i = 1; i < seqLength; i += 2) //find every 2nd digit
						{
							if (buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 3; //replace with a 3
							}
						}
						serialRuleLog = "Every 2nd digit set to 3.";
						break;
					case 2:
						if (seqLength >= 2) //does nothing if sequence length == 1; this prevents a runtime error
						{
							if (buildSeq[1] == 0)
							{
								buildSeq[1] = 1; //replace position 2 (index 1) with a 1
							}
							for (int i = 2; i < seqLength; i += 2) //check every odd digit (iterate over evens)
							{
								if (primes.Contains(i-1)) //if checked odd digit is prime
								{
									if (buildSeq[i] == 0) //check that it's not already assigned
									{
										buildSeq[i] = 1; //replace with a 1
									}
								}
							}
							serialRuleLog = "All prime digits set to 1.";
						}
						else
						{
							serialRuleLog = "No prime digits to set to 1.";
						}
						break;
				}
				Log("Serial contains a vowel. Priority: {0}. " + serialRuleLog, priority + 1);
				serialRuleBool = true; //set rule as completed
			}

			//good bone exceed number of port plates
			else if ((goodBone > bombInfo.GetPortPlateCount()) && !goodPlateRuleBool)
			{
				switch (priority)
				{
					case 0:
						for (int i = 0; i < seqLength; i++) //iterate over entire thing
						{
							switch (i % 4)
							{
								//for each digit, set correctly
								case 0:
									buildSeq[i] = 3;
									break;
								case 1:
									buildSeq[i] = 1;
									break;
								case 2:
									buildSeq[i] = 2;
									break;
								case 3:
									buildSeq[i] = 4;
									break;
							}
						}
						goodPlateRuleLog = "Repeating '3124' until end of sequence.";
						break;
					case 1:
						for (int i = 0; i < seqLength; i += 2)
						{
							if (buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 1; //replace with a 1
							}
						}
						goodPlateRuleLog = "Every odd digit set to 1.";
						break;
					case 2:
						for (int i = 3; i < seqLength; i += 4) //find every 4th digit
						{
							if (buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 2; //replace with a 2
							}
						}
						goodPlateRuleLog = "Every 4th digit set to 2.";
						break;
					case 3:
						for (int i = 0; i < seqLength; i++) //find every remaining digit
						{
							if (buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = goodBone; //replace with the good bone
							}
						}
						goodPlateRuleLog = String.Format("Every remaining digit set to {0}.", goodBone);
						break;
				}
				Log("Good Bone value ({0}) exceeds number of port plates ({1}). Priority: {2}. " + goodPlateRuleLog, goodBone, bombInfo.GetPortPlateCount(), priority + 1);
				goodPlateRuleBool = true; //set rule as completed
			}

			//sequence contains a 2
			else if (buildSeq.Contains(2) && !containTwoRuleBool)
			{
				switch (priority)
				{
					case 0:
						for (int i = 2; i < seqLength; i += 3) //find every 3rd digit
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
						buildSeq[seqLength - 1] = 1; //replace final digit with 1
						containTwoRuleLog = "Final digit replaced with 1.";
						break;
					case 3:
						for (int i = 0; i < seqLength; i++)
						{
							if (buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 4; //replace with a 4
							}
						}
						containTwoRuleLog = "Every remaining digit set to 4.";
						break;
				}
				Log("Sequence contains a 2. Priority: {0}. " + containTwoRuleLog, priority + 1);
				containTwoRuleBool = true; //set rule as completed
			}

			//sequence does not contain a 1
			else if (!buildSeq.Contains(1) && !notContainOneRuleBool)
			{
				switch (priority)
				{
					case 0:
						for (int i = 1; i < seqLength; i += 2) //find every 2nd digit
						{
							buildSeq[i] = 4; //set to 4
						}
						notContainOneRuleLog = "Every 2nd digit set to 4.";
						break;
					case 1:
						for (int i = 0; i < seqLength; i++)
						{
							if (buildSeq[i] == 3) //replace all 3s
							{
								buildSeq[i] = 4; //with 4s
							}
						}
						notContainOneRuleLog = "Every 3 replaced with 4.";
						break;
					case 2:
						for (int i = 0; i < seqLength; i++)
						{
							if (i < 4) //replace the first 4 digits
							{
								buildSeq[i] = 2; //with a 2
							}
						}
						notContainOneRuleLog = "First 4 digits replaced with a 2.";
						break;
					case 3:
						for (int i = 0; i < seqLength; i++) //iterate over remaining digits
						{
							if (buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 1; //replace with a 1
							}
						}
						notContainOneRuleLog = "Every remaining digit set to 1.";
						break;
				}
				Log("Sequence does not contain a 1. Priority: {0}. " + notContainOneRuleLog, priority + 1);
				notContainOneRuleBool = true; //set rule as completed
			}

			//otherwise
			else
			{
				switch (priority)
				{
					case 0:
						for (int i = 3; i < seqLength; i += 4) //find every 4th digit
						{
							buildSeq[i] = 4; //set to 4
						}
						otherwiseLog = "Every 4th digit set to 4.";
						break;
					case 1:
						for (int i = 2; i < seqLength; i += 3) //find every 3rd digit
						{
							if (buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 3; //set to 3
							}
						}
						otherwiseLog = "Every 3rd digit set to 3.";
						break;
					case 2:
						for (int i = 1; i < seqLength; i += 2) //find every 2nd digit
						{
							if (buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 2; //set to 2
							}
						}
						otherwiseLog = "Every 2nd digit set to 2.";
						break;
					case 3:
						for (int i = 0; i < seqLength; i++) //find every remaining digit
						{
							if (buildSeq[i] == 0) //check that it's not already assigned
							{
								buildSeq[i] = 1; //set to 1
							}
						}
						otherwiseLog = "Every remaining digit set to 1.";
						break;
				}
				Log("No other rules apply. Priority: {0}. " + otherwiseLog, priority + 1);
			}

			//log on every iteration
			Log("Current sequence: {0}", buildSeq.Join(""));
		}

		//replacements
		Log("Replacing all digits matching 'Replace future values of X with Y' rules:");
		for (int i = 0; i < seqLength; i++)
		{
			if (replaceTwos) //if we're replacing twos
			{
				if (buildSeq[i] == 2)
				{
					buildSeq[i] = 3;
					Log("Replaced 2 (digit {0}) with 3", i + 1);
					Log("Current sequence: {0}", buildSeq.Join(""));
				}
			}
			if (replaceThrees) //if we're replacing threes
			{
				if (buildSeq[i] == 3) //yes, this can happen straight after a 2 is replaced with a 3. 3 -> 4 triggers afterwards, so if both are active, 2 -> 4
				{
					buildSeq[i] = 4;
					Log("Replaced 3 (digit {0}) with 4", i + 1);
					Log("Current sequence: {0}", buildSeq.Join(""));
				}
			}
			if (!(replaceTwos || replaceThrees))
			{
				Log("None! Current sequence: {0}", buildSeq.Join(""));
				break;
			}
		}

		//sequence mods
		buildSeq = SequenceMods(bombInfo, buildSeq);

		Log("Replacing the Bad Bone ({0}) with the Good Bone ({1}):", badBone, goodBone);
		for (int i = 0; i < seqLength; i++)
		{
			if (buildSeq[i] == badBone) //if value is the bad bone
			{
				buildSeq[i] = goodBone; //replace with the good bone
				Log("Replaced {0} (digit {1}) with {2}", badBone, i + 1, goodBone);
			}
		}
		Log("Done.");

		return buildSeq;
	}

	private int[] SequenceMods(KMBombInfo bombInfo, int[] modSeq)
	{
		Log("Checking sequence modifiers:");
		//no ports
		if (bombInfo.GetPortCount() == 0)
		{
			string portLog = "DEFAULT TEXT - SHOULD NOT BE VISIBLE.";
			int count = badBone + goodBone; //preset count as being the good/bad bone values
			for (int i = 0; i < seqLength; i++)
			{
				if ((modSeq[i] == goodBone) || (modSeq[i] == badBone)) //check if they're good/bad bones
				{
					count += 1; //count total of both
				}
			}
			if (count > seqLength) //if the count+goodval+badval > sequence length
			{
				modSeq = modSeq.Reverse();
				portLog = "Reversing entire sequence.";
			}
			else
			{
				portLog = "No action taken.";
			}
			Log("No ports found. Count: [{0}+{1}+{2}={3}] {4} {5} (sequence length); " + portLog, count - badBone - goodBone, badBone, goodBone, count, (count > seqLength) ? ">" : "<", seqLength);
			Log("Current sequence: {0}", modSeq.Join(""));
		}

		//more letters than numbers
		if (bombInfo.GetSerialNumberLetters().Count() > bombInfo.GetSerialNumberNumbers().Count())
		{
			for (int i = 0; i < seqLength; i++)
			{
				if (IsPowerOfTwo(i + 1))
				{
					switch (modSeq[i])
					{
						case 1:
							modSeq[i] = 4;
							break;
						case 2:
							modSeq[i] = 3;
							break;
						case 3:
							modSeq[i] = 2;
							break;
						case 4:
							modSeq[i] = 1;
							break;
					}
				}
			}
			Log("More letters than numbers in serial number. Replacing power of 2 positions with opposite value.");
			Log("Current sequence: {0}", modSeq.Join(""));
		}

		//more than 2 batteries
		if (bombInfo.GetBatteryCount() > 2)
		{
			int tempVal;
			for (int i = 0; i < seqLength; i++)
			{
				if (i < 4)
				{
					tempVal = (modSeq[i] + 2) % 5;
					if (tempVal == 0)
					{
						tempVal = goodBone;
					}
					modSeq[i] = tempVal;
				}
			}
			Log("More than 2 batteries. Adjusting positions 1-4: add 2, modulo 5, replace 0s with Good Bone ({0}).", goodBone);
			Log("Current sequence: {0}", modSeq.Join(""));
		}

		//bad bone even
		if (badBone % 2 == 0)
		{
			if (seqLength > 3)
			{
				int startIndex = 2;
				int endIndex = Math.Min(seqLength,8);
				while(startIndex < endIndex)
				{
					int temp = modSeq[startIndex];
					modSeq[startIndex] = modSeq[endIndex-1];
					modSeq[endIndex-1] = temp;
					startIndex++;
					endIndex--;
				}
				Log("Bad Bone is even. Reversing digits 3-{0}", Math.Min(seqLength, 8));
				Log("Current sequence: {0}", modSeq.Join(""));
			}
		}

		//sequence length > 10
		if (seqLength > 10)
		{
			modSeq[7] = 2; //B
			modSeq[8] = 1; //A
			modSeq[9] = 4; //D
			Log("Sequence length exceeds 10. Replacing digits 8, 9, and 10 with 2, 1, and 4 respectively.");
			Log("Current sequence: {0}", modSeq.Join(""));
		}

		//BAAAAD TO THE BONE
		if (seqLength == 5 && bonesPos[posNorth] == one && bonesPos[posEast] == two && bonesPos[posSouth] == three && bonesPos[posWest] == four)
		{
			modSeq = new int[5] { goodBone, goodBone, highBone, goodBone, midBone }; //
			Log("North bone is 1 & bone order is clockwise & sequence length is 5. We're Bad to the Bone!");
			Log("Current sequence: {0}", modSeq.Join(""));
		}

		//BOB???????????
		if (seqLength == 3 && bombInfo.IsIndicatorPresent("BOB"))
		{
			modSeq = new int[3] { goodBone, midBone, highBone }; //generate the smoke on the water riff
			Log("Sequence length is 3 and Indicator BOB present. This riff sounds familiar...");
			Log("Current sequence: {0}", modSeq.Join(""));
		}

		Log("All sequence modifiers complete.");
		return modSeq;
	}

	private bool IsPowerOfTwo(int x) //for use in func above
	{
		return (x & (x - 1)) == 0;
	}

	// Update is called once per frame
	void Update () {
		Quaternion skullRot = skullPivot.transform.localRotation;
		if(!_skullHeld && !(skullRot == skullStartRot))
		{
			//return skull to center
			skullPivot.transform.localRotation = Quaternion.Lerp(skullRot,skullStartRot,0.66f);
		}
		if(_skullHeld)
		{
			float xMouse = mouseStartPos.x - Input.mousePosition.x; //mousePos x needs to be inverted - +x = left. apparently.
			float yMouse = Input.mousePosition.y - mouseStartPos.y; //mousePos y does not - +y = up. for some reason.
			Vector3 currentRot = new Vector3(yMouse,0,xMouse); //rotation is stupid. +x is up. +z is right.
			Vector3 clampedRot = Vector3.ClampMagnitude(currentRot,22.5f);
			skullPivot.transform.localRotation = Quaternion.Euler(clampedRot);
		}
		if(_deafMode)
		{
			foreach (GameObject boneObject in boneList)
			{
				SpriteRenderer boneRend = boneObject.GetComponent<SpriteRenderer>();
				if (boneRend.color != Color.white)
				{
					float redColor = boneRend.color.r + 0.04f;
					boneRend.color = new Color(redColor, 1.0f, 1.0f, 1.0f);
				}
			}
		}

		if(bombInfo.GetSolvedModuleNames().Count > (seqLength-1) && _sequenceZero)
		{
			seqLength++;
			correctSeq = SeqRules(); //run the big ol rules determinator
			Log("Correct Sequence: {0}",correctSeq.Join(""));
		}
	}
}
