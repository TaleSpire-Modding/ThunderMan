﻿using Dummiesman;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BepInEx;

namespace LordAshes
{

    public partial class CustomMiniPlugin : BaseUnityPlugin
    {
        public class RequestHandler
        {
            // Directory for custom content
            private string dir = "";

            /// <summary>
            /// Constructor taking in the content directory and identifiers
            /// </summary>
            /// <param name="requestIdentifiers"></param>
            /// <param name="path"></param>
            public RequestHandler(string guid, string path)
            {
                this.dir = path;
            }

            private static readonly Dictionary<string,Action<CreatureBoardAsset, string>>
                ChangesActions = new Dictionary<string,Action<CreatureBoardAsset, string>>();

            public static void AppendChanges(string key, Action<CreatureBoardAsset, string> action)
            {
                ChangesActions.Add(key,action);
            }

            /// <summary>
            /// Callback method that is informed by StatMessaging when the Stat Block has changed.
            /// This is triggered on any changes in the Stat Block and thus it is the responsibilty of the
            /// callback function to determine which Stat Block changes are applicable to the plugin
            /// (typically by checking the change.key for an expected key)
            /// </summary>
            /// <param name="changes">Array of changes detected by the Stat Messaging plugin</param>
            public void Request(StatMessaging.Change[] changes)
            {
                // Process all changes
                foreach (StatMessaging.Change change in changes)
                {
                    // Find a reference to the indicated mini
                    CreaturePresenter.TryGetAsset(change.cid, out var asset);
                    // If the change is not for the CustomMiniPlugin key then ignore it
                    if (asset != null)
                    {
                        if (change.key != "org.lordashes.plugins.custommini" && ChangesActions.ContainsKey(change.key))
                        {
                            ChangesActions[change.key](asset, change.value);
                        }
                        // Process the request (since remove has a blank value this will trigger mesh removal)
                        else if (change.key == "org.lordashes.plugins.custommini")
                        {
                            LoadCustomContent(asset, dir + "Minis/" + change.value + "/" + change.value);
                        }
                    }
                }
            }

            /// <summary>
            /// Method to sync the transformation mesh with the character's stealth mode setting
            /// </summary>
            public void SyncStealthMode()
            {
                foreach (CreatureBoardAsset asset in CreaturePresenter.AllCreatureAssets.ToArray())
                {
                    try
                    {
                        if (asset.Creature.IsExplicitlyHidden)
                        {
                            if (asset.CreatureLoader.LoadedAsset.GetComponent<MeshRenderer>().enabled)
                            {
                                asset.CreatureLoader.LoadedAsset.GetComponent<MeshRenderer>().enabled = false;
                                asset.CreatureLoader.LoadedAsset.GetComponent<MeshRenderer>().forceRenderingOff = true;
                            }
                        }
                        else
                        {
                            if (!asset.CreatureLoader.LoadedAsset.GetComponent<MeshRenderer>().enabled)
                            {
                                asset.CreatureLoader.LoadedAsset.GetComponent<MeshRenderer>().enabled = true;
                                asset.CreatureLoader.LoadedAsset.GetComponent<MeshRenderer>().forceRenderingOff = false;
                            }
                        }
                    }
                    catch (Exception) {; }
                }
            }

            public static List<Func<string, CreatureGuid, bool>>
                missingContentCallbacks = new List<Func<string, CreatureGuid, bool>>();

            /// <summary>
            /// Adds a custom mesh game object to the indicated asset remove any previous attached mesh objects
            /// </summary>
            /// <param name="asset">Parent asset to whom the custom mesh will be attached</param>
            /// <param name="source">Path and name of the content file</param>
            public static void LoadCustomContent(CreatureBoardAsset asset, string source)
            {
                try
                {
                    UnityEngine.Debug.Log("Customizing Mini '" + asset.Creature.Name + "' Using '" + source + "'...");

                    // Effects are prefixed by # tag
                    bool effect = (source.IndexOf("#") > -1);
                    source = source.Replace("#", "");
                    string prefix = (effect) ? "CustomEffect:" : "CustomContent:";
                    Debug.Log("Effect = " + effect);

                    // Look up the content name to see if the actual file has an extenion or not
                    if (System.IO.Path.GetFileNameWithoutExtension(source) != "")
                    {
                        // Obtain file name of the content
                        if (System.IO.File.Exists(source))
                        {
                            // Asset Bundle
                        }
                        else if (System.IO.File.Exists(source + ".OBJ"))
                        {
                            // OBJ File
                            source = source + ".OBJ";
                        }
                        else
                        {
                            // No Compatibale Content Found
                            foreach (var callback in missingContentCallbacks)
                            {
                                if (callback(asset.Creature.Name, asset.Creature.CreatureId)) break;
                            }
                            return;
                        }
                    }
                    else
                    {
                        // Source is blank meaning this is a remove request
                        if (!effect)
                        {
                            Debug.Log("Destorying '" + asset.Creature.Name + "' mesh...");
                            asset.CreatureLoader.LoadedAsset.GetComponent<MeshFilter>().mesh.triangles = new int[0];
                        }
                        else
                        {
                            Debug.Log("Destorying '" + asset.Creature.Name + "' effect...");
                            GameObject.Destroy(GameObject.Find(prefix + asset.Creature.CreatureId));
                        }
                        return;
                    }

                    if (System.IO.File.Exists(source))
                    {
                        GameObject content = null;
                        // Determine which type of content it is 
                        switch (System.IO.Path.GetExtension(source).ToUpper())
                        {
                            case "": // AssetBundle Source
                                UnityEngine.Debug.Log("Using AssetBundle Loader");
                                string assetBundleName = System.IO.Path.GetFileNameWithoutExtension(source);
                                AssetBundle assetBundle = null;
                                foreach (AssetBundle ab in AssetBundle.GetAllLoadedAssetBundles())
                                {
                                    // Debug.Log("Checking Existing AssetBundles: Found '" + ab.name + "'. Seeking '"+assetBundleName+"'");
                                    if (ab.name == assetBundleName) { UnityEngine.Debug.Log("AssetBundle Is Already Loaded. Reusing."); assetBundle = ab; break; }
                                }
                                if (assetBundle == null) { UnityEngine.Debug.Log("AssetBundle Is Not Already Loaded. Loading."); assetBundle = AssetBundle.LoadFromFile(source); }
                                content = GameObject.Instantiate(assetBundle.LoadAsset<GameObject>(System.IO.Path.GetFileNameWithoutExtension(source)));
                                break;
                            case ".MINI": // AssetBundle Source
                                UnityEngine.Debug.Log("Using AssetBundle Loader");
                                string massetBundleName = System.IO.Path.GetFileNameWithoutExtension(source);
                                AssetBundle massetBundle = null;
                                foreach (AssetBundle ab in AssetBundle.GetAllLoadedAssetBundles())
                                {
                                    // Debug.Log("Checking Existing AssetBundles: Found '" + ab.name + "'. Seeking '"+assetBundleName+"'");
                                    if (ab.name == massetBundleName) { UnityEngine.Debug.Log("AssetBundle Is Already Loaded. Reusing."); massetBundle = ab; break; }
                                }
                                if (massetBundle == null) { UnityEngine.Debug.Log("AssetBundle Is Not Already Loaded. Loading."); massetBundle = AssetBundle.LoadFromFile(source); }
                                content = GameObject.Instantiate(massetBundle.LoadAsset<GameObject>(System.IO.Path.GetFileNameWithoutExtension(source)));
                                break;
                            case ".OBJ": // OBJ/MTL Source
                                UnityEngine.Debug.Log("Using OBJ/MTL Loader");
                                if (!System.IO.File.Exists(System.IO.Path.GetDirectoryName(source) + "/" + System.IO.Path.GetFileNameWithoutExtension(source) + ".mtl"))
                                {
                                    foreach (var callback in missingContentCallbacks)
                                    {
                                        if (callback(asset.Creature.Name + " (" + System.IO.Path.GetDirectoryName(source) + "/" + System.IO.Path.GetFileNameWithoutExtension(source) + ".mtl)", asset.Creature.CreatureId)) break;
                                    }
                                }
                                UnityExtension.ShaderDetector.Reference(System.IO.Path.GetDirectoryName(source) + "/" + System.IO.Path.GetFileNameWithoutExtension(source) + ".mtl");
                                content = new OBJLoader().Load(source);
                                break;
                            default: // Unrecognized Source
                                Debug.Log("Content Type '" + System.IO.Path.GetExtension(source).ToUpper() + "' is not supported. Use OBJ/MTL or FBX.");
                                break;
                        }
                        content.name = prefix + asset.Creature.CreatureId;

                        if (!effect)
                        {
                            try
                            {
                                asset.CreatureLoader.transform.position = new Vector3(0, 0, 0);
                                asset.CreatureLoader.transform.rotation = Quaternion.Euler(0, 0, 0);
                                asset.CreatureLoader.transform.eulerAngles = new Vector3(0, 0, 0);
                                asset.CreatureLoader.transform.localPosition = new Vector3(0, 0, 0);
                                asset.CreatureLoader.transform.localRotation = Quaternion.Euler(0, 180, 0);
                                asset.CreatureLoader.transform.localEulerAngles = new Vector3(0, 180, 0);
                                asset.CreatureLoader.transform.localScale = new Vector3(1f, 1f, 1f);
                                ReplaceGameObjectMesh(content, asset.CreatureLoader.LoadedAsset);
                            }
                            catch (Exception) {; }
                            UnityEngine.Debug.Log("Destroying Template...");
                            GameObject.Destroy(GameObject.Find(prefix + asset.Creature.CreatureId));
                        }
                    }
                    else
                    {
                        SystemMessage.DisplayInfoText("I don't know about\r\n" + System.IO.Path.GetFileNameWithoutExtension(source));
                    }
                }
                catch (Exception) {; }
            }

            /// <summary>
            /// Method to replace the destination MeshFilter and MeshRenderer with that of the source.
            /// Since component cannot be actually switched, all properties are copied over.
            /// </summary>
            /// <param name="source"></param>
            /// <param name="destination"></param>
            public static void ReplaceGameObjectMesh(GameObject source, GameObject destination)
            {
                MeshFilter dMF = destination.GetComponent<MeshFilter>();
                MeshRenderer dMR = destination.GetComponent<MeshRenderer>();
                if (dMF == null || dMR == null) { Debug.LogWarning("Unable get destination MF or MR."); return; }

                destination.transform.position = new Vector3(0, 0, 0);
                destination.transform.rotation = Quaternion.Euler(0, 0, 0);
                destination.transform.eulerAngles = new Vector3(0, 0, 0);
                destination.transform.localPosition = new Vector3(0, 0, 0);
                destination.transform.localRotation = Quaternion.Euler(0, 0, 0);
                destination.transform.localEulerAngles = new Vector3(0, 0, 0);
                destination.transform.localScale = new Vector3(1f, 1f, 1f);

                dMF.transform.position = new Vector3(0, 0, 0);
                dMF.transform.rotation = Quaternion.Euler(0, 0, 0);
                dMF.transform.eulerAngles = new Vector3(0, 0, 0);
                dMF.transform.localPosition = new Vector3(0, 0, 0);
                dMF.transform.localRotation = Quaternion.Euler(0, 0, 0);
                dMF.transform.localEulerAngles = new Vector3(0, 0, 0);
                dMF.transform.localScale = new Vector3(1, 1, 1);

                dMR.transform.position = new Vector3(0, 0, 0);
                dMR.transform.rotation = Quaternion.Euler(0, 0, 0);
                dMR.transform.eulerAngles = new Vector3(0, 0, 0);
                dMR.transform.localPosition = new Vector3(0, 0, 0);
                dMR.transform.localRotation = Quaternion.Euler(0, 0, 0);
                dMR.transform.localEulerAngles = new Vector3(0, 0, 0);
                dMR.transform.localScale = new Vector3(1, 1, 1);

                MeshFilter sMF = (source.GetComponent<MeshFilter>() != null) ? source.GetComponent<MeshFilter>() : source.GetComponentInChildren<MeshFilter>();
                if (sMF != null)
                {
                    Debug.Log("Copying MF->MF");
                    dMF.mesh = sMF.mesh;
                    dMF.sharedMesh = sMF.sharedMesh;
                }

                MeshRenderer sMR = (source.GetComponent<MeshRenderer>() != null) ? source.GetComponent<MeshRenderer>() : source.GetComponentInChildren<MeshRenderer>();
                if (sMR != null)
                {
                    Debug.Log("Copying MR->MR");
                    dMR.material = sMR.material;
                    dMR.materials = sMR.materials;
                    dMR.sharedMaterial = sMR.sharedMaterial;
                    dMR.sharedMaterials = sMR.sharedMaterials;
                }

                SkinnedMeshRenderer sSMR = (source.GetComponent<SkinnedMeshRenderer>() != null) ? source.GetComponent<SkinnedMeshRenderer>() : source.GetComponentInChildren<SkinnedMeshRenderer>();
                if (sSMR != null)
                {
                    Debug.Log("Copying SMR->MF/MR");
                    dMF.sharedMesh = sSMR.sharedMesh;
                    dMR.material = sSMR.material;
                    dMR.materials = sSMR.materials;
                    dMR.sharedMaterial = sSMR.sharedMaterial;
                    dMR.sharedMaterials = sSMR.sharedMaterials;
                }
            }
        }
    }
}