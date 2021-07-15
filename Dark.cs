using MelonLoader;
using ModThatIsNotMod;
using StressLevelZero.Data;
using System.Collections.Generic;
using UnityEngine;
using VLB;

namespace Camo {
	public class Dark : MelonMod {
		private bool modToggle;
		private bool wEmissives;
		private bool extraDarkScene;
		private bool extraDarkLight;

		private Shader standard;
		private Shader holowall;
		private Shader holowallC;
		private Shader holoP;
		private Shader hologram;
		private Shader scanline;
		private Shader flowmap;
		private Shader gibskinE;

		//Material whitelist (I know, it's jank)
		private readonly List<string> matWhitelist = new List<string>() { "light", "mat_lamp", "mat_mono", "mat_grid", "mat_enemy", "charging", "lit", "neon", "albedo2", "metal1", "monitor", "lambert", "lamp", "vending", "billboard", "mat_district" };

		public override void OnApplicationStart() {
			InitialRegisters();
		}

		public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
			if(modToggle && (!MelonPreferences.GetEntryValue<bool>("DarkScenes", "ZWOnly") || (MelonPreferences.GetEntryValue<bool>("DarkScenes", "ZWOnly") && sceneName == "zombie_warehouse")) && !MelonPreferences.GetEntryValue<bool>("DarkScenes", "ExtraBrightScene")) { //Main start check
				FindShaders();

				//Run all main methods to make scene dark
				SetMaterials();
				SetRenderers();
				RemoveVolumetrics();
				SpawnFlashlight();

			} else if(modToggle && MelonPreferences.GetEntryValue<bool>("DarkScenes", "ExtraBrightScene")) {
				//Set extra bright settings :)
				GameObject lightGO = new GameObject();
				lightGO.transform.position = new Vector3(0, 10, 0);
				Light light = lightGO.AddComponent<Light>();
				light.intensity = 1000;
				light.range = 1000;
			}
		}

		public override void OnUpdate() {
			//Force reload
			if(Input.GetKeyDown(KeyCode.P)) {
				FindShaders();
				SetMaterials();
				SetRenderers();
				RemoveVolumetrics();
				SpawnFlashlight();
			}
		}

		#region Dark Scene Setup
		void SetMaterials() {
			//Make all materials non-emissive / non-glossy
			Material[] allMats = Resources.FindObjectsOfTypeAll<Material>();
			foreach(Material mat in allMats) {
				//Set shader specific values (Pretty ugly, I know, switch statements don't work here sadly...)
				if(mat.shader == holowall || mat.shader == holowallC) {
					mat.SetColor("_ScreenColor", Color.black);
				} else if(mat.shader == hologram || mat.shader == flowmap) {
					mat.SetColor("_Color", new Color(0.1f, 0.1f, 0.1f, 1));
				} else if(mat.shader == scanline || mat.shader == holoP) {
					mat.SetColor("_EmissionColor", Color.black);
					mat.SetColor("_Fluorescence", Color.black);
					mat.SetFloat("_Power", 0f);
				} else if(mat.shader == gibskinE) {
					mat.SetColor("_EmissionColor", Color.black);
					mat.SetInt("_FLUORESCENCEMAP", 0);
				} else if(mat.shader == standard) {
					if(wEmissives) { //Make sure standard mats are in whitelist
						foreach(string matName in matWhitelist) {
							if(mat.name.ToLower().Contains(matName)) {
								mat.DisableKeyword("_EMISSION");
								break;
							}
						}
					} else { //Remove all emissives if not targeting whitelist
						mat.DisableKeyword("_EMISSION");
					}
				}

				if(extraDarkScene) {
					mat.SetFloat("g_flCubeMapScalar", 0f);
				} else {
					mat.SetFloat("g_flCubeMapScalar", 0.3f);
				}
			}
		}

		void SetRenderers() {
			//Set all renderer lightmaps to off
			Renderer[] allRs = Resources.FindObjectsOfTypeAll<Renderer>();
			foreach(Renderer rend in allRs) {
				rend.lightmapIndex = -1;
				if(extraDarkLight)
					rend.realtimeLightmapIndex = -1;
				rend.useLightProbes = false;
				rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
				rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
			}
		}

		void RemoveVolumetrics() {
			//Disable all volumetrics
			VolumetricLightBeam[] allVs = Resources.FindObjectsOfTypeAll<VolumetricLightBeam>();
			foreach(VolumetricLightBeam vol in allVs)
				vol.enabled = false;

			BeamGeometry[] allVBs = Resources.FindObjectsOfTypeAll<BeamGeometry>();
			foreach(BeamGeometry bg in allVBs)
				bg.enabled = false;
		}

		void SpawnFlashlight() {
			//Spawn flashlight
			Transform head = Player.GetPlayerHead().transform;
			GameObject flashlight = null;
			SpawnableObject[] spawnables = Resources.FindObjectsOfTypeAll<SpawnableObject>();
			for(int i = 0; i < spawnables.Length; i++) {
				if(spawnables[i].title == "Flashlight") {
					flashlight = GameObject.Instantiate(spawnables[i].prefab, head.position + Vector3.forward, Quaternion.Euler(new Vector3(0, 90, 0)));
					break;
				}
			}

			//Add dim light to flashlight if not ExtraDarkFL
			if(!extraDarkLight) {
				GameObject extraLightGO = new GameObject();
				extraLightGO.transform.parent = flashlight.transform;
				extraLightGO.transform.localPosition = Vector3.zero;
				Light eL = extraLightGO.AddComponent<Light>();
				eL.type = LightType.Point;
				eL.range = 1f;
				eL.intensity = 1f;
			}

			//Undo dark changes on flashlight
			Material[] flMats = flashlight.GetComponentsInChildren<Material>();
			foreach(Material mat in flMats)
				mat.EnableKeyword("_EMISSION");

			VolumetricLightBeam[] flV = flashlight.GetComponentsInChildren<VolumetricLightBeam>();
			foreach(VolumetricLightBeam vol in flV)
				vol.enabled = true;

			BeamGeometry[] flBG = flashlight.GetComponentsInChildren<BeamGeometry>();
			foreach(BeamGeometry bg in flBG)
				bg.enabled = true;
		}
		#endregion


		void InitialRegisters() {
			//ModPrefs, Initial Vars, MTINM Menu Items

			MelonPreferences.CreateCategory("DarkScenes");
			MelonPreferences.CreateEntry("DarkScenes", "ModToggle", true);
			MelonPreferences.CreateEntry("DarkScenes", "WhitelistEmissives", true);
			MelonPreferences.CreateEntry("DarkScenes", "ExtraDarkScene", true);
			MelonPreferences.CreateEntry("DarkScenes", "ExtraDarkFlashlight", false);
			MelonPreferences.CreateEntry("DarkScenes", "ZWOnly", false);
			MelonPreferences.CreateEntry("DarkScenes", "ExtraBrightScene", false);

			modToggle = MelonPreferences.GetEntryValue<bool>("DarkScenes", "ModToggle");
			wEmissives = MelonPreferences.GetEntryValue<bool>("DarkScenes", "WhitelistEmissives");
			extraDarkScene = MelonPreferences.GetEntryValue<bool>("DarkScenes", "ExtraDarkScene");
			extraDarkLight = MelonPreferences.GetEntryValue<bool>("DarkScenes", "ExtraDarkFlashlight");

			ModThatIsNotMod.BoneMenu.MenuCategory category = ModThatIsNotMod.BoneMenu.MenuManager.CreateCategory("Dark Scenes", Color.blue);
			category.CreateFunctionElement("CHANGES REQUIRE SCENE RELOAD", Color.red, null);
			category.CreateBoolElement("Mod Toggle", Color.white, modToggle, ToggleMod);
			category.CreateBoolElement("Extra Dark Scene", Color.white, extraDarkScene, ToggleDarkScene);
			category.CreateBoolElement("Extra Dark Flashlight", Color.white, extraDarkLight, ToggleDarkLight);
		}

		void FindShaders() {
			standard = Shader.Find("Valve/vr_standard");
			holowall = Shader.Find("SLZ/Holographic Wall");
			holowallC = Shader.Find("SLZ/Holographic Wall - Clock");
			holoP = Shader.Find("SLZ/Holographic Projection");
			hologram = Shader.Find("SLZ/Additive Hologram with Depth");
			scanline = Shader.Find("SLZ/Scanline");
			flowmap = Shader.Find("SLZ/Flowmap Additive");
			gibskinE = Shader.Find("SLZ/GibSkinMAS_Emissive");
		}

		//MTINM Menu Bool Flips

		void ToggleMod(bool toggle) {
			modToggle = !modToggle;
			MelonPreferences.SetEntryValue<bool>("DarkScenes", "ModToggle", modToggle);
		}
		void ToggleDarkScene(bool toggle) {
			extraDarkScene = !extraDarkScene;
			MelonPreferences.SetEntryValue<bool>("DarkScenes", "ExtraDarkScene", extraDarkScene);
		}
		void ToggleDarkLight(bool toggle) {
			extraDarkLight = !extraDarkLight;
			MelonPreferences.SetEntryValue<bool>("DarkScenes", "ExtraDarkFlashlight", extraDarkLight);
		}
	}
}