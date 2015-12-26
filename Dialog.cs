using UnityEngine;
using System.Collections;
using System.Xml;
using System.Collections.Generic;


public class Dialog : MonoBehaviour {

	// key = actor's name, value = actor's dialogue set
	private Dictionary<string, ActorDialogue> dialogue = new Dictionary<string, ActorDialogue>();

	private int id;
	private int actorCount = 0;
	private int setCount = 0;
	private int lineCount = 0;
	private int dialogueCount = 0;
	
	void Start () {

		parseDialogueFromXML ();

		/*print ("Actor count: " + actorCount);
		print ("Set count: " + setCount);
		print ("Line count: " + lineCount);
		print ("Dialogue count: " + dialogueCount);*/

	}

	void Update () {
	}
	public Dialog returnInstance() {
		return this;
	}
	// Dialogue is pulled from this method by providing actors name and set/line id
	public string getActorLineById(string actor, int setId, int lineId) {

		ActorDialogue actorDialogue;
		if (dialogue.TryGetValue (actor, out actorDialogue)) {
			DialogueLine dialogueLine = actorDialogue.getDialogueLineById (setId, lineId);
			if (dialogueLine != null)
				return dialogueLine.getText();
			else
				return "FAIL";
		} else
			return "FAIL, NO VALUE";
	}

	public int getSetById(string actor, int setId, int lineId) {
		
		ActorDialogue actorDialogue;
		if (dialogue.TryGetValue (actor, out actorDialogue)) {
			DialogueLine dialogueLine = actorDialogue.getDialogueLineById (setId, lineId);
			if (dialogueLine != null)
				return dialogueLine.getSetId();
			else
				return 0;
		} else
			return 0;
	}

	public int getBranchById(string actor, int setId, int lineId) {
		
		ActorDialogue actorDialogue;
		if (dialogue.TryGetValue (actor, out actorDialogue)) {
			DialogueLine dialogueLine = actorDialogue.getDialogueLineById (setId, lineId);
			if (dialogueLine != null)

				return dialogueLine.getBranchSetId();
			else
				return 0;
		} else
			return 0;
	}
	public void test() {
		print ("Test");
	}
	public void parseDialogueFromXML() {

		/* Each line in set are linked sequentically, and may be condiationally linked
		 * to any line in any set within actors context. Thus we need to double check linking
		 * on lines already parsed and lines not yet parsed to link to both directions */

		bool firstLine = true; // ensures that set starting line is not linked to last line of other set
		ArrayList allLines = new ArrayList(); // needed for backward linking
		ArrayList conditionalUnlinkedLines = new ArrayList (); // needed for forward linking

		XmlDocument xmlDoc = new XmlDocument ();
		xmlDoc.Load ("dialogue.xml"); // Peliprojekti/dialogue.xml

		XmlNodeList actors = xmlDoc.GetElementsByTagName("actor");
		foreach (XmlNode actor in actors)
		{
			actorCount++;
			dialogueCount++;

			string actorName = actor.Attributes["name"].Value;
			ActorDialogue actorDialogue = new ActorDialogue(actorName);

			XmlNodeList dialogueSetNodes = actor.ChildNodes;
			foreach (XmlNode dialogueSetNode in dialogueSetNodes) // Actor's all sets
			{
				setCount++;

				firstLine = true; // new set started, thus first processed line is marked a first line, set to false in the end

				int setId = int.Parse (dialogueSetNode.Attributes["setId"].Value);
				DialogueSet dialogueSet = new DialogueSet(setId);

				XmlNodeList dialogueLineNodes = dialogueSetNode.ChildNodes;
				DialogueLine previousLine = null; // for sequential linking
				foreach (XmlNode dialogueLineNode in dialogueLineNodes) // Set's all lines
				{
					lineCount++;

					int lineId = int.Parse (dialogueLineNode.Attributes["id"].Value);
					string text = dialogueLineNode.InnerText;

					DialogueLine dialogueLine = new DialogueLine(text, lineId);
					dialogueLine.setSetId(dialogueSet.getId()); // line must know it's set for backward linking
					allLines.Add(dialogueLine);

					// Linking sequently lines
					if (previousLine != null && !firstLine) // Don't link to previous set
						previousLine.setNextLine(dialogueLine);

					// look for branching token
					if (dialogueLineNode.Attributes["branchInfo"] != null) {

						dialogueLine.setChoice(true);

						// parse branch info
						string branchInfo = dialogueLineNode.Attributes["branchInfo"].Value;
						char[] delimiter = { '/' };
						string[] values = branchInfo.Split (delimiter);
						int branchSetId = int.Parse(values[0]);
						int branchLineId = int.Parse(values[1]);

						if (branchSetId > dialogueSet.getId() || branchLineId > dialogueLine.getId()) {
							// Prepare for forward linking
							dialogueLine.setBranchLinkingInfo(branchInfo);
							conditionalUnlinkedLines.Add(dialogueLine);
						}

						else {
							// Perform backward linking
							foreach (DialogueLine oldLine in allLines) {
								if (oldLine.getSetId() == branchSetId && dialogueLine.getId() == branchLineId) {
									dialogueLine.setAltLine(oldLine);
									break;
								}
							}
						}

					}

				    // Try to forward link pending lines on this line

					DialogueLine toRemove = null; // Remove linked conditionalLine from pending list AFTER iteration to avoid problems
					foreach (DialogueLine conditionalLine in conditionalUnlinkedLines) {

						int branchLineId = conditionalLine.getBranchLineId();
						int branchSetId = conditionalLine.getBranchSetId();
							
						if (branchLineId == dialogueLine.getId()
							&& branchSetId == dialogueSet.getId()) {

							conditionalLine.setAltLine(dialogueLine);
							toRemove = conditionalLine;
						}
					}

					// toRemove was linked, remove from forward linking list
					if (toRemove != null)
						conditionalUnlinkedLines.Remove(toRemove); // Iteration done, safe to remove

					/// ---------------------------------------------------------------

					previousLine = dialogueLine;
					dialogueSet.addDialogueLine(dialogueLine);
					firstLine = false; // new lines may be coming in same set, they are not first line
					//print ("LINE ADDED");
				}

				actorDialogue.addDialogueSet(dialogueSet);
				//print ("SET ADDED");
			}

			//print ("DIALOGUE TO BE ADDED");
			dialogue.Add(actorDialogue.getActorName(), actorDialogue);
			//print ("DIALOGUE ADDED");
		}
	}

	public class ActorDialogue {
		
		private string actorName;
		private Dictionary<int, DialogueSet> dialogueTree = new Dictionary<int, DialogueSet> (); // key=setId, value=dialogueSet
		
		public ActorDialogue(string actorName) {
			this.actorName = actorName;
		}
		
		public string getActorName() {
			return actorName;
		}
		
		public void addDialogueSet(DialogueSet dialogueSet) {
			dialogueTree.Add (dialogueSet.getId(), dialogueSet);
		}
		
		public DialogueSet getDialogueSetById(int id) {
			
			DialogueSet dialogueSet;
			
			if (dialogueTree.TryGetValue (id, out dialogueSet))
				return dialogueSet;
			
			else {
				//print ("Invalid DialogueSet ID");
				return null;
			}
		}
		
		public DialogueLine getDialogueLineById(int setId, int lineId) {
			
			DialogueSet dialogueSet;
			if (dialogueTree.TryGetValue (setId, out dialogueSet)) {
				return dialogueSet.getLineById(lineId);
			}
			
			else {
				//print ("Invalid DialogueSet ID");
				return null;
			}
		}
		
		public int getDialogueSetCount() {
			return dialogueTree.Count;
		}
		
	}

	public class DialogueSet {
		
		private int id;
		private Dictionary<int, DialogueLine> lines = new Dictionary<int, DialogueLine> (); // Lines forms linked list
		
		public DialogueSet(int id) {
			this.id = id;
		}
		
		public void addDialogueLine(DialogueLine line) {
			lines.Add (line.getId(), line);
		}
		
		public DialogueLine getLineById(int id) {
			
			DialogueLine line;
			
			if (lines.TryGetValue (id, out line))
				return line;
			
			else {
				//print ("Invalid DialogueLine ID");
				return null;
			}
		}
		
		public Dictionary<int, DialogueLine> getDialogueLines() {
			return lines;
		}
		
		public int getDialogueLinesCount() {
			return lines.Count;
		}
		
		public int getId() {
			return id;
		}
	}

	// Linked list of dialogue lines
	public class DialogueLine {

		// Always initialized
		private string text;
		private int id;
		private int setId; // for linking
		private DialogueLine nextLine;

		// Optional
		private bool ischoice = false;
		private bool branch = false; // Switch to choose correct dialogue path
		private DialogueLine altLine = null;

		// Helper for linking process
		private string branchLinkingInfo = null;


		public DialogueLine(string text, int id) {
			this.text = text;
			this.id = id;
		}

		public void setNextLine(DialogueLine nextLine) {
			this.nextLine = nextLine;
		}

		public void setAltLine(DialogueLine altLine) {
			this.altLine = altLine;
			setChoice (true);
		}

		public string getText() {
			return text;
		}

		public int getId() {
			return id;
		}

		public void setChoice(bool ischoice) {
			this.ischoice = ischoice;
		}

		public bool isChoice() {
			return ischoice;
		}

		public void setSetId(int setId) {
			this.setId = setId;
		}

		public int getSetId() {
			return setId;
		}

		// Switch to choose correct dialogue path
		public void makeBranch(bool branch) {
			if (ischoice)
				this.branch = branch;
			else {
				//print ("Couldn't branch non-conditional line");
			}
		}

		public bool doesBranch() {
			return branch;
		}

		public DialogueLine getNextLine() {
			return branch ? altLine : nextLine;
		}

		// Helper methods for linking
		public void setBranchLinkingInfo(string info) {
			branchLinkingInfo = info;
		}

		public int getBranchSetId() {

			if (branchLinkingInfo != null) {
				char[] delimiter = { '/' };
				string[] values = branchLinkingInfo.Split (delimiter);
				return int.Parse(values[0]);
			}
			else {
				return -1;
			}
		}

		public int getBranchLineId() {
			
			if (branchLinkingInfo != null) {
				char[] delimiter = { '/' };
				string[] values = branchLinkingInfo.Split (delimiter);
				return int.Parse(values[1]);
			}
			else {
				return -1;
			}
		}
	}

}