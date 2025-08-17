using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;
using Events;
//using Math = ExMath;

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

	void Awake () { //Avoid doing calculations in here regarding edgework. Just use this for setting up buttons for simplicity.
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
		if(isWakingUp || isAutosolving){
			Needy.HandlePass();
			return;
		}
		CheckLightsCoroutine = StartCoroutine(CheckLights());
	}

	void OnTimerExpired(){
		if(isAutosolving) return;
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
			timerem += (isLightsOn ? -2f/60f : 1f/150f);
			timerem = Mathf.Clamp(timerem, 0.01f, 30.5f);
			Needy.SetNeedyTimeRemaining(timerem);

			//battery stuff
			UpdateBattery(6 - (int)((timerem+5f)/6));

			yield return null;
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
	private readonly string TwitchHelpMessage = @"Use !{0} on/off to toggle lights. (UNSUPPORTED)";
#pragma warning restore 414

	IEnumerator ProcessTwitchCommand (string Command) {
		yield return null;
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
		//bodge
		OnTimerExpired();
		OnNeedyDeactivation();
		Needy.HandlePass();
	}
}
