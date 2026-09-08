using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;
using Events;

/*
	To use mission description based settings, format the description as follows:
	[Ethan's Dream] <Recharge Rate> <Dissipation Rate>
	Decimal and negative values are supported. Test with the regex below to be sure.
	Recharge means when lights are on, Dissipation means when lights are off. The default values are 2.0 and 0.5 respectively.
	\^[Ethan's Dream\] (-?\d+\.?\d*) (-?\d+\.?\d*)$
 */

public class EthansDream : MonoBehaviour {

	public KMBombInfo Bomb;
	public KMAudio Audio;
	public KMNeedyModule Needy;
	public KMSelectable NeedySelctable;

	static int ModuleIdCounter = 1;
	int ModuleId;
	private bool ModuleSolved;

	public KMSelectable LightSwitch;
	public TextMesh Display;
	public GameObject[] BatBars;
	public Light[] BatLights;
	public Light SwitchLight;
	public Material[] BatMats;

	private Coroutine CheckLightsCoroutine = null;

	private bool isActive = false;
	private bool isLightsOn = false;
	private bool isWakingUp = false;
	private bool isBombDead = false;
	private bool isAutosolving = false;

	private EthansDreamSettings Settings = new EthansDreamSettings();
	private float RechargeRate = 2f; //ie towards death, at the rate of 2.0 units per "sec" out of 60
	private float DissipationRate = 0.5f; //ie to life , at the rate of 0.5 units per "sec" out of 60
	//units are as per "timer"

	bool TwitchPlaysActive;

	void Awake () { //Avoid doing calculations in here regarding edgework. Just use this for setting up buttons for simplicity.
		//Setup settings
		ModConfig<EthansDreamSettings> modConfig = new ModConfig<EthansDreamSettings>("EthansDreamSettings");
		Settings = modConfig.Settings;
		modConfig.Settings = Settings;
		RechargeRate = Settings.RechargeRate;
		DissipationRate = Settings.DissipationRate;

		//Load data from mission
		string missionDescription = (string)wawa.DDL.Missions.Description;

        Regex regex = new Regex(@"\[Ethan's Dream\] (-?\d+\.?\d*) (-?\d+\.?\d*)");
		if (missionDescription != null && regex.IsMatch(missionDescription)) {
            Match match = regex.Match(missionDescription);
 
            RechargeRate = float.Parse(match.Groups[1].Value);
            DissipationRate = float.Parse(match.Groups[2].Value);
            Debug.LogFormat("[Ethan's Dream #{0}] Settings have been overwritten by mission description.", ModuleId);
        }

        Debug.LogFormat("[Ethan's Dream #{0}] Twitch plays active: {1}.", ModuleId, TwitchPlaysActive);
        Debug.LogFormat("[Ethan's Dream #{0}] Recharge Rate: {1}, Dissipation Rate: {2}", ModuleId, RechargeRate, DissipationRate);


        ModuleId = ModuleIdCounter++;
		LightSwitch.OnInteract += delegate () { ButtonPress(); return false; };

		Needy.OnNeedyActivation += OnNeedyActivation;
		Needy.OnNeedyDeactivation += OnNeedyDeactivation;
		Needy.OnTimerExpired += OnTimerExpired;
		Bomb.OnBombExploded += delegate() { isBombDead = true; };
	}

	void ButtonPress (){
		LightSwitch.AddInteractionPunch();
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, LightSwitch.transform);

		TurnLights(!isLightsOn);
	}

	void OnNeedyActivation(){
		if(isWakingUp){
			Needy.HandlePass();
			return;
		}

		CheckLightsCoroutine = StartCoroutine(CheckLights());
		if(isLightsOn) ButtonPress();
	}

	void OnTimerExpired(){
		StartCoroutine(WakeUp());
	}

	void OnNeedyDeactivation(){
		TurnLights(true);
		UpdateBattery(0);
		StopCoroutine(CheckLightsCoroutine);
	}

	IEnumerator CheckLights(){
		float timerem = Needy.GetNeedyTimeRemaining();
		while(true){
			//time remaining
			if(isAutosolving){
				timerem = 60;
			} else {
				timerem += (isLightsOn ? -RechargeRate/15f : DissipationRate/15f);
				timerem = Mathf.Clamp(timerem, 0.01f, 60.5f);
			}

			Needy.SetNeedyTimeRemaining(timerem);
			UpdateBattery(6 - (int)((timerem+10f)/12));
			yield return new WaitForSeconds(1/15f);
		}
	}

	void TurnLights (bool on){
		if (!Application.isEditor){
			if(on && !isLightsOn) SceneManager.Instance.GameplayState.Room.CeilingLight.TurnOn(true);
			else if(!on && isLightsOn) SceneManager.Instance.GameplayState.Room.CeilingLight.TurnOff(false); 
		}
		isLightsOn = on;
		UpdateSwitch();
	}

	void UpdateSwitch(){
		Display.text = isLightsOn ? "I" : "O"; //debug
		LightSwitch.transform.localEulerAngles = new Vector3(0f, isLightsOn ? -90f : 90f, 0f);
		SwitchLight.intensity = isLightsOn ? 0f: 10f;
	}

	void UpdateBattery(int count){
		for(int i = 0; i < count; i++){ //on
			BatBars[i].GetComponent<Renderer>().material = BatMats[(count-1)/2+1];
			BatLights[i].intensity = Mathf.PingPong(Bomb.GetTime()*3f, 7f) + 1.5f;
			BatLights[i].color = BatMats[(count-1)/2+1].color;
		}

		for(int i = count; i < 6; i++){ //off
			BatBars[i].GetComponent<Renderer>().material = BatMats[0];
			BatLights[i].intensity = 0f;
		}
	}

	IEnumerator WakeUp(){
		StopCoroutine(CheckLightsCoroutine);
		UpdateBattery(6);
		isWakingUp = true;
		while(!isBombDead){
			Needy.HandleStrike();
			LightSwitch.AddInteractionPunch(4f);
			if(TwitchPlaysActive){
				isWakingUp = false;
				yield break;
			}
			yield return new WaitForSeconds(0.5f);
		}
	}

	private void OnEnable(){
		EnvironmentEvents.OnLightsOn += OnLightsOn;
		EnvironmentEvents.OnLightsOff += OnLightsOff;
	}

	private void OnDisable(){
		EnvironmentEvents.OnLightsOn -= OnLightsOn;
		EnvironmentEvents.OnLightsOff -= OnLightsOff;
	}

	private void OnLightsOn(bool _){
		isLightsOn = true;
		UpdateSwitch();
	}

	private void OnLightsOff(bool _){
		isLightsOn = false;
		UpdateSwitch();
	}

#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"Use !{0} on/off to toggle lights.";
#pragma warning restore 414

	IEnumerator ProcessTwitchCommand (string Command) {
		yield return null;

		if(isAutosolving){
			yield return "sendtochaterror Needy is autosolving.";
			yield break;
		}

		Command = Command.ToUpper();
		if(Command == "ON"){
			if(!isLightsOn) LightSwitch.OnInteract();
			else yield return "sendtochaterror Lights are already on.";
		} else if(Command == "OFF"){
			if(isLightsOn) LightSwitch.OnInteract();
			else yield return "sendtochaterror Lights are already off.";
		} else {
			yield return "sendtochaterror Invalid command: " + Command;
		}
	}

	void TwitchHandleForcedSolve () { //Void so that autosolvers go to it first instead of potentially striking due to running out of time.
		StartCoroutine(HandleAutosolver());
	}

	IEnumerator HandleAutosolver () {
		yield return null;
		isAutosolving = true;
		TurnLights(true);
	}

	//mod settings
	class EthansDreamSettings {
		public float RechargeRate = 2f;
		public float DissipationRate = 0.5f;
	}

	static Dictionary<string, object>[] TweaksEditorSettings = new Dictionary<string, object>[] {
		new Dictionary<string, object> {
			 { "Filename", "EthansDream.json" },
			 { "Name", "EthansDream Settings" },
			 { "Listing", new List<Dictionary<string, object>>{
					new Dictionary<string, object> {
						 { "Key", "Recharge Rate" },
						 { "Text", "The rate at which the battery recharges in units per \"second\". Default is 2.0." }
					},
					new Dictionary<string, object> {
						 { "Key", "Dissipation Rate" },
						 { "Text", "The rate at which the battery dissipates in units per \"second\". Default is 0.5." }
					}
			 } }
		}
	};
}
