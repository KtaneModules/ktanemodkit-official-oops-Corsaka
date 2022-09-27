using UnityEngine;
using KModkit;
using KeepCoding;
using System;
using System.Linq;
using System.Collections.Generic;

public class badbonesScript : ModuleScript {
	//system
	private bool _isSolved = false;
	//module
	private string sequence = "";
	private string correctSeq = "";
	private int seqLength,badBone,goodBone,midBone,highBone;
	private bool _skullHeld = false;
	private float speed = 20f;
	//list of primes for use in one specific function
	//yes, this will break if you make a bomb with more than 500 modules on it and the sequence length is 0, but that's such a rare edge case that i don't care.
	private int[] primes = {2,3,5,7,11,13,17,19,23,29,31,37,41,43,47,53,59,61,67,71,73,79,83,89,97,101,103,107,109,113,127,131,137,139,149,151,157,163,167,173,179,181,191,193,197,199,211,223,227,229,233,239,241,251,257,263,269,271,277,281,283,293,307,311,313,317,331,337,347,349,353,359,367,373,379,383,389,397,401,409,419,421,431,433,439,443,449,457,461,463,467,479,487,491,499};
	//eyes
	[SerializeField]
	internal KMSelectable submit,reset;
	public GameObject red,blue;
	//skull
	[SerializeField]
	internal KMSelectable skull;
	public GameObject skullPivot;
	//sprites
	public GameObject one,two,three,four;

	//bombgen
	private void Start () {
		reset.Assign(onInteract: resetSeq);
		submit.Assign(onInteract: submitSeq);
		skull.Assign(onInteract: skullMove);
		skull.Assign(onInteractEnded: skullRelease);
		assignBones();
		createSeq();
	}

	void assignBones()
	{
		Vector3 posNorth = new Vector3(0,0,0.06f);
		Vector3 posEast = new Vector3(0.06f,0,0);
		Vector3 posSouth = new Vector3(0,0,-0.06f);
		Vector3 posWest = new Vector3(-0.06f,0,0);
		Vector3[] positions = {posNorth,posEast,posSouth,posWest};

		System.Random rnd = new System.Random();
		var order = Enumerable.Range(1, 4).OrderBy(r => rnd.Next()).ToArray();
		one.transform.position = positions[order[0]];
		two.transform.position = positions[order[1]];
		three.transform.position = positions[order[2]];
		four.transform.position = positions[order[3]];

		var rndRange = Enumerable.Range(1, 4).OrderBy(r => rnd.Next()).ToArray();
		badBone = rndRange[0];
		goodBone = rndRange[1];
		midBone = rndRange[2];
		highBone = rndRange[3];
	}

	//determine sequence
	void createSeq()
	{
		var bombInfo = Get<KMBombInfo>();
		foreach(int num in bombInfo.GetSerialNumberNumbers()) //for every digit in serial number
		{
			seqLength += num; //add value of digit to seqLength
		}
		if(seqLength == 0) //if the sum of these digits is 0
		{
			seqLength++;
			foreach(string _ in bombInfo.GetSolvedModuleNames()) //instead iterate over solved modules
			{
				seqLength++; //for every one, add 1 to seqLength
			}
		}
		Log("Sequence Length: {0}",seqLength);

		if(seqLength == 3 && bombInfo.IsIndicatorPresent(Indicator.BOB)) //special case
		{
			correctSeq = String.Format("{0}{1}{2}",goodBone,midBone,highBone); //TODO: change this to low-middle-high
			Log("Something tells me this guitar riff isn't like the others. Hey, what's BOB doing here?");
		}
		else
		{
			correctSeq = seqRules(); //run the big ol rules determiner
		}
		Log("Correct Sequence: {0}",correctSeq);
	}

	private void resetSeq()
	{
		if(_isSolved){return;} //if solved, end function immediately
		sequence = ""; //otherwise, clear sequence
	}

	private void submitSeq()
	{
		if(_isSolved){return;} //if solved, end function immediately

		Log("Inputted Sequence: {0}",sequence);
		if(sequence == correctSeq) //if they match
		{
			Solve("SOLVE! Correct sequence!");
			_isSolved = true; //stop any further interactions
		}
		else
		{
			Strike("STRIKE! Incorrect sequence!");
		}
	}

	private void skullMove()
	{
		_skullHeld = true;
		//no _isSolved check as moving is fun :) (and doesn't affect anything)
	}

	private void skullRelease()
	{
		_skullHeld = false;
		if(_isSolved){return;} //if solved, end function immediately
	}

	private string seqRules()
	{
		var bombInfo = Get<KMBombInfo>(); //get cached bomb info
		var buildSeq = new List<char>(new char[seqLength]); //create a build sequence for use later
		int bbCount = 0; //to count bad bones modules
		bool multiRuleBool=false,badFourRuleBool=false,serialRuleBool=false,goodPlateRuleBool=false,containTwoRuleBool=false,notContainOneRuleBool=false; //bools for each rule
		bool replaceTwos=false,replaceThrees=false; //in case we are updating all future 2s/3s
		string badFourRuleLog,serialRuleLog,goodPlateRuleLog,containTwoRuleLog,notContainOneRuleLog,otherwiseLog; //logs for each rule
		badFourRuleLog=serialRuleLog=goodPlateRuleLog=containTwoRuleLog=notContainOneRuleLog=otherwiseLog="DEFAULT TEXT - THIS SHOULD NOT BE VISIBLE";
		for(int priority=0;priority<4;priority++) //we have 4 priority layers
		{
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
			if("AEIOU".Contains(bombInfo.GetSerialNumberLetters().ToString())) //check for vowels in serial number
			{
				vowel = true; //if there are, set the vowel bool
			}

			//multiple bad bones modules
			if((bbCount > 1) && !multiRuleBool) //if there's 2+ bad bones modules and this rule hasn't been completed before
			{
				for(int i=2;i<seqLength;i+=3) //find every 3rd digit
				{
					buildSeq.Insert(i,'3'); //replace with a 3
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
						buildSeq.Insert(0,'4'); //first
						buildSeq.Insert(seqLength-1,'4'); //final
						badFourRuleLog = "First/Last digit of sequence set to 4.";
						break;
					case 1:
						for(int i=1;i<seqLength;i+=2) //find every 2nd digit
						{
							if(buildSeq[i] == '\0') //check that it's not already assigned
							{
								buildSeq.Insert(i,'2'); //replace with a 2
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
							if(buildSeq[i] == '\0') //check that it's not already assigned
							{
								buildSeq.Insert(i,'4'); //replace with a 4
							}
						}
						serialRuleLog = "Every 2nd digit set to 3.";
						break;
					case 2:
						for(int i=1;i<seqLength;i+=2) //we can ignore i=2 because the only way to access this rule is for every 2nd digit to already be set to 2
						{
							if(primes.Contains(i))
							{
								if(buildSeq[i] == '\0') //check that it's not already assigned
								{
									buildSeq.Insert(i,'1'); //replace with a 1
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
									buildSeq.Insert(i,'1');
									break;
								case 1:
									buildSeq.Insert(i,'2');
									break;
								case 2:
									buildSeq.Insert(i,'3');
									break;
								case 3:
									buildSeq.Insert(i,'4');
									break;
							}
						}
						goodPlateRuleLog = "Repeating '1234' until end of sequence.";
						break;
					case 1:
						for(int i=0;i<seqLength;i+=2)
						{
							if(buildSeq[i] == '\0') //check that it's not already assigned
							{
								buildSeq.Insert(i,'3'); //replace with a 3
							}
						}
						goodPlateRuleLog = "Every odd digit set to 3.";
						break;
					case 2:
						for(int i=3;i<seqLength;i+=4) //find every 4th digit
						{
							if(buildSeq[i] == '\0') //check that it's not already assigned
							{
								buildSeq.Insert(i,'2'); //replace with a 2
							}
						}
						goodPlateRuleLog = "Every 4th digit set to 2.";
						break;
					case 3:
						for(int i=0;i<seqLength;i++) //find every remaining digit
						{
							if(buildSeq[i] == '\0') //check that it's not already assigned
							{
								buildSeq.Insert(i,(char)(goodBone+'0')); //replace with the good bone
							}
						}
						goodPlateRuleLog = String.Format("Every remaining digit set to {0}.",goodBone);
						break;
				}
				Log("Good Bone value ({0}) exceeds number of port plates ({1}). Priority: {2}. " + goodPlateRuleLog,goodBone,bombInfo.GetPortPlateCount(),priority+1);
				goodPlateRuleBool = true; //set rule as completed
			}

			//sequence contains a 2
			else if(buildSeq.Contains('2') && !containTwoRuleBool)
			{
				switch(priority)
				{
					case 0:
						for(int i=2;i<seqLength;i+=3) //find every 3rd digit
						{
							buildSeq.Insert(i,'4'); //replace with a 4
						}
						containTwoRuleLog = "Every 3rd digit set to 4.";
						break;
					case 1:
						replaceThrees = true; //replace all future 3s
						containTwoRuleLog = "Every future 3 will be set to 4.";
						break;
					case 2:
						buildSeq.Insert(seqLength-1,'1'); //replace final digit with 1
						containTwoRuleLog = "Final digit replaced with 1.";
						break;
					case 3:
						for(int i=0;i<seqLength;i++)
						{
							if(buildSeq[i] == '\0') //check that it's not already assigned
							{
								buildSeq.Insert(i,'3'); //replace with a 3
							}
						}
						containTwoRuleLog = "Every remaining digit set to 3.";
						break;
				}
				Log("Sequence contains a 2. Priority: {0}. " + containTwoRuleLog,priority+1);
				containTwoRuleBool = true; //set rule as completed
			}

			//sequence does not contain a 1
			else if(!buildSeq.Contains('1') && !notContainOneRuleBool)
			{
				switch(priority)
				{
					case 0:
						for(int i=1;i<seqLength;i+=2) //find every 2nd digit
						{
							buildSeq.Insert(i,'1'); //set to 1
						}
						notContainOneRuleLog = "Every 2nd digit set to 1.";
						break;
					case 1:
						for(int i=0;i<seqLength;i++)
						{
							if(buildSeq[i] == '1') //replace all 1s
							{
								buildSeq.Insert(i,'4'); //with 4s
							}
						}
						notContainOneRuleLog = "Every 1 replaced with 4.";
						break;
					case 2:
						for(int i=0;i<seqLength;i++)
						{
							if(buildSeq[i] == '4') //replace the first 4
							{
								buildSeq.Insert(i,'2'); //with a 2
								break; //only the first 4
							}
						}
						notContainOneRuleLog = "First 4 replaced with a 2.";
						break;
					case 3:
						for(int i=0;i<seqLength;i++) //iterate over remaining digits
						{
							if(buildSeq[i] == '\0') //check that it's not already assigned
							{
								buildSeq.Insert(i,'1'); //replace with a 1
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
							buildSeq.Insert(i,'4'); //set to 4
						}
						otherwiseLog = "Every 4th digit set to 4.";
						break;
					case 1:
						for(int i=2;i<seqLength;i+=3) //find every 3rd digit
						{
							if(buildSeq[i] == '\0') //check that it's not already assigned
							{
								buildSeq.Insert(i,'3'); //set to 3
							}
						}
						otherwiseLog = "Every 3rd digit set to 3.";
						break;
					case 2:
						for(int i=1;i<seqLength;i+=2) //find every 2nd digit
						{
							if(buildSeq[i] == '\0') //check that it's not already assigned
							{
								buildSeq.Insert(i,'2'); //set to 2
							}
						}
						otherwiseLog = "Every 2nd digit set to 2.";
						break;
					case 3:
						for(int i=0;i<seqLength;i++) //find every remaining digit
						{
							if(buildSeq[i] == '\0') //check that it's not already assigned
							{
								buildSeq.Insert(i,'1'); //set to 1
							}
						}
						otherwiseLog = "Every remaining digit set to 1.";
						break;
				}
				Log("No other rules apply. Priority: {0}. " + otherwiseLog,priority+1);
			}

			//log on every iteration
			Log("Current sequence: {0}",String.Concat(buildSeq));
		}
		
		//replacements
		Log("Replacing all digits matching 'Replace future values of X with Y' rules:");
		for(int i=0;i<seqLength;i++)
		{
			if(replaceTwos) //if we're replacing twos
			{
				if(buildSeq[i] == '2')
				{
					buildSeq.Insert(i,'3');
					Log("Replaced digit {0} with 3",i+1);
				}
			}
			if(replaceThrees) //if we're replacing threes
			{
				if(buildSeq[i] == '3') //yes, this can happen straight after a 2 is replaced with a 3 - 3 -> 4 takes priority
				{
					buildSeq.Insert(i,'4');
					Log("Replaced digit {0} with 4",i+1);
				}
			}
			Log("Current sequence: {0}",String.Concat(buildSeq));
		}

		Log("Replacing the Bad Bone values with the Good Bone value:");
		for(int i=0;i<seqLength;i++)
		{
			if(buildSeq[i] == badBone) //if value is the bad bone
			{
				buildSeq.Insert(i,(char)(goodBone+'0')); //replace with the good bone
				Log("Replaced digit {0} with {1}",i+1,goodBone);
			}
		}

		return String.Concat(buildSeq);
	}

	private void PlayLow()
	{
		//
	}

	private void PlayMiddle()
	{
		//
	}

	private void PlayHigh()
	{
		//
	}

	private void PlayFinal()
	{
		//
	}

	// Update is called once per frame
	void Update () {
		if(!_skullHeld)
		{
			transform.Rotate(Vector3.up * speed * Time.deltaTime);
		}
	}
}
