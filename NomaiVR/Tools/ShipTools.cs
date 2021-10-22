﻿using NomaiVR.Hands;
using NomaiVR.Helpers;
using NomaiVR.ReusableBehaviours;
using UnityEngine;
using UnityEngine.UI;
using static NomaiVR.Tools.AutopilotButtonPatch;

namespace NomaiVR.Tools
{
    internal class ShipTools : NomaiVRModule<ShipTools.Behaviour, ShipTools.Behaviour.Patch>
    {
        protected override bool IsPersistent => false;
        protected override OWScene[] Scenes => SolarSystemScene;

        public class Behaviour : MonoBehaviour
        {
            private ReferenceFrameTracker referenceFrameTracker;
            private static Transform mapGridRenderer;
            private static ShipMonitorInteraction probe;
            private static ShipMonitorInteraction signalscope;
            private static ShipMonitorInteraction landingCam;
            private static ShipMonitorInteraction autoPilot;
            private static ShipCockpitController cockpitController;
            private static bool isLandingCamEnabled;

            internal void Awake()
            {
                referenceFrameTracker = FindObjectOfType<ReferenceFrameTracker>();
                cockpitController = FindObjectOfType<ShipCockpitController>();
                mapGridRenderer = FindObjectOfType<MapController>()._gridRenderer.transform;
            }

            internal void Update()
            {
                if (referenceFrameTracker.isActiveAndEnabled && ToolHelper.IsUsingAnyTool())
                {
                    referenceFrameTracker.enabled = false;
                }
                else if (!referenceFrameTracker.isActiveAndEnabled && !ToolHelper.IsUsingAnyTool())
                {
                    referenceFrameTracker.enabled = true;
                }
            }

            public class Patch : NomaiVRPatch
            {
                public override void ApplyPatches()
                {
                    Postfix<ShipBody>("Start", nameof(ShipStart));
                    Prefix<ReferenceFrameTracker>(nameof(ReferenceFrameTracker.FindReferenceFrameInLineOfSight), nameof(PreFindFrame));
                    Postfix<ReferenceFrameTracker>(nameof(ReferenceFrameTracker.FindReferenceFrameInLineOfSight), nameof(PostFindFrame));
                    Prefix<ReferenceFrameTracker>(nameof(ReferenceFrameTracker.FindReferenceFrameInMapView), nameof(PreFindFrame));
                    Postfix<ReferenceFrameTracker>(nameof(ReferenceFrameTracker.FindReferenceFrameInMapView), nameof(PostFindFrame));
                    Empty<PlayerCameraController>("OnEnterLandingView");
                    Empty<PlayerCameraController>("OnExitLandingView");
                    Empty<PlayerCameraController>("OnEnterShipComputer");
                    Empty<PlayerCameraController>("OnExitShipComputer");
                    Prefix<ShipCockpitController>("EnterLandingView", nameof(PreEnterLandingView));
                    Prefix<ShipCockpitController>("ExitLandingView", nameof(PreExitLandingView));
                    Postfix<ShipCockpitController>("ExitFlightConsole", nameof(PostExitFlightConsole));
                    Prefix<ShipCockpitUI>("Update", nameof(PreCockpitUIUpdate));
                    Postfix<ShipCockpitUI>("Update", nameof(PostCockpitUIUpdate));
                    Prefix(typeof(ReferenceFrameTracker).GetMethod("UntargetReferenceFrame", new[] { typeof(bool) }), nameof(PreUntargetFrame));
                }

                private static void PreCockpitUIUpdate(ShipCockpitController ____shipSystemsCtrlr)
                {
                    ____shipSystemsCtrlr._usingLandingCam = isLandingCamEnabled;
                }

                private static void PostCockpitUIUpdate(ShipCockpitController ____shipSystemsCtrlr)
                {
                    ____shipSystemsCtrlr._usingLandingCam = false;
                }

                private static bool PreEnterLandingView(
                    LandingCamera ____landingCam,
                    ShipLight ____landingLight,
                    ShipCameraComponent ____landingCamComponent,
                    ShipAudioController ____shipAudioController
                )
                {
                    isLandingCamEnabled = true;
                    ____landingCam.enabled = true;
                    ____landingLight.SetOn(true);

                    if (____landingCamComponent.isDamaged)
                    {
                        ____shipAudioController.PlayLandingCamOn(AudioType.ShipCockpitLandingCamStatic_LP);
                    }
                    else
                    {
                        ____shipAudioController.PlayLandingCamOn(AudioType.ShipCockpitLandingCamAmbient_LP);
                    }

                    return false;
                }

                private static bool PreExitLandingView(
                    LandingCamera ____landingCam,
                    ShipLight ____landingLight,
                    ShipAudioController ____shipAudioController
                )
                {
                    isLandingCamEnabled = false;
                    ____landingCam.enabled = false;
                    ____landingLight.SetOn(false);
                    ____shipAudioController.PlayLandingCamOff();

                    return false;
                }

                private static void PostExitFlightConsole(ShipCockpitController __instance)
                {
                    __instance.ExitLandingView();
                }

                private static bool ShouldRenderScreenText()
                {
                    return Locator.GetToolModeSwapper().IsInToolMode(ToolMode.None);
                }

                private static void ShipStart(ShipBody __instance)
                {
                    var cockpitUI = __instance.transform.Find("Module_Cockpit/Systems_Cockpit/ShipCockpitUI");

                    var probeScreenPivot = cockpitUI.Find("ProbeScreen/ProbeScreenPivot");
                    probe = probeScreenPivot.Find("ProbeScreen").gameObject.AddComponent<ShipMonitorInteraction>();
                    probe.mode = ToolMode.Probe;
                    probe.text = UITextType.ScoutModePrompt;

                    var font = Resources.Load<Font>(@"fonts/english - latin/SpaceMono-Regular");

                    var probeCamDisplay = probeScreenPivot.Find("ProbeCamDisplay");
                    var probeScreenText = new GameObject().AddComponent<Text>();
                    probeScreenText.gameObject.AddComponent<ConditionalRenderer>().GETShouldRender = ShouldRenderScreenText;
                    probeScreenText.transform.SetParent(probeCamDisplay.transform, false);
                    probeScreenText.transform.localScale = Vector3.one * 0.0035f;
                    probeScreenText.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    probeScreenText.text = "<color=grey>PROBE LAUNCHER</color>\n\ninteract with screen\nto activate";
                    probeScreenText.color = new Color(1, 1, 1, 0.1f);
                    probeScreenText.alignment = TextAnchor.MiddleCenter;
                    probeScreenText.fontSize = 8;
                    probeScreenText.font = font;

                    var signalScreenPivot = cockpitUI.Find("SignalScreen/SignalScreenPivot");
                    signalscope = signalScreenPivot.Find("SignalScopeScreenFrame_geo").gameObject.AddComponent<ShipMonitorInteraction>();
                    signalscope.mode = ToolMode.SignalScope;
                    signalscope.text = UITextType.UISignalscope;

                    var sigScopeDisplay = signalScreenPivot.Find("SigScopeDisplay");
                    var scopeTextCanvas = new GameObject().AddComponent<Canvas>();
                    scopeTextCanvas.gameObject.AddComponent<ConditionalRenderer>().GETShouldRender = ShouldRenderScreenText;
                    scopeTextCanvas.transform.SetParent(sigScopeDisplay.transform.parent, false);
                    scopeTextCanvas.transform.localPosition = sigScopeDisplay.transform.localPosition;
                    scopeTextCanvas.transform.localRotation = sigScopeDisplay.transform.localRotation;
                    scopeTextCanvas.transform.localScale = sigScopeDisplay.transform.localScale;
                    var scopeScreenText = new GameObject().AddComponent<Text>();
                    scopeScreenText.transform.SetParent(scopeTextCanvas.transform, false);
                    scopeScreenText.transform.localScale = Vector3.one * 0.5f;
                    scopeScreenText.text = "<color=grey>SIGNALSCOPE</color>\n\ninteract with screen to activate";
                    scopeScreenText.color = new Color(1, 1, 1, 0.1f);
                    scopeScreenText.alignment = TextAnchor.MiddleCenter;
                    scopeScreenText.fontSize = 8;
                    scopeScreenText.font = font;

                    var cockpitTech = __instance.transform.Find("Module_Cockpit/Geo_Cockpit/Cockpit_Tech/Cockpit_Tech_Interior");

                    landingCam = cockpitTech.Find("LandingCamScreen").gameObject.AddComponent<ShipMonitorInteraction>();
                    landingCam.button = InputConsts.InputCommandType.LANDING_CAMERA;
                    landingCam.SkipPressCallback = () =>
                    {
                        if (isLandingCamEnabled)
                        {
                            cockpitController.ExitLandingView();
                            return true;
                        }
                        return false;
                    };
                    landingCam.text = UITextType.ShipLandingPrompt;

                    var landingTextCanvas = new GameObject().AddComponent<Canvas>();
                    landingTextCanvas.transform.SetParent(landingCam.transform.parent, false);
                    landingTextCanvas.gameObject.AddComponent<ConditionalRenderer>().GETShouldRender = () => ShouldRenderScreenText() && !isLandingCamEnabled;
                    landingTextCanvas.transform.localPosition = new Vector3(-0.017f, 3.731f, 5.219f);
                    landingTextCanvas.transform.localRotation = Quaternion.Euler(53.28f, 0, 0);
                    landingTextCanvas.transform.localScale = Vector3.one * 0.007f;
                    var landingText = new GameObject().AddComponent<Text>();
                    landingText.transform.SetParent(landingTextCanvas.transform, false);
                    landingText.transform.localScale = Vector3.one * 0.6f;
                    landingText.text = "<color=grey>LANDING CAMERA</color>\n\ninteract with screen\nto activate";
                    landingText.color = new Color(1, 1, 1, 0.1f);
                    landingText.alignment = TextAnchor.MiddleCenter;
                    landingText.fontSize = 8;
                    landingText.font = font;

                    autoPilot = cockpitTech.GetComponentInChildren<AutopilotButton>().GetComponent<ShipMonitorInteraction>();
                }

                private static Vector3 cameraPosition;
                private static Quaternion cameraRotation;

                private static void PreFindFrame(ReferenceFrameTracker __instance)
                {
                    if (__instance._isLandingView)
                    {
                        return;
                    }

                    var activeCam = __instance._activeCam.transform;
                    cameraPosition = activeCam.position;
                    cameraRotation = activeCam.rotation;

                    if (__instance._isMapView)
                    {
                        activeCam.position = mapGridRenderer.position + mapGridRenderer.up * 10000;
                        activeCam.rotation = Quaternion.LookRotation(mapGridRenderer.up * -1);
                    }
                    else
                    {
                        activeCam.position = LaserPointer.Behaviour.Laser.position;
                        activeCam.rotation = LaserPointer.Behaviour.Laser.rotation;
                    }
                }

                private static bool IsAnyInteractionFocused()
                {
                    return (probe != null && probe.IsFocused()) || 
                           (signalscope != null && signalscope.IsFocused()) || 
                           (landingCam != null && landingCam.IsFocused()) ||
                           (autoPilot != null && autoPilot.IsFocused());
                }

                private static bool PreUntargetFrame()
                {
                    return !IsAnyInteractionFocused();
                }

                private static ReferenceFrame PostFindFrame(ReferenceFrame __result, ReferenceFrameTracker __instance)
                {
                    if (__instance._isLandingView) return __result;

                    var activeCam = __instance._activeCam.transform;
                    activeCam.position = cameraPosition;
                    activeCam.rotation = cameraRotation;

                    return IsAnyInteractionFocused() ? __instance._currentReferenceFrame : __result;
                }
            }
        }
    }
}
